using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PolyBot.SourceGenerators;

namespace PolyBot.InternalsGenerators;

/// <summary>
/// Generates the per-payload <c>XxxFilter</c> abstract classes (namespace <c>PolyBot</c>)
/// from the referenced Telegram.Bot <c>Update</c> class. Compiled into the PolyBot
/// library itself.
/// </summary>
internal static class UpdateFiltersEmitter
{
    public static string? Generate(Compilation compilation)
    {
        INamedTypeSymbol? updateClass = compilation.GetTypeByMetadataName("Telegram.Bot.Types.Update");
        INamedTypeSymbol? updateTypeEnum = compilation.GetTypeByMetadataName("Telegram.Bot.Types.Enums.UpdateType");
        if (updateClass is null || updateTypeEnum is null)
        {
            return null;
        }

        List<UpdateShape.FilterGroup> groups = UpdateShape.BuildGroups(updateClass, updateTypeEnum);
        if (groups.Count == 0)
        {
            return null;
        }

        List<MemberDeclarationSyntax> classes = new();
        foreach (UpdateShape.FilterGroup group in groups)
        {
            string baseName = UpdateShape.GroupBaseName(group);
            classes.Add(BuildBaseFilterClass(group, baseName));
            for (int i = 1; i < group.Mappings.Count; i++)
            {
                classes.Add(BuildNarrowingFilterClass(group, baseName, i));
            }
        }

        return EmitterSyntax.RenderPolyBotFile("PolyBot", classes.ToArray());
    }

    private static ClassDeclarationSyntax BuildBaseFilterClass(UpdateShape.FilterGroup group, string className)
    {
        string dtoFqn = group.DtoType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string dtoName = group.DtoType.Name;
        string updateFqn = "global::Telegram.Bot.Types.Update";

        List<string> extractionChain = group.ExtractionChain;

        MethodDeclarationSyntax canPassUpdate = SyntaxFactory.MethodDeclaration(
                returnType: SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.BoolKeyword)),
                identifier: "CanPass")
            .AddModifiers(SyntaxKind.PublicKeyword)
            .AddParameterListParameters(EmitterSyntax.BuildParameter("update", updateFqn))
            .WithBody(SyntaxFactory.Block(
                SyntaxFactory.LocalDeclarationStatement(
                    SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName(dtoFqn + "?"))
                        .AddVariables(SyntaxFactory.VariableDeclarator("payload")
                            .WithInitializer(SyntaxFactory.EqualsValueClause(SyntaxFactory.ParseExpression("Extract(update)"))))),
                SyntaxFactory.ReturnStatement(SyntaxFactory.ParseExpression("payload is not null && CanPass(payload)"))))
            .WithLeadingTrivia(EmitterSyntax.DocComment(
                "<summary>",
                $"Extracts the update's <c>{dtoName}</c> payload (first match wins) and tests it.",
                "</summary>"));

        MethodDeclarationSyntax extract = SyntaxFactory.MethodDeclaration(
                returnType: SyntaxFactory.ParseTypeName(dtoFqn + "?"),
                identifier: "Extract")
            .AddModifiers(SyntaxKind.ProtectedKeyword, SyntaxKind.VirtualKeyword)
            .AddParameterListParameters(EmitterSyntax.BuildParameter("update", updateFqn))
            .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(
                SyntaxFactory.ParseExpression(string.Join(" ?? ", extractionChain.Select(p => "update." + p)))))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
            .WithLeadingTrivia(EmitterSyntax.DocComment(
                "<summary>",
                $"Extracts the first <c>{dtoName}</c> payload, probing in Update property order: {string.Join(", ", extractionChain)}.",
                "</summary>"));

        MethodDeclarationSyntax canPassPayload = SyntaxFactory.MethodDeclaration(
                returnType: SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.BoolKeyword)),
                identifier: "CanPass")
            .AddModifiers(SyntaxKind.ProtectedKeyword, SyntaxKind.AbstractKeyword)
            .AddParameterListParameters(EmitterSyntax.BuildParameter(CamelCase(dtoName), dtoFqn))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
            .WithLeadingTrivia(EmitterSyntax.DocComment(
                "<summary>",
                $"Tests the extracted <c>{dtoName}</c> payload. Covers update type(s): {string.Join(", ", group.Mappings.Select(m => m.MemberName))}.",
                "</summary>"));

        string covered = string.Join(", ", group.Mappings.Select(m => m.MemberName));
        return SyntaxFactory.ClassDeclaration(className)
            .AddModifiers(SyntaxKind.PublicKeyword, SyntaxKind.AbstractKeyword)
            .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("global::PolyBot.Routing.IUpdateFilter")))
            .AddMembers(canPassUpdate)
            .AddMembers(extract)
            .AddMembers(canPassPayload)
            .WithLeadingTrivia(EmitterSyntax.DocComment(
                "<summary>",
                $"Base filter for updates carrying a <c>{dtoName}</c> payload. Covers update type(s): {covered}.",
                "</summary>"));
    }

    private static ClassDeclarationSyntax BuildNarrowingFilterClass(UpdateShape.FilterGroup group, string baseName, int mappingIndex)
    {
        string dtoFqn = group.DtoType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        (string memberName, string propertyName) = group.Mappings[mappingIndex];
        string updateFqn = "global::Telegram.Bot.Types.Update";

        MethodDeclarationSyntax extract = SyntaxFactory.MethodDeclaration(
                returnType: SyntaxFactory.ParseTypeName(dtoFqn + "?"),
                identifier: "Extract")
            .AddModifiers(SyntaxKind.ProtectedKeyword, SyntaxKind.OverrideKeyword)
            .AddParameterListParameters(EmitterSyntax.BuildParameter("update", updateFqn))
            .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(
                SyntaxFactory.ParseExpression($"update.{propertyName}")))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
            .WithLeadingTrivia(EmitterSyntax.DocComment(
                "<summary>",
                $"Narrows extraction to <c>update.{propertyName}</c> only.",
                "</summary>"));

        return SyntaxFactory.ClassDeclaration(memberName + "Filter")
            .AddModifiers(SyntaxKind.PublicKeyword, SyntaxKind.AbstractKeyword)
            .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName($"global::PolyBot.{baseName}")))
            .AddMembers(extract)
            .WithLeadingTrivia(EmitterSyntax.DocComment(
                "<summary>",
                $"Narrows <c>{baseName}</c> to <c>UpdateType.{memberName}</c> updates only (extraction: <c>update.{propertyName}</c>).",
                "</summary>"));
    }

    private static string CamelCase(string value)
    {
        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }
}
