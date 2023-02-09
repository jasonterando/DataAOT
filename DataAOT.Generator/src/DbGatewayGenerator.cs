using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DataAOT.Generator
{
    [Generator]
    public class DbGatewayGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new ActorSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxReceiver is not ActorSyntaxReceiver actorSyntaxReceiver) return;

            // Iterate through each method that has our attribute
            foreach (var gatewaySyntax in actorSyntaxReceiver.CandidateGateways)
            {
                // var namespaceName = classSyntax.GetNamespace();
                var gatewayClassName = gatewaySyntax.Identifier.Text;
                var gatewaySymbol = gatewaySyntax.GetDeclaredSymbol(context.Compilation) as INamedTypeSymbol
                                        ?? throw new Exception($"Unable to get {gatewayClassName} symbol");

                var gatewayBaseType = gatewaySyntax.GetGatewayBaseType()?.Type as GenericNameSyntax
                                      ?? throw new Exception($"Unable to find DbGateway for {gatewayClassName}");
                var modelClassName = gatewayBaseType.TypeArgumentList.Arguments.First().ToString();
                var modelSyntax =
                    actorSyntaxReceiver.CandidateModels.FirstOrDefault(m => m.Identifier.Text == modelClassName)
                    ?? throw new Exception($"Unable to find model {modelClassName}");
                var modelSymbol = modelSyntax.GetDeclaredSymbol(context.Compilation) as INamedTypeSymbol
                                        ?? throw new Exception($"Unable to get {modelClassName} symbol");

                var renderer = new DBGatewayTemplateRenderer(gatewaySyntax, gatewaySymbol, modelSymbol);
                var code = renderer.Render();
                
                // var result = SyntaxFactory.ParseCompilationUnit(code)
                //     .NormalizeWhitespace();
                //     .GetText()
                //     .ToString();                

                context.AddSource($"{gatewayClassName}.{modelClassName}",
                    SourceText.From(code, Encoding.UTF8));
            }
        }
    }
}