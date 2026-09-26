using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PolyBot.SourceGenerators;

namespace PolyBot.InternalsGenerators;

/// <summary>
/// Generates the <c>WaitFor{UpdateType}Async</c> extension methods on
/// <c>IUpdateAwaiter</c> (namespace <c>PolyBot.Attributes</c>). Compiled into the
/// PolyBot library itself; the host generator adds the user-filter-dependent
/// <c>With*</c> overloads into the same class name.
/// </summary>
internal static class WaitForExtensionsEmitter
{
    public static string? Generate(Compilation compilation)
    {
        INamedTypeSymbol? updateClass = compilation.GetTypeByMetadataName("Telegram.Bot.Types.Update");
        INamedTypeSymbol? updateTypeEnum = compilation.GetTypeByMetadataName("Telegram.Bot.Types.Enums.UpdateType");
        if (updateClass is null || updateTypeEnum is null)
        {
            return null;
        }

        List<IPropertySymbol> payloadProperties = updateClass.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => !p.IsStatic && p.Type.IsReferenceType && p.Type.SpecialType != SpecialType.System_String)
            .ToList();

        List<MemberDeclarationSyntax> methods = new();
        foreach (ISymbol member in updateTypeEnum.GetMembers())
        {
            if (member is not IFieldSymbol { HasConstantValue: true } field || field.Name == "Unknown")
            {
                continue;
            }

            IPropertySymbol? property = UpdateShape.FindPayloadProperty(payloadProperties, field.Name);
            if (property is null)
            {
                continue;
            }

            methods.Add(BuildWaitForMethod(field.Name, property.Type));
        }

        if (methods.Count == 0)
        {
            return null;
        }

        ClassDeclarationSyntax extensionsClass = SyntaxFactory.ClassDeclaration("PolyBotAwaiterExtensions")
            .AddModifiers(SyntaxKind.PublicKeyword, SyntaxKind.StaticKeyword, SyntaxKind.PartialKeyword)
            .AddMembers(methods.ToArray())
            .WithLeadingTrivia(EmitterSyntax.DocComment(
                "<summary>",
                "One <c>WaitFor*</c> method per Telegram.Bot update type.",
                "</summary>"));

        return EmitterSyntax.RenderPolyBotFile("PolyBot.Attributes", extensionsClass);
    }

    private static MethodDeclarationSyntax BuildWaitForMethod(string memberName, ITypeSymbol dtoType)
    {
        string dtoFqn = dtoType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return SyntaxFactory.MethodDeclaration(
                returnType: SyntaxFactory.ParseTypeName($"global::PolyBot.Awaits.UpdateAwaiterBuilder<{dtoFqn}>"),
                identifier: $"WaitFor{memberName}Async")
            .AddModifiers(SyntaxKind.PublicKeyword, SyntaxKind.StaticKeyword)
            .AddParameterListParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("awaiter"))
                    .AddModifiers(SyntaxKind.ThisKeyword)
                    .WithType(SyntaxFactory.ParseTypeName("global::PolyBot.Awaits.IUpdateAwaiter")),
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("timeout"))
                    .WithType(SyntaxFactory.ParseTypeName("global::System.TimeSpan?"))
                    .WithDefault(SyntaxFactory.EqualsValueClause(SyntaxFactory.ParseExpression("null"))))
            .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(
                SyntaxFactory.ParseExpression($"awaiter.WaitForAsync<{dtoFqn}>(timeout)")))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
            .WithLeadingTrivia(EmitterSyntax.DocComment(
                "<summary>",
                $"Starts a conversational await for the update type <c>{memberName}</c>.",
                "</summary>"));
    }
}
