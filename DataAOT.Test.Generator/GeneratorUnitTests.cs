using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using DataAOT.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit.Abstractions;

namespace DataAOT.Test.Generator;

public class GeneratorUnitTests : IClassFixture<GeneratorTestFixture>
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly GeneratorTestFixture _fixture;

    public GeneratorUnitTests(GeneratorTestFixture fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void NoGeneratorDiagnostics()
    {
        foreach (var generatorDiag in _fixture.GeneratorDiagnostics)
        {
            _testOutputHelper.WriteLine(generatorDiag.ToString());
        }

        Assert.Empty(_fixture.GeneratorDiagnostics);
    }

    [Fact]
    public void NoCompilerDiagnostics()
    {
        foreach (var compilerDiag in _fixture.CompilerDiagnostics)
        {
            _testOutputHelper.WriteLine(compilerDiag.ToString());
        }

        Assert.Empty(_fixture.CompilerDiagnostics);
    }
}

public class GeneratorTestFixture : IDisposable
{
    public readonly ImmutableArray<Diagnostic> GeneratorDiagnostics;
    public readonly ImmutableArray<Diagnostic> CompilerDiagnostics;
    public readonly SyntaxTree GeneratedSyntaxTree;

    public GeneratorTestFixture()
    {
        var userSource = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "TestFiles", "TestModel.cs.txt")
        );

        var compilation = CreateCompilation(userSource);
        var generators = new DbGatewayGenerator();

        var result = CSharpGeneratorDriver.Create(
                generators: ImmutableArray.Create(generators),
                additionalTexts: ImmutableArray<AdditionalText>.Empty,
                parseOptions: (CSharpParseOptions) compilation.SyntaxTrees.First().Options,
                optionsProvider: null
            )
            .RunGeneratorsAndUpdateCompilation(compilation,
                out var updatedCompilation,
                out var diagnostics)
            .GetRunResult();

        CompilerDiagnostics = updatedCompilation.GetDiagnostics();
        GeneratorDiagnostics = diagnostics;
        GeneratedSyntaxTree = result.Results.First().GeneratedSources[0].SyntaxTree;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private Compilation CreateCompilation(string source)
    {
        var referenceMap = new SortedDictionary<string, PortableExecutableReference>();
        BuildAssemblyReferences(typeof(DbField).Assembly, referenceMap); // Data.AOT
        BuildAssemblyReferences(typeof(DbGatewayGenerator).Assembly, referenceMap); // Data.AOT.Generators
        BuildAssemblyReferences(typeof(TableAttribute).Assembly, referenceMap); // System.ComponentModel.Annotations

        var references = referenceMap.Values.ToList();

        foreach (var reference in references)
        {
            Console.WriteLine(reference.Display);
        }

        return CSharpCompilation.Create(
            assemblyName: "compilation",
            syntaxTrees: new[] {CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))},
            references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
    }

    // private GeneratorDriverRunResult RunGenerators(Compilation compilation, out ImmutableArray<Diagnostic> diagnostics,
    //     params ISourceGenerator[] generators)
    // {
    // }

    private void BuildAssemblyReferences(Assembly assemblyToAnalyze,
        IDictionary<string, PortableExecutableReference> references)
    {
        if (string.IsNullOrEmpty(assemblyToAnalyze.FullName)) return;
        if (references.ContainsKey(assemblyToAnalyze.FullName)) return;

        references[assemblyToAnalyze.FullName] = MetadataReference.CreateFromFile(assemblyToAnalyze.Location);

        foreach (var reference in assemblyToAnalyze.GetReferencedAssemblies())
        {
            try
            {
                BuildAssemblyReferences(Assembly.Load(reference), references);
            }
            catch (Exception ex)
            {
                throw new Exception($"Could not load {reference}: {ex.Message}");
            }
        }
    }
}