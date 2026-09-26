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
                methods.AddRange(BuildWithFilterMethods(filter, dtoFqn, generic: false));
            }
            else
            {
                // Implements IUpdateFilter directly (no generated DTO base): generic form.
                methods.AddRange(BuildWithFilterMethods(filter, dtoFqn: null, generic: true));
            }
        }

        ClassDeclarationSyntax extensionsClass = SyntaxFactory.ClassDeclaration("PolyBotAwaiterExtensions")
            .AddModifiers(SyntaxKind.PublicKeyword, SyntaxKind.StaticKeyword, SyntaxKind.PartialKeyword)
            .AddMembers(methods.ToArray())
            .WithLeadingTrivia(EmitterSyntax.DocComment(
                "<summary>Generated <c>With*</c> filter compositions for conversational awaits, one per discovered concrete filter.</summary>"));

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

    private static List<MethodDeclarationSyntax> BuildWithFilterMethods(FilterClassModel filter, string? dtoFqn, bool generic)
    {
        List<MethodDeclarationSyntax> methods = new();
        if (filter.HasParameterlessUsage)
        {
            methods.Add(BuildWithFilterMethod(filter, dtoFqn, generic, parameters: null));
        }

        foreach (FilterCtorModel ctor in filter.Ctors.Items)
        {
            if (ctor.Params.Count == 0)
            {
                continue; // Equivalent to the parameterless form.
            }

            methods.Add(BuildWithFilterMethod(filter, dtoFqn, generic, ctor.Params.Items));
        }

        return methods;
    }

    private static MethodDeclarationSyntax BuildWithFilterMethod(FilterClassModel filter, string? dtoFqn, bool generic, IReadOnlyList<FilterParamModel>? parameters)
    {
        string builderType = generic
            ? "global::PolyBot.Awaits.UpdateAwaiterBuilder<TUpdate>"
            : $"global::PolyBot.Awaits.UpdateAwaiterBuilder<{dtoFqn}>";

        string construction = parameters is null
            ? $"builder.WithFilter<{filter.TypeFqn}>()"
            : $"builder.WithFilter(new {filter.TypeFqn}({string.Join(", ", parameters.Select(static p => p.Name))}))";

        MethodDeclarationSyntax method = SyntaxFactory.MethodDeclaration(
                returnType: SyntaxFactory.ParseTypeName(builderType),
                identifier: $"With{filter.ShortName}")
            .AddModifiers(SyntaxKind.PublicKeyword, SyntaxKind.StaticKeyword)
            .AddParameterListParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("builder"))
                    .AddModifiers(SyntaxKind.ThisKeyword)
                    .WithType(SyntaxFactory.ParseTypeName(builderType)))
            .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(
                SyntaxFactory.ParseExpression(construction)))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
            .WithLeadingTrivia(EmitterSyntax.DocComment(
                "<summary>",
                parameters is null
                    ? $"AND-composes the <c>{filter.ShortName}</c> filter into the await."
                    : $"AND-composes the <c>{filter.ShortName}</c> filter into the await, constructing it with the given arguments.",
                "</summary>"));

        if (parameters is not null)
        {
            foreach (FilterParamModel parameter in parameters)
            {
                ParameterSyntax parameterSyntax = SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameter.Name))
                    .WithType(SyntaxFactory.ParseTypeName(parameter.TypeFqn));
                if (parameter.IsOptional)
                {
                    parameterSyntax = parameterSyntax.WithDefault(
                        SyntaxFactory.EqualsValueClause(SyntaxFactory.ParseExpression(parameter.DefaultLiteral ?? "null")));
                }

                method = method.AddParameterListParameters(parameterSyntax);
            }
        }

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
