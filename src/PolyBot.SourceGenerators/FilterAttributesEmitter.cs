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

        List<MemberDeclarationSyntax> members = new();

        if (filter.HasParameterlessUsage)
        {
            members.Add(BuildConstructor(attributeName, parameters: null));
        }

        foreach (FilterCtorModel ctor in filter.Ctors.Items)
        {
            if (ctor.Params.Count == 0)
            {
                continue; // Equivalent to the parameterless form.
            }

            members.Add(BuildConstructor(attributeName, ctor.Params.Items));
        }

        // Settable properties enable attribute named arguments (Name = value), e.g. to set a
        // middle optional parameter by name. One property per distinct parameter name.
        HashSet<string> propertyNames = new(StringComparer.Ordinal);
        foreach (FilterCtorModel ctor in filter.Ctors.Items)
        {
            foreach (FilterParamModel parameter in ctor.Params.Items)
            {
                if (propertyNames.Add(parameter.PropertyName))
                {
                    members.Add(BuildProperty(parameter));
                }
            }
        }

        return SyntaxFactory.ClassDeclaration(attributeName)
            .AddAttributeLists(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(usageAttribute)))
            .AddModifiers(SyntaxKind.PublicKeyword, SyntaxKind.SealedKeyword)
            .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("global::System.Attribute")))
            .AddMembers(members.ToArray())
            .WithLeadingTrivia(EmitterSyntax.DocComment(
                "<summary>",
                $"Marker attribute applying the <c>{filter.ShortName}</c> filter (see <c>{filter.TypeFqn}</c>) to a handler.",
                "</summary>"));
    }

    private static ConstructorDeclarationSyntax BuildConstructor(string attributeName, IReadOnlyList<FilterParamModel>? parameters)
    {
        ConstructorDeclarationSyntax constructor = SyntaxFactory.ConstructorDeclaration(attributeName)
            .AddModifiers(SyntaxKind.PublicKeyword)
            .WithLeadingTrivia(EmitterSyntax.DocComment(
                "<summary>",
                $"Initializes a new instance of the <see cref=\"{attributeName}\"/> class.",
                "</summary>"));

        if (parameters is null)
        {
            return constructor.WithBody(SyntaxFactory.Block());
        }

        List<StatementSyntax> assignments = new();
        foreach (FilterParamModel parameter in parameters)
        {
            ParameterSyntax parameterSyntax = SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameter.Name))
                .WithType(SyntaxFactory.ParseTypeName(parameter.TypeFqn));
            if (parameter.IsOptional)
            {
                parameterSyntax = parameterSyntax.WithDefault(
                    SyntaxFactory.EqualsValueClause(SyntaxFactory.ParseExpression(parameter.DefaultLiteral ?? "null")));
            }

            constructor = constructor.AddParameterListParameters(parameterSyntax);
            assignments.Add(SyntaxFactory.ParseStatement($"{parameter.PropertyName} = {parameter.Name};"));
        }

        return constructor.WithBody(SyntaxFactory.Block(assignments));
    }

    private static PropertyDeclarationSyntax BuildProperty(FilterParamModel parameter)
    {
        return SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName(parameter.TypeFqn), parameter.PropertyName)
            .AddModifiers(SyntaxKind.PublicKeyword)
            .AddAccessorListAccessors(
                SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)))
            .WithLeadingTrivia(EmitterSyntax.DocComment(
                "<summary>",
                $"Gets or sets the <c>{parameter.Name}</c> argument passed to the filter's constructor.",
                "</summary>"));
    }
}
