using AvroNet.Schemas;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace AvroNet;

[Generator(LanguageNames.CSharp)]
internal sealed partial class AvroGenerator : IIncrementalGenerator
{
    internal const string AvroModelAttributeName = "AvroModelAttribute";
    internal const string AvroModelAttributeFullName = $"AvroNet.{AvroModelAttributeName}";
    internal const string AvroClassSchemaConstName = "SchemaJson";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static context =>
            context.AddSource("AvroModelAttribute.g.cs", SourceText.From(AvroModelAttribute, Encoding.UTF8)));

        var models = context.SyntaxProvider
            .ForAttributeWithMetadataName(AvroModelAttributeFullName,
                predicate: static (node, cancellationToken) =>
                {
                    if (!node.IsKind(SyntaxKind.ClassDeclaration) && !node.IsKind(SyntaxKind.RecordDeclaration))
                        return false;

                    return Unsafe.As<TypeDeclarationSyntax>(node).Modifiers.Any(SyntaxKind.PartialKeyword);
                },
                transform: static (context, cancellationToken) =>
                {
                    var typeDeclaration = Unsafe.As<TypeDeclarationSyntax>(context.TargetNode);
                    var typeSymbol = Unsafe.As<INamedTypeSymbol>(context.TargetSymbol);
                    var modelFeatures = (AvroModelFeatures)context.Attributes
                        .Single(attr => attr.AttributeClass?.Name == AvroModelAttributeName)
                        .ConstructorArguments[0].Value!;

                    var modelSchema = default(string);
                    for (int i = 0; i < typeDeclaration.Members.Count; ++i)
                    {
                        if (typeDeclaration.Members[i] is FieldDeclarationSyntax field)
                        {
                            var schemaJson = field.Declaration.Variables
                                .FirstOrDefault(v => v.Identifier.Text == AvroClassSchemaConstName);
                            if (schemaJson is not null)
                            {
                                var schemaJsonSymbol = (IFieldSymbol)context.SemanticModel
                                    .GetDeclaredSymbol(schemaJson, cancellationToken)!;
                                if (schemaJsonSymbol.IsConst)
                                {
                                    modelSchema = (string?)schemaJsonSymbol.ConstantValue;
                                    break;
                                }
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(modelSchema))
                        throw new NotSupportedException("add a diagnostic here for 'schema is null or empty'");

                    return new AvroModelOptions(
                        Name: typeSymbol.Name,
                        Schema: modelSchema!,
                        Namespace: typeSymbol.ContainingNamespace?.ToDisplayString()! ?? "",
                        AccessModifier: typeDeclaration.Modifiers.Any(SyntaxKind.PublicKeyword) ? "public" : "internal",
                        DeclarationType: typeDeclaration.IsKind(SyntaxKind.RecordDeclaration) ? "partial record class" : "partial class",
                        Features: modelFeatures
                    );
                });

        context.RegisterSourceOutput(models, static (context, options) =>
        {
            using var writer = new SourceTextWriter(options);
            using var document = JsonDocument.Parse(options.Schema);
            writer.Write(new AvroSchema(document.RootElement));
            var sourceText = writer.ToString();
            context.AddSource($"{options.Name}.AvroModel.g.cs", SourceText.From(sourceText, Encoding.UTF8));
        });
    }
}
