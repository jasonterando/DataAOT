using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DataAOT.Generator;

internal class ActorSyntaxReceiver : ISyntaxReceiver
{
    public List<ClassDeclarationSyntax> CandidateGateways { get; } = new();
    public List<ClassDeclarationSyntax> CandidateModels { get; } = new();

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        if (syntaxNode is ClassDeclarationSyntax classSyntax)
        {
            // Look for database gateways
            if (classSyntax.GetGatewayBaseType() != null)
            {
                CandidateGateways.Add(classSyntax);    
            }
            
            // Look for table models
            if (classSyntax.HasAttribute("Table"))
            {
                CandidateModels.Add(classSyntax);    
            }
            
        }
    }
}