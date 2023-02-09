using System;

namespace DataAOT;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

public static class RoslynExtensions
{
    public static IEnumerable<ITypeSymbol> GetBaseTypesAndThis(this ITypeSymbol type)
    {
        var current = type;
        while (current != null)
        {
            yield return current;
            current = current.BaseType;
        }
    }

    public static IEnumerable<ISymbol> GetAllMembers(this ITypeSymbol type)
    {
        return type.GetBaseTypesAndThis().SelectMany(n => n.GetMembers());
    }

    public static CompilationUnitSyntax GetCompilationUnit(this SyntaxNode syntaxNode)
    {
        return syntaxNode.Ancestors().OfType<CompilationUnitSyntax>().FirstOrDefault() 
               ?? throw new Exception("Unable to get compilation unit");
    }

    public static string GetClassName(this ClassDeclarationSyntax proxy)
    {
        return proxy.Identifier.Text;
    }

    public static string GetClassModifier(this ClassDeclarationSyntax proxy)
    {
        return proxy.Modifiers.ToFullString().Trim();
    }

    /// <summary>
    /// Return true if the class definition includes the specified attribute annotation
    /// </summary>
    /// <param name="classSyntax"></param>
    /// <param name="attributeName"></param>
    /// <returns></returns>
    public static bool HasAttribute(this ClassDeclarationSyntax classSyntax, string attributeName)
    {
        return classSyntax.AttributeLists.Count > 0 &&
               classSyntax.AttributeLists.SelectMany(al => al.Attributes
                       .Where(a => (a.Name as IdentifierNameSyntax)?.Identifier.Text == attributeName))
                   .Any();
    }

    public static bool HasAttribute(this MethodDeclarationSyntax methodSyntax, string attributeName)
    {
        return methodSyntax.AttributeLists.Count > 0 &&
               methodSyntax.AttributeLists.SelectMany(al => al.Attributes
                       .Where(a => (a.Name as IdentifierNameSyntax)?.Identifier.Text == attributeName))
                   .Any();
    }

    
    public static string GetNamespace(this CompilationUnitSyntax root)
    {
        return root.ChildNodes()
            .OfType<NamespaceDeclarationSyntax>()
            .FirstOrDefault()
            ?.Name
            .ToString()
            ?? throw new Exception("Namespace not found in node");
    }

    public static List<string> GetUsings(this CompilationUnitSyntax root)
    {
        return root.ChildNodes()
            .OfType<UsingDirectiveSyntax>()
            .Select(n => n.Name.ToString())
            .ToList();
    }
    
    
    public static ClassDeclarationSyntax GetClass(this MethodDeclarationSyntax methodSyntax)
    {
        var parent = methodSyntax.Parent;
        while(parent != null)
        {
            if (parent is ClassDeclarationSyntax classSyntax) return classSyntax;
            parent = parent.Parent;
        }
        throw new Exception($"Class not found for {methodSyntax.Identifier}");
    }

    public static string GetNamespace(this BaseTypeDeclarationSyntax declarationSyntax)
    {
        // If we don't have a namespace at all we'll return an empty string
        // This accounts for the "default namespace" case
        var nameSpace = string.Empty;

        // Get the containing syntax node for the type declaration
        // (could be a nested type, for example)
        var potentialNamespaceParent = declarationSyntax.Parent;
    
        // Keep moving "out" of nested classes etc until we get to a namespace
        // or until we run out of parents
        while (potentialNamespaceParent != null &&
               potentialNamespaceParent is not NamespaceDeclarationSyntax
               && potentialNamespaceParent is not FileScopedNamespaceDeclarationSyntax
               )
        {
            potentialNamespaceParent = potentialNamespaceParent.Parent;
        }

        // Build up the final namespace by looping until we no longer have a namespace declaration
        if (potentialNamespaceParent is BaseNamespaceDeclarationSyntax namespaceParent)
        {
            // We have a namespace. Use that as the type
            nameSpace = namespaceParent.Name.ToString();
        
            // Keep moving "out" of the namespace declarations until we 
            // run out of nested namespace declarations
            while (true)
            {
                if (namespaceParent.Parent is not NamespaceDeclarationSyntax parent)
                {
                    break;
                }

                // Add the outer namespace as a prefix to the final namespace
                nameSpace = $"{namespaceParent.Name}.{nameSpace}";
                namespaceParent = parent;
            }
        }

        if (string.IsNullOrEmpty(nameSpace))
        {
            throw new Exception("Namespace not found");
        }
        
        // return the final namespace
        return nameSpace;        
    }
    
    
    public static IReadOnlyList<AttributeData> GetAttributes(this AttributeListSyntax attributes, Compilation compilation)
    {
        // Collect pertinent syntax trees from these attributes
        var acceptedTrees = new HashSet<SyntaxTree>();
        foreach (var attribute in attributes.Attributes)
            acceptedTrees.Add(attribute.SyntaxTree);

        var parentSymbol = attributes.Parent!.GetDeclaredSymbol(compilation)!;
        var parentAttributes = parentSymbol.GetAttributes();
        var ret = new List<AttributeData>();
        foreach (var attribute in parentAttributes)
        {
            if (acceptedTrees.Contains(attribute.ApplicationSyntaxReference!.SyntaxTree))
                ret.Add(attribute);
        }

        return ret;
    }

    public static ISymbol? GetDeclaredSymbol(this SyntaxNode node, Compilation compilation)
    {
        var model = compilation.GetSemanticModel(node.SyntaxTree);
        return model.GetDeclaredSymbol(node);
    }

    /// <summary>
    /// Get attribute value as string based upon positional index of constructor parameter
    /// </summary>
    /// <param name="attribute"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    public static string? GetAttributeStringValue(this AttributeData attribute, int index) =>
        attribute.ConstructorArguments.Length > index
            ? attribute.ConstructorArguments[index].Value?.ToString()
            : null;

    /// <summary>
    /// Get attribute value as string based upon named argument
    /// </summary>
    /// <param name="attribute"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static string? GetAttributeStringValue(this AttributeData attribute, string name) =>
        attribute.NamedArguments.Where(a => a.Key == name)
            .Select(a => a.Value.ToString())
            .FirstOrDefault();

    /// <summary>
    /// Get attribute value as enum based upon positional index of constructor parameter
    /// </summary>
    /// <param name="attribute"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    public static T? GetAttributeEnumValue<T>(this AttributeData attribute, int index) where T : Enum
    {
        if (attribute.ConstructorArguments.Length > index)
        {
            if (attribute.ConstructorArguments[index].Value != null)
            {
                return (T?) attribute.ConstructorArguments[index].Value;
            }
        }
        return default;
    }

    /// <summary>
    /// Get attribute value as enum based upon named argument
    /// </summary>
    /// <param name="attribute"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static T? GetAttributeEnumValue<T>(this AttributeData attribute, string name) where T : Enum
    {
        foreach (var arg in attribute.NamedArguments)
        {
            if (arg.Key == name)
            {
                return (T?) arg.Value.Value;
            }
        }
        return default;
    }

    /// <summary>
    /// Returns the gateway base type declaration if class derives from DbGateway
    /// TODO:  Check for IDbGateway inheritance
    /// </summary>
    /// <param name="classSyntax"></param>
    /// <returns></returns>
    public static BaseTypeSyntax? GetGatewayBaseType(this ClassDeclarationSyntax classSyntax)
    {
        return classSyntax.BaseList?.Types.FirstOrDefault(
            baseType => baseType.Type.ToString().StartsWith("DbGateway<"));

    }
}