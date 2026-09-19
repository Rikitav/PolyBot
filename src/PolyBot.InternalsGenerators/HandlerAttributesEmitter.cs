using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PolyBot.SourceGenerators;

namespace PolyBot.InternalsGenerators;

/// <summary>
/// Generates one handler attribute class per <c>Telegram.Bot.Types.Enums.UpdateType</c>
/// member into the <c>PolyBot.Attributes</c> namespace. Compiled into the PolyBot
/// library itself.
/// </summary>
internal static class HandlerAttributesEmitter
{
    public static string? Generate(Compilation compilation)
    {
        INamedTypeSymbol? updateTypeEnum = compilation.GetTypeByMetadataName("Telegram.Bot.Types.Enums.UpdateType");
        INamedTypeSymbol? updateClass = compilation.GetTypeByMetadataName("Telegram.Bot.Types.Update");
        if (updateTypeEnum is null || updateClass is null)
        {
            return null;
        }

        List<MemberDeclarationSyntax> classes = new();
        foreach (ISymbol member in updateTypeEnum.GetMembers())
        {
            if (member is not IFieldSymbol { HasConstantValue: true } field || field.Name == "Unknown")
            {
                continue;
            }

            string propertyName = UpdateShape.FindUpdatePropertyName(updateClass, field.Name);
            classes.Add(BuildAttributeClass(field.Name, propertyName));
        }

        if (classes.Count == 0)
        {
            return null;
        }

        return EmitterSyntax.RenderPolyBotFile("PolyBot.Attributes", classes.ToArray());
    }

    private static ClassDeclarationSyntax BuildAttributeClass(string fieldName, string propertyName)
    {
        AttributeSyntax usageAttribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName("global::System.AttributeUsage"))
            .AddArgumentListArguments(
                SyntaxFactory.AttributeArgument(SyntaxFactory.ParseExpression("global::System.AttributeTargets.Method")),
                SyntaxFactory.AttributeArgument(
                    nameColon: null,
                    nameEquals: SyntaxFactory.NameEquals("AllowMultiple"),
                    expression: SyntaxFactory.ParseExpression("false")));

        AttributeSyntax descriptorAttribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName("global::PolyBot.Attributes.UpdateHandlerDescriptor"))
            .AddArgumentListArguments(
                SyntaxFactory.AttributeArgument(SyntaxFactory.ParseExpression($"global::Telegram.Bot.Types.Enums.UpdateType.{fieldName}")),
                SyntaxFactory.AttributeArgument(SyntaxFactory.ParseExpression($"nameof(global::Telegram.Bot.Types.Update.{propertyName})")));

        return SyntaxFactory.ClassDeclaration(fieldName + "HandlerAttribute")
            .AddAttributeLists(
                SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(usageAttribute)),
                SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(descriptorAttribute)))
            .AddModifiers(SyntaxKind.PublicKeyword, SyntaxKind.SealedKeyword)
            .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("global::PolyBot.Attributes.HandlerAttribute")))
            .WithLeadingTrivia(
                SyntaxFactory.Comment($"/// <summary>Marks a handler for <c>UpdateType.{fieldName}</c> updates.</summary>"),
                SyntaxFactory.LineFeed);
    }
}
