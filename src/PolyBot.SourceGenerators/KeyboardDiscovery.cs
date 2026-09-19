using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PolyBot.SourceGenerators;

internal sealed class KeyboardButtonModel : IEquatable<KeyboardButtonModel>
{
    public required string Text { get; init; }

    public required string CallbackData { get; init; }

    public bool Equals(KeyboardButtonModel? other)
    {
        return other is not null && Text == other.Text && CallbackData == other.CallbackData;
    }

    public override bool Equals(object? obj) => Equals(obj as KeyboardButtonModel);

    public override int GetHashCode() => unchecked((Text.GetHashCode() * 31) + CallbackData.GetHashCode());
}

internal sealed class KeyboardModel : IEquatable<KeyboardModel>
{
    public required string? Namespace { get; init; }

    public required EquatableArray<string> ContainingTypeDeclarations { get; init; }

    public required string MemberModifiers { get; init; }

    public required string MemberName { get; init; }

    public required string ParametersSource { get; init; }

    public required bool IsProperty { get; init; }

    public required EquatableArray<string> ParameterNames { get; init; }

    public required EquatableArray<EquatableArray<KeyboardButtonModel>> Rows { get; init; }

    public bool Equals(KeyboardModel? other)
    {
        return other is not null
            && Namespace == other.Namespace
            && ContainingTypeDeclarations.Equals(other.ContainingTypeDeclarations)
            && MemberModifiers == other.MemberModifiers
            && MemberName == other.MemberName
            && ParametersSource == other.ParametersSource
            && IsProperty == other.IsProperty
            && ParameterNames.Equals(other.ParameterNames)
            && Rows.Equals(other.Rows);
    }

    public override bool Equals(object? obj) => Equals(obj as KeyboardModel);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + (Namespace?.GetHashCode() ?? 0);
            hash = (hash * 31) + ContainingTypeDeclarations.GetHashCode();
            hash = (hash * 31) + MemberModifiers.GetHashCode();
            hash = (hash * 31) + MemberName.GetHashCode();
            hash = (hash * 31) + ParametersSource.GetHashCode();
            hash = (hash * 31) + IsProperty.GetHashCode();
            hash = (hash * 31) + ParameterNames.GetHashCode();
            hash = (hash * 31) + Rows.GetHashCode();
            return hash;
        }
    }
}

/// <summary>
/// One candidate's discovery result: the model plus any diagnostics.
/// </summary>
internal sealed class KeyboardItem
{
    public KeyboardModel? Model { get; init; }

    public List<Diagnostic> Diagnostics { get; init; } = new();
}

/// <summary>
/// Discovers <c>partial</c> methods and properties annotated with
/// <c>PolyBot.CallbackButtonAttribute</c>; each attribute list forms one keyboard row.
/// </summary>
internal static class KeyboardDiscovery
{
    private const string AttributeShortName = "CallbackButton";

    private const string InlineKeyboardMarkupFqn = "Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup";

    public static KeyboardItem Discover(GeneratorSyntaxContext context)
    {
        List<Diagnostic> diagnostics = new();
        if (context.Node is MethodDeclarationSyntax method)
        {
            return DiscoverMethod(method, context.SemanticModel, diagnostics);
        }

        if (context.Node is PropertyDeclarationSyntax property)
        {
            return DiscoverProperty(property, context.SemanticModel, diagnostics);
        }

        return new KeyboardItem();
    }

    private static KeyboardItem NotPartial(SyntaxNode node, SyntaxToken identifier, List<Diagnostic> diagnostics)
    {
        diagnostics.Add(Diagnostic.Create(
            PolyBotDiagnostics.KeyboardMemberNotPartial,
            node.GetLocation(),
            identifier.ValueText));
        return new KeyboardItem { Diagnostics = diagnostics };
    }

    private static KeyboardItem DiscoverMethod(MethodDeclarationSyntax method, SemanticModel semanticModel, List<Diagnostic> diagnostics)
    {
        if (!HasCallbackButtonName(method.AttributeLists))
        {
            return new KeyboardItem();
        }

        if (!method.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            return NotPartial(method, method.Identifier, diagnostics);
        }

        string memberName = method.Identifier.ValueText;
        if (!IsInlineKeyboardMarkup(method.ReturnType, semanticModel))
        {
            diagnostics.Add(Diagnostic.Create(
                PolyBotDiagnostics.WrongKeyboardReturnType,
                method.GetLocation(),
                memberName,
                method.ReturnType.ToString()));
            return new KeyboardItem { Diagnostics = diagnostics };
        }

        if (method.TypeParameterList is not null)
        {
            diagnostics.Add(Diagnostic.Create(
                PolyBotDiagnostics.KeyboardMemberNotPartial,
                method.GetLocation(),
                memberName + " (generic methods are not supported)"));
            return new KeyboardItem { Diagnostics = diagnostics };
        }

        List<string> parameterNames = new();
        foreach (ParameterSyntax parameter in method.ParameterList.Parameters)
        {
            parameterNames.Add(parameter.Identifier.ValueText);
        }

        KeyboardModel model = BuildModel(
            method.AttributeLists,
            semanticModel,
            memberName,
            method.Modifiers.ToString(),
            method.ParameterList.ToString(),
            isProperty: false,
            parameterNames,
            diagnostics);
        return new KeyboardItem { Model = model, Diagnostics = diagnostics };
    }

    private static KeyboardItem DiscoverProperty(PropertyDeclarationSyntax property, SemanticModel semanticModel, List<Diagnostic> diagnostics)
    {
        if (!HasCallbackButtonName(property.AttributeLists))
        {
            return new KeyboardItem();
        }

        if (!property.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            return NotPartial(property, property.Identifier, diagnostics);
        }

        string memberName = property.Identifier.ValueText;
        if (!IsInlineKeyboardMarkup(property.Type, semanticModel))
        {
            diagnostics.Add(Diagnostic.Create(
                PolyBotDiagnostics.WrongKeyboardReturnType,
                property.GetLocation(),
                memberName,
                property.Type.ToString()));
            return new KeyboardItem { Diagnostics = diagnostics };
        }

        KeyboardModel model = BuildModel(
            property.AttributeLists,
            semanticModel,
            memberName,
            property.Modifiers.ToString(),
            parametersSource: string.Empty,
            isProperty: true,
            new List<string>(),
            diagnostics);
        return new KeyboardItem { Model = model, Diagnostics = diagnostics };
    }

    private static KeyboardModel BuildModel(
        SyntaxList<AttributeListSyntax> attributeLists,
        SemanticModel semanticModel,
        string memberName,
        string memberModifiers,
        string parametersSource,
        bool isProperty,
        List<string> parameterNames,
        List<Diagnostic> diagnostics)
    {
        List<EquatableArray<KeyboardButtonModel>> rows = new();
        foreach (AttributeListSyntax attributeList in attributeLists)
        {
            List<KeyboardButtonModel> row = new();
            foreach (AttributeSyntax attribute in attributeList.Attributes)
            {
                if (!IsCallbackButtonAttribute(attribute, semanticModel))
                {
                    continue;
                }

                if (attribute.ArgumentList?.Arguments.Count != 2 ||
                    semanticModel.GetConstantValue(attribute.ArgumentList.Arguments[0].Expression) is not { HasValue: true, Value: string text } ||
                    semanticModel.GetConstantValue(attribute.ArgumentList.Arguments[1].Expression) is not { HasValue: true, Value: string callbackData })
                {
                    continue;
                }

                foreach (string placeholder in ExtractPlaceholders(callbackData))
                {
                    if (!parameterNames.Contains(placeholder))
                    {
                        diagnostics.Add(Diagnostic.Create(
                            PolyBotDiagnostics.UnknownKeyboardPlaceholder,
                            attribute.GetLocation(),
                            placeholder,
                            memberName));
                    }
                }

                row.Add(new KeyboardButtonModel { Text = text, CallbackData = callbackData });
            }

            if (row.Count > 0)
            {
                rows.Add(new EquatableArray<KeyboardButtonModel>(row.ToArray()));
            }
        }

        AttributeSyntax? anchor = attributeLists.SelectMany(list => list.Attributes).FirstOrDefault();
        return new KeyboardModel
        {
            Namespace = anchor is null ? null : ResolveNamespace(anchor),
            ContainingTypeDeclarations = anchor is null ? new EquatableArray<string>(Array.Empty<string>()) : ResolveContainingTypes(anchor),
            MemberModifiers = memberModifiers,
            MemberName = memberName,
            ParametersSource = parametersSource,
            IsProperty = isProperty,
            ParameterNames = new EquatableArray<string>(parameterNames.ToArray()),
            Rows = new EquatableArray<EquatableArray<KeyboardButtonModel>>(rows.ToArray()),
        };
    }

    private static bool HasCallbackButtonName(SyntaxList<AttributeListSyntax> attributeLists)
    {
        foreach (AttributeSyntax attribute in attributeLists.SelectMany(list => list.Attributes))
        {
            if (IsCallbackButtonName(attribute.Name.ToString()))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCallbackButtonName(string name)
    {
        return name == AttributeShortName ||
               name == AttributeShortName + "Attribute" ||
               name.EndsWith("." + AttributeShortName, StringComparison.Ordinal) ||
               name.EndsWith("." + AttributeShortName + "Attribute", StringComparison.Ordinal);
    }

    private static bool IsCallbackButtonAttribute(AttributeSyntax attribute, SemanticModel semanticModel)
    {
        if (!IsCallbackButtonName(attribute.Name.ToString()))
        {
            return false;
        }

        // When the symbol resolved, verify it is PolyBot.CallbackButtonAttribute.
        if (semanticModel.GetSymbolInfo(attribute).Symbol is IMethodSymbol constructor)
        {
            return constructor.ContainingType.ToDisplayString() == "PolyBot.Attributes.CallbackButtonAttribute";
        }

        // Error symbol (e.g. missing using): the name match above stands.
        return true;
    }

    private static bool IsInlineKeyboardMarkup(TypeSyntax returnType, SemanticModel semanticModel)
    {
        if (semanticModel.GetTypeInfo(returnType).Type is INamedTypeSymbol typeSymbol)
        {
            return typeSymbol.ToDisplayString() == InlineKeyboardMarkupFqn;
        }

        // Error symbol: accept the fully-qualified and short spellings.
        string text = returnType.ToString();
        return text is "InlineKeyboardMarkup"
            or "Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup"
            or "global::Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup";
    }

    private static IEnumerable<string> ExtractPlaceholders(string callbackData)
    {
        int index = 0;
        while (index < callbackData.Length)
        {
            int open = callbackData.IndexOf('{', index);
            if (open < 0)
            {
                yield break;
            }

            if (open + 1 < callbackData.Length && callbackData[open + 1] == '{')
            {
                index = open + 2;
                continue;
            }

            int close = callbackData.IndexOf('}', open + 1);
            if (close < 0)
            {
                yield break;
            }

            string inner = callbackData.Substring(open + 1, close - open - 1);
            int cut = inner.IndexOfAny(PlaceholderStopChars);
            yield return cut < 0 ? inner : inner.Substring(0, cut);
            index = close + 1;
        }
    }

    private static readonly char[] PlaceholderStopChars = { ':', ',' };

    private static string? ResolveNamespace(SyntaxNode node)
    {
        SyntaxNode? current = node.Parent;
        while (current is not null)
        {
            if (current is FileScopedNamespaceDeclarationSyntax fileScoped)
            {
                return fileScoped.Name.ToString();
            }

            if (current is NamespaceDeclarationSyntax regular)
            {
                return regular.Name.ToString();
            }

            current = current.Parent;
        }

        return null;
    }

    private static EquatableArray<string> ResolveContainingTypes(SyntaxNode node)
    {
        List<string> declarations = new();
        SyntaxNode? current = node.Parent;
        while (current is not null)
        {
            if (current is TypeDeclarationSyntax typeDeclaration)
            {
                string modifiers = typeDeclaration.Modifiers.ToString();
                if (!modifiers.Contains("partial"))
                {
                    modifiers = string.IsNullOrEmpty(modifiers) ? "partial" : modifiers + " partial";
                }

                string recordKeyword = typeDeclaration is RecordDeclarationSyntax recordDeclaration ? "record " + recordDeclaration.Keyword.ValueText : typeDeclaration.Keyword.ValueText;
                declarations.Add(modifiers + " " + recordKeyword + " " + typeDeclaration.Identifier.ValueText);
            }

            current = current.Parent;
        }

        declarations.Reverse();
        return new EquatableArray<string>(declarations.ToArray());
    }
}
