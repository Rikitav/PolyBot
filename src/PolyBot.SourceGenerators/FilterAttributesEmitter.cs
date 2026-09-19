using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PolyBot.SourceGenerators;

/// <summary>
/// Emits one wrapper attribute per discovered filter class; on short-name ambiguity the first discovered filter wins.
/// </summary>
internal static class FilterAttributesEmitter
{
    public static string? Generate(EquatableArray<FilterClassModel> filterClasses)
    {
        if (filterClasses.Count == 0)
        {
            return null;
        }

        List<MemberDeclarationSyntax> classes = new();
        HashSet<string> emittedNames = new(StringComparer.Ordinal);
        foreach (FilterClassModel filter in filterClasses.Items)
        {
            if (!emittedNames.Add(filter.ShortName))
            {
                continue;
            }

            classes.Add(BuildWrapperClass(filter));
        }

        if (classes.Count == 0)
        {
            return null;
        }

        return EmitterSyntax.RenderPolyBotFile("PolyBot.Attributes", classes.ToArray());
    }

    private static ClassDeclarationSyntax BuildWrapperClass(FilterClassModel filter)
    {
        string attributeName = filter.ShortName + "Attribute";

        AttributeSyntax usageAttribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName("global::System.AttributeUsage"))
            .AddArgumentListArguments(
                SyntaxFactory.AttributeArgument(SyntaxFactory.ParseExpression("global::System.AttributeTargets.Method")),
                SyntaxFactory.AttributeArgument(
                    nameColon: null,
                    nameEquals: SyntaxFactory.NameEquals("AllowMultiple"),
                    expression: SyntaxFactory.ParseExpression("true")));

        ConstructorDeclarationSyntax constructor = SyntaxFactory.ConstructorDeclaration(attributeName)
            .AddModifiers(SyntaxKind.PublicKeyword)
            .WithBody(SyntaxFactory.Block());

        return SyntaxFactory.ClassDeclaration(attributeName)
            .AddAttributeLists(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(usageAttribute)))
            .AddModifiers(SyntaxKind.PublicKeyword, SyntaxKind.SealedKeyword)
            .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("global::System.Attribute")))
            .AddMembers(constructor)
            .WithLeadingTrivia(EmitterSyntax.DocComment(
                "<summary>",
                $"Marker attribute applying the <c>{filter.ShortName}</c> filter (see <c>{filter.TypeFqn}</c>) to a handler.",
                "</summary>"));
    }
}
