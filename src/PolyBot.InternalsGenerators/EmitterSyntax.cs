using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PolyBot.SourceGenerators;

/// <summary>
/// Syntax-building helpers shared by both generator projects. Linked from
/// PolyBot.InternalsGenerators into PolyBot.SourceGenerators.
/// </summary>
internal static class EmitterSyntax
{
    /// <summary>
    /// Builds <c>///</c> doc-comment trivia, one trivia per line.
    /// </summary>
    public static SyntaxTriviaList DocComment(params string[] lines)
    {
        List<SyntaxTrivia> trivia = new();
        foreach (string line in lines)
        {
            trivia.Add(SyntaxFactory.Comment("/// " + line));
            trivia.Add(SyntaxFactory.LineFeed);
        }

        return SyntaxFactory.TriviaList(trivia);
    }

    public static ParameterSyntax BuildParameter(string name, string typeName)
    {
        return SyntaxFactory.Parameter(SyntaxFactory.Identifier(name))
            .WithType(SyntaxFactory.ParseTypeName(typeName));
    }

    public static SyntaxToken[] Tokens(params SyntaxKind[] kinds)
    {
        SyntaxToken[] tokens = new SyntaxToken[kinds.Length];
        for (int i = 0; i < kinds.Length; i++)
        {
            tokens[i] = SyntaxFactory.Token(kinds[i]);
        }

        return tokens;
    }

    public static ClassDeclarationSyntax AddModifiers(this ClassDeclarationSyntax node, params SyntaxKind[] kinds)
    {
        return node.AddModifiers(Tokens(kinds));
    }

    public static StructDeclarationSyntax AddModifiers(this StructDeclarationSyntax node, params SyntaxKind[] kinds)
    {
        return node.AddModifiers(Tokens(kinds));
    }

    public static MethodDeclarationSyntax AddModifiers(this MethodDeclarationSyntax node, params SyntaxKind[] kinds)
    {
        return node.AddModifiers(Tokens(kinds));
    }

    public static PropertyDeclarationSyntax AddModifiers(this PropertyDeclarationSyntax node, params SyntaxKind[] kinds)
    {
        return node.AddModifiers(Tokens(kinds));
    }

    public static ConstructorDeclarationSyntax AddModifiers(this ConstructorDeclarationSyntax node, params SyntaxKind[] kinds)
    {
        return node.AddModifiers(Tokens(kinds));
    }

    public static FieldDeclarationSyntax AddModifiers(this FieldDeclarationSyntax node, params SyntaxKind[] kinds)
    {
        return node.AddModifiers(Tokens(kinds));
    }

    public static ParameterSyntax AddModifiers(this ParameterSyntax node, params SyntaxKind[] kinds)
    {
        return node.AddModifiers(Tokens(kinds));
    }

    public static string RenderPolyBotFile(string namespaceName, params MemberDeclarationSyntax[] members)
    {
        CompilationUnitSyntax compilationUnit = SyntaxFactory.CompilationUnit()
            .AddMembers(SyntaxFactory.FileScopedNamespaceDeclaration(SyntaxFactory.ParseName(namespaceName))
                .AddMembers(members));

        return GeneratedSource.Render(compilationUnit);
    }

    public static StatementSyntax BuildLocalDeclaration(string typeName, string variableName, string expression)
    {
        return SyntaxFactory.LocalDeclarationStatement(
            SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName(typeName))
                .AddVariables(SyntaxFactory.VariableDeclarator(variableName)
                    .WithInitializer(SyntaxFactory.EqualsValueClause(SyntaxFactory.ParseExpression(expression)))));
    }

    public static FieldDeclarationSyntax BuildStaticArrayField(string elementType, string name, string initializerSource)
    {
        VariableDeclarationSyntax declaration = SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName(elementType + "[]"))
            .AddVariables(SyntaxFactory.VariableDeclarator(name)
                .WithInitializer(SyntaxFactory.EqualsValueClause(SyntaxFactory.ParseExpression(initializerSource))));
        return SyntaxFactory.FieldDeclaration(declaration)
            .AddModifiers(SyntaxKind.PrivateKeyword, SyntaxKind.StaticKeyword, SyntaxKind.ReadOnlyKeyword);
    }

    public static string RequiredService(string typeFqn, string provider = "_services")
    {
        return $"global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<{typeFqn}>({provider})";
    }

    public static string RequiredKeyedService(string typeFqn, string keyLiteral, string provider = "_services")
    {
        return $"global::Microsoft.Extensions.DependencyInjection.ServiceProviderKeyedServiceExtensions.GetRequiredKeyedService<{typeFqn}>({provider}, {keyLiteral})";
    }

    /// <summary>
    /// Filter resolution: service collection first, then an <c>ActivatorUtilities</c>
    /// fallback so unregistered parameterless filters still resolve.
    /// </summary>
    public static string ResolveFilter(string filterTypeFqn, string provider = "_services")
    {
        return $"global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<{filterTypeFqn}>({provider}) ?? ({filterTypeFqn})global::Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance({provider}, typeof({filterTypeFqn}))";
    }

    /// <summary>
    /// Filter resolution with constructor arguments captured from the attribute/With* usage:
    /// explicit arguments bypass DI and construct the filter directly; without arguments the
    /// service collection is consulted first, then <c>ActivatorUtilities</c>.
    /// </summary>
    public static string ResolveFilterWithArgs(string filterTypeFqn, string? ctorArgs, string provider = "_services")
    {
        return ctorArgs is null
            ? ResolveFilter(filterTypeFqn, provider)
            : $"new {filterTypeFqn}({ctorArgs})";
    }
}
