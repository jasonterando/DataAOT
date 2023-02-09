#!/usr/bin/env python3
#
# publish-nuget.py
#
# This is a utility to publish NuGet projects and manage version numbering.
# It will use the .csproj to get the current package version number,
# and update the .csproj file with new package and assembly version numbers
# upon deployment
#
# Usage: publish-nuget.py [--test] [--release] { build, patch, minor, major }
#
# The semantic version number of the package (x.y.z-build000) will be updated
# based upon whether major, minor, patch or build are specified.
#
# The utility checks Git to make sure all changes have been committed
# and will push and commit a tag corresponding to the updated version.
# To disable this functionality, use the "--git-disable" or "-g" flag.
#
# You can see what the generated version number will be, without publishing
# or pushing to Git, by using the "--test" or "-t" flag.
#
# By default, versions are assumed to be pre-production builds, meaning that
# versions will be appended with a "-build000" suffix, where "000" is an
# incremental build number.
#
# To publish the current build as pre-release, use:
# publish-nuget.py build
#
# To publish the current build as a release, use:
# publish-nuget.py --release build
#
# To publish a new pre-release build and increment the
# major, minor or patch number, use:
# publish-nuget.py major/minor/patch
#
# To create a new release build and increment the
# major, minor or patch number, use:
# publish-nuget.py major/minor/patch

import argparse
import os.path
import shutil
import subprocess
import tempfile
from enum import Enum
from pathlib import Path
from xml.dom import minidom
import re

# TODO:  make these injectable via env variables?
NUGET_SOURCE = "GitLab"

# Someday, dotnet nuget publish will be updated to support this
USE_SEMANTIC_VERSION_2 = False


class IncrementType(Enum):
    build = 1
    patch = 2
    minor = 3
    major = 4


# Manage a .csproj file
# noinspection SpellCheckingInspection
class CsharpProject:
    def __init__(self, filename):
        print("Processing project file " + filename)
        self.parser = minidom.parse(filename)
        self.filename = filename

        elements_group = self.parser.getElementsByTagName("PropertyGroup")
        if len(elements_group) == 0:
            raise Exception(self.filename + " does not contain a PropertyGroup element")
        element_group = elements_group[0]

        self.package = CsharpProject.find_element(self.parser, element_group, "PackageVersion")
        self.assembly = CsharpProject.find_element(self.parser, element_group, "AssemblyVersion")
        self.name = CsharpProject.find_element(self.parser, element_group, "AssemblyName")

        if len(self.name.data) == 0:
            raise Exception(self.filename + " does not have AssemblyName set")

        version_number = self.package.data
        if len(version_number) == 0:
            version_number = "0.0.0"
        self.version = SemanticVersion.build(version_number)

    # Find or create element in parent, return the first child text node of that element
    @staticmethod
    def find_element(doc, parent, name):
        elements = parent.getElementsByTagName(name)
        if len(elements) == 0:
            element = doc.createElement(name)
            node = doc.createTextNode("")
            element.appendChild(node)
            parent.appendChild(element)
        else:
            node = elements[0].firstChild
        return node

    # Deploy the assembly based upon the current version number
    def deploy(self, test):
        v = self.version.to_assembly()
        print("Setting assembly version to " + v)
        self.assembly.data = v
        if self.version.is_release():
            v = self.version.to_release_debug()
            print("Deploy debug release " + v)
            self.package.data = v
            if not test:
                self.save()
                self.publish(v, "Debug")

            v = self.version.to_release()
            print("Deploy release " + v)
            self.package.data = v
            if not test:
                self.save()
                self.publish(v, "Release")
        else:
            v = self.version.to_build()
            print("Deploy build " + v)
            self.package.data = v
            if not test:
                self.save()
                self.publish(v, "Debug")

    # Build and publish the package
    def publish(self, version, config):
        command = ["dotnet", "build", "-c", config]
        result = subprocess.run(command)
        if result.returncode != 0:
            raise Exception(
                "Unable to execute build: \"" + ' '.join(command) + "\", exit code: " + str(result.returncode))

        command = ["dotnet", "pack", "-c", config]
        result = subprocess.run(command)
        if result.returncode != 0:
            raise Exception(
                "Unable to execute pack: \"" + ' '.join(command) + "\", exit code: " + str(result.returncode))

        command = ["dotnet",
                   "nuget",
                   "push",
                   os.path.join("bin", config, self.name.data + "." + version + ".nupkg"),
                   "--source",
                   NUGET_SOURCE]
        result = subprocess.run(command)
        if result.returncode != 0:
            raise Exception(
                "Unable to execute NuGet push: \"" + ' '.join(command) + "\", exit code: " + str(result.returncode))

    # Save the XML file
    def save(self):
        file = open(self.filename, "w")
        # writexml makes ugly xml
        # self.parser.writexml(file)
        xml = '\n'.join([line for line in self.parser.toprettyxml(indent=' ' * 4).split('\n') if line.strip()])
        file.write(xml)
        file.close()


# Manage Git status
class CheckGit:
    @staticmethod
    def pending_count():
        p = subprocess.Popen(['git', 'status', '--porcelain'], stdout=subprocess.PIPE)
        result = p.communicate()[0].decode("utf-8")
        return 0 if len(result) == 0 else len(result.split('\n'))

    @staticmethod
    def tag(version):
        command = ["git", "add", "*"]
        result = subprocess.run(command)
        if result.returncode != 0:
            raise Exception(
                "Unable to execute git add: \"" + ' '.join(command) + "\", exit code: " + str(result.returncode))

        command = ["git", "commit", "-m", "Tag version " + version]
        result = subprocess.run(command)
        if result.returncode != 0:
            raise Exception(
                "Unable to execute git push: \"" + ' '.join(command) + "\", exit code: " + str(result.returncode))

        command = ["git", "push"]
        result = subprocess.run(command)
        if result.returncode != 0:
            raise Exception(
                "Unable to execute git push: \"" + ' '.join(command) + "\", exit code: " + str(result.returncode))

        command = ["git", "tag", version]
        result = subprocess.run(command)
        if result.returncode != 0:
            raise Exception(
                "Unable to execute git tag: \"" + ' '.join(command) + "\", exit code: " + str(result.returncode))

        command = ["git", "push", "--tag"]
        result = subprocess.run(command)
        if result.returncode != 0:
            raise Exception(
                "Unable to execute git push tag: \"" + ' '.join(command) + "\", exit code: " + str(result.returncode))


# Manage a semantic version number
class SemanticVersion:
    def __init__(self, major, minor, patch, build_label, build_version):
        self.maj = major
        self.min = minor
        self.pat = patch
        self.lbl = build_label
        self.ver = build_version

    @staticmethod
    def build(version_number):
        parts = version_number.split('-')
        if len(parts) == 1:
            prefix = parts[0]
            suffix = ""
        elif len(parts) == 2:
            prefix = parts[0]
            suffix = parts[1]
        else:
            raise Exception("Stored version number \"" + version_number + "\" has more than one hyphen")

        segments = prefix.split('.')
        if len(segments) != 3:
            raise Exception("Stored version number \"" + version_number + "\" is not in Maj.Min.Patch format")

        major = int(segments[0])
        minor = int(segments[1])
        patch = int(segments[2])

        if len(suffix) == 0:
            buildLabel = ""
            build_version = 0
        else:
            # Semantic versioning 2 separates build label and number by a period,
            # older versions do not
            parts = suffix.split('.')
            if len(parts) == 2:
                buildLabel = parts[0]
                build_version = int(parts[1])
            # If not using semantic versioning 2, split by label and number
            elif len(parts) == 1:
                m = re.match(r"^(\D+)(\d*)$", parts[0]).groups()
                buildLabel = m[0]
                build_version = int(m[1])
            else:
                raise Exception("Stored version number \"" + version_number + "\" has an invalid build segment")

        return SemanticVersion(major, minor, patch, buildLabel, build_version)

    def increment(self, incr, release):
        if incr == IncrementType.major:
            self.maj = self.maj + 1
            self.min = 0
            self.pat = 0
            if release:
                self.ver = 0
            else:
                self.ver = 1
        elif incr == IncrementType.minor:
            self.min = self.min + 1
            self.pat = 0
            if release:
                self.ver = 0
            else:
                self.ver = 1
        elif incr == IncrementType.patch:
            self.pat = self.pat + 1
            if release:
                self.ver = 0
            else:
                self.ver = 1
        elif incr == IncrementType.build:
            if self.is_release():
                # If the current version is already a release, increment the patch version
                # and reset the build number
                self.pat = self.pat + 1
                self.ver = 1
            elif release:
                # If we are releasing the current build, clear the build version
                self.ver = 0
            else:
                self.ver = self.ver + 1

    # Determine if this version is release or not by virtual of a build prefix
    def is_release(self):
        return self.ver == 0

    # Translate to a build string
    def to_build(self):
        lbl = 'build' if len(self.lbl) == 0 else self.lbl
        return str(self.maj) + "." + str(self.min) + "." + str(self.pat) + "-" \
            + lbl \
            + ('.' if USE_SEMANTIC_VERSION_2 else '') \
            + '{:0>4}'.format(self.ver)

    # Translate to a release string
    def to_release(self):
        return str(self.maj) + "." + str(self.min) + "." + str(self.pat)

    # Translate to a release string with a debug build suffix
    def to_release_debug(self):
        return str(self.maj) + "." + str(self.min) + "." + str(self.pat) + "-debug"

    # Translate to an assembly string
    def to_assembly(self):
        s = str(self.maj) + "." + str(self.min) + "." + str(self.pat)
        if not self.is_release():
            s = s + '.' + str(self.ver)
        return s


#
# Main Routine
#
parser = argparse.ArgumentParser(
    description='NuGet package version and publish manager')

parser.add_argument("-r", "--release", default=False, action='store_true',
                    help="Make deployment as a Release package")
parser.add_argument("-t", "--test", default=False, action='store_true',
                    help="Display updated version numbers, but do not process")
parser.add_argument("-g", "--git-disable", default=False, action='store_true',
                    help="Disable git check and tag push")
parser.add_argument("increment", help="Type of version increment",
                    choices=['build', 'patch', 'minor', 'major'])

args = parser.parse_args()

f = next(Path(".").glob("*.csproj"), None)
if f is None:
    raise Exception("Unable to locate .csproj file in current directory")
f = str(f)

if not args.git_disable:
    pending = CheckGit.pending_count()
    if pending > 0:
        exit("You have git changes pending, please commit all changes")

with tempfile.TemporaryDirectory() as tmp:
    b = os.path.join(tmp, 'backup.csproj')
    try:
        # Backup the csproj file in case something goes wrong
        shutil.copy(f, b)

        csproj = CsharpProject(str(f))

        old = csproj.version.to_release() if csproj.version.is_release() else csproj.version.to_build()
        csproj.version.increment(IncrementType[args.increment], args.release)
        new = csproj.version.to_release() if csproj.version.is_release() else csproj.version.to_build()
        print("Updating " + csproj.name.data + " from " + old + " to " + new)
        if args.test:
            print("Test mode specified, changes will NOT be saved or published...")

        csproj.deploy(args.test)

        if not args.test or args.git_disable:
            try:
                CheckGit.tag(new)
            except Exception as ex1:
                print("Error: " + str(ex1))
                exit(-1)

    except Exception as ex:
        print("Restoring original copy of " + f)
        shutil.copy(b, f)
        print("Error: " + str(ex))
        exit(-1)
