using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PolyBot.SourceGenerators;

/// <summary>
/// Evaluates filter argument expressions (attribute arguments, <c>With*</c> call arguments) to
/// C# literals for splicing into generated construction code. Only compile-time constants are
/// accepted; anything else fails so the caller can report CUR044.
/// </summary>
internal static class FilterArgEmitter
{
    private static readonly SymbolDisplayFormat FqnFormat = SymbolDisplayFormat.FullyQualifiedFormat;

    /// <summary>
    /// Tries to emit <paramref name="expression"/> as a C# literal assignable to
    /// <paramref name="expectedType"/>. Handles constants (including enum members and
    /// arithmetic/namof-style constant expressions via <see cref="SemanticModel.GetConstantValue"/>),
    /// <c>typeof(...)</c>, <c>default(...)</c>, and single-dimensional array/collection expressions.
    /// </summary>
    public static bool TryEmit(ExpressionSyntax expression, ITypeSymbol? expectedType, SemanticModel semanticModel, out string literal)
    {
        expression = StripParentheses(expression);

        if (expression is TypeOfExpressionSyntax typeofExpression)
        {
            if (semanticModel.GetTypeInfo(typeofExpression).Type is ITypeSymbol typeOf)
            {
                literal = $"typeof({typeOf.ToDisplayString(FqnFormat)})";
                return true;
            }

            literal = string.Empty;
            return false;
        }

        if (expression is DefaultExpressionSyntax defaultExpression)
        {
            if (semanticModel.GetTypeInfo(defaultExpression).Type is ITypeSymbol defaultType)
            {
                literal = $"default({defaultType.ToDisplayString(FqnFormat)})";
                return true;
            }

            literal = string.Empty;
            return false;
        }

        if (TryEmitArray(expression, expectedType, semanticModel, out literal))
        {
            return true;
        }

        // Constants: literals, enum members, const references, constant arithmetic.
        Optional<object?> constant = semanticModel.GetConstantValue(expression);
        if (constant.HasValue)
        {
            return TryEmitConstant(constant.Value, expectedType, out literal);
        }

        literal = string.Empty;
        return false;
    }

    private static bool TryEmitConstant(object? value, ITypeSymbol? expectedType, out string literal)
    {
        if (value is null)
        {
            literal = "null";
            return true;
        }

        if (expectedType is not null && expectedType.TypeKind == TypeKind.Enum && expectedType is INamedTypeSymbol enumType)
        {
            string? memberName = EnumValueToMemberName(enumType, value);
            if (memberName is not null)
            {
                literal = $"{enumType.ToDisplayString(FqnFormat)}.{memberName}";
                return true;
            }

            literal = string.Empty;
            return false;
        }

        if (value is string s)
        {
            literal = SymbolDisplay.FormatLiteral(s, quote: true);
            return true;
        }

        if (value is bool b)
        {
            literal = b ? "true" : "false";
            return true;
        }

        if (value is char c)
        {
            literal = SymbolDisplay.FormatLiteral(c, quote: true);
            return true;
        }

        if (value is IFormattable)
        {
            // Numeric and other IFormattable constants: suffix rules live in FilterCtorInfo.
            literal = FilterCtorInfo.EmitDefaultLiteral(value, expectedType!);
            return true;
        }

        literal = string.Empty;
        return false;
    }

    private static bool TryEmitArray(ExpressionSyntax expression, ITypeSymbol? expectedType, SemanticModel semanticModel, out string literal)
    {
        IEnumerable<ExpressionSyntax>? elements = expression switch
        {
            CollectionExpressionSyntax collection => collection.Elements.Select(e => e is ExpressionElementSyntax ee ? ee.Expression : null!),
            ArrayCreationExpressionSyntax { Initializer: not null } arrayCreation => arrayCreation.Initializer.Expressions,
            ImplicitArrayCreationExpressionSyntax implicitArray => implicitArray.Initializer.Expressions,
            _ => null,
        };

        if (elements is null)
        {
            literal = string.Empty;
            return false;
        }

        ITypeSymbol? elementType = expectedType is IArrayTypeSymbol expectedArray ? expectedArray.ElementType : null;
        List<string> parts = new();
        foreach (ExpressionSyntax element in elements)
        {
            if (element is null || !TryEmit(element, elementType, semanticModel, out string elementLiteral))
            {
                literal = string.Empty;
                return false;
            }

            parts.Add(elementLiteral);
        }

        if (elementType is null)
        {
            // Empty collection without a known element type cannot be typed.
            literal = string.Empty;
            return false;
        }

        literal = $"new {elementType.ToDisplayString(FqnFormat)}[] {{ {string.Join(", ", parts)} }}";
        return true;
    }

    private static ExpressionSyntax StripParentheses(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    private static string? EnumValueToMemberName(INamedTypeSymbol enumType, object? value)
    {
        foreach (ISymbol member in enumType.GetMembers())
        {
            if (member is IFieldSymbol { HasConstantValue: true } field && Equals(field.ConstantValue, value))
            {
                return field.Name;
            }
        }

        return null;
    }
}
