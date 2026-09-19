using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PolyBot.SourceGenerators;

/// <summary>
/// Emits the <c>With{FilterName}</c> await-chain extensions; the <c>WaitFor*</c> entry points are emitted by PolyBot.InternalsGenerators into the PolyBot assembly itself.
/// </summary>
internal static class AwaiterExtensionsEmitter
{
    public static string? Generate(Compilation compilation, EquatableArray<FilterClassModel> filterClasses)
    {
        if (filterClasses.Count == 0)
        {
            return null;
        }

        INamedTypeSymbol? updateClass = compilation.GetTypeByMetadataName("Telegram.Bot.Types.Update");
        INamedTypeSymbol? updateTypeEnum = compilation.GetTypeByMetadataName("Telegram.Bot.Types.Enums.UpdateType");
        if (updateClass is null || updateTypeEnum is null)
        {
            return null;
        }

        List<UpdateShape.FilterGroup> groups = UpdateShape.BuildGroups(updateClass, updateTypeEnum);

        List<MemberDeclarationSyntax> methods = new();
        Dictionary<string, string> baseNameToDto = BuildBaseNameToDtoMap(groups);
        foreach (FilterClassModel filter in filterClasses.Items)
        {
            if (filter.DtoBaseName is not null && baseNameToDto.TryGetValue(filter.DtoBaseName, out string? dtoFqn))
            {
                methods.Add(BuildWithFilterMethod(filter, dtoFqn, generic: false));
            }
            else
            {
                // Implements IUpdateFilter directly (no generated DTO base): generic form.
                methods.Add(BuildWithFilterMethod(filter, dtoFqn: null, generic: true));
            }
        }

        ClassDeclarationSyntax extensionsClass = SyntaxFactory.ClassDeclaration("PolyBotAwaiterExtensions")
            .WithLeadingTrivia(EmitterSyntax.DocComment(
                "<summary>Generated <c>With*</c> filter compositions for conversational awaits, one per discovered concrete filter.</summary>"))
            .AddModifiers(SyntaxKind.PublicKeyword, SyntaxKind.StaticKeyword)
            .AddMembers(methods.ToArray());

        return EmitterSyntax.RenderPolyBotFile("PolyBot.Attributes", extensionsClass);
    }

    private static Dictionary<string, string> BuildBaseNameToDtoMap(List<UpdateShape.FilterGroup> groups)
    {
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        foreach (UpdateShape.FilterGroup group in groups)
        {
            string dtoFqn = group.DtoType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            map[UpdateShape.GroupBaseName(group)] = dtoFqn;
            for (int i = 1; i < group.Mappings.Count; i++)
            {
                map[group.Mappings[i].MemberName + "Filter"] = dtoFqn;
            }
        }

        return map;
    }

    private static MethodDeclarationSyntax BuildWithFilterMethod(FilterClassModel filter, string? dtoFqn, bool generic)
    {
        string builderType = generic
            ? "global::PolyBot.Awaits.UpdateAwaiterBuilder<TUpdate>"
            : $"global::PolyBot.Awaits.UpdateAwaiterBuilder<{dtoFqn}>";

        MethodDeclarationSyntax method = SyntaxFactory.MethodDeclaration(
                returnType: SyntaxFactory.ParseTypeName(builderType),
                identifier: $"With{filter.ShortName}")
            .AddModifiers(SyntaxKind.PublicKeyword, SyntaxKind.StaticKeyword)
            .AddParameterListParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("builder"))
                    .AddModifiers(SyntaxKind.ThisKeyword)
                    .WithType(SyntaxFactory.ParseTypeName(builderType)))
            .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(
                SyntaxFactory.ParseExpression($"builder.WithFilter<{filter.TypeFqn}>()")))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
            .WithLeadingTrivia(EmitterSyntax.DocComment(
                "<summary>",
                $"AND-composes the <c>{filter.ShortName}</c> filter into the await.",
                "</summary>"));

        if (generic)
        {
            method = method
                .AddTypeParameterListParameters(SyntaxFactory.TypeParameter("TUpdate"))
                .AddConstraintClauses(
                    SyntaxFactory.TypeParameterConstraintClause("TUpdate")
                        .AddConstraints(SyntaxFactory.ClassOrStructConstraint(SyntaxKind.ClassConstraint)));
        }

        return method;
    }
}
