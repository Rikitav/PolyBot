using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace PolyBot.SourceGenerators;

/// <summary>
/// Reads the public constructors of a concrete filter class and derives the wrapper surface
/// (attribute ctor parameters / <c>With*</c> extension parameters): only attribute-legal
/// parameter types are mirrored; optional non-legal parameters are dropped because their
/// declared defaults apply at construction.
/// </summary>
internal static class FilterCtorInfo
{
    private static readonly SymbolDisplayFormat FqnFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>
    /// Mirrored signatures of all public constructors that qualify (every parameter is
    /// attribute-legal or optional), deduplicated by parameter signature.
    /// </summary>
    public static EquatableArray<FilterCtorModel> ReadMirroredCtors(INamedTypeSymbol filterType)
    {
        List<FilterCtorModel> ctors = new();
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (IMethodSymbol constructor in filterType.InstanceConstructors)
        {
            if (constructor.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            bool qualifies = constructor.Parameters.All(p => p.IsOptional || IsAttributeLegalType(p.Type));
            if (!qualifies)
            {
                continue;
            }

            List<FilterParamModel> parameters = new();
            foreach (IParameterSymbol parameter in constructor.Parameters)
            {
                if (!IsAttributeLegalType(parameter.Type))
                {
                    continue;
                }

                parameters.Add(new FilterParamModel
                {
                    Name = parameter.Name,
                    PropertyName = char.ToUpperInvariant(parameter.Name[0]) + parameter.Name.Substring(1),
                    TypeFqn = parameter.Type.ToDisplayString(FqnFormat),
                    IsOptional = parameter.IsOptional,
                    DefaultLiteral = parameter.IsOptional ? EmitDefaultLiteral(parameter.ExplicitDefaultValue, parameter.Type) : null,
                });
            }

            FilterCtorModel model = new() { Params = new EquatableArray<FilterParamModel>(parameters.ToArray()) };
            string signature = string.Join(",", parameters.Select(p => p.TypeFqn + (p.IsOptional ? "?" : "")));
            if (seen.Add(signature))
            {
                ctors.Add(model);
            }
        }

        return new EquatableArray<FilterCtorModel>(ctors.ToArray());
    }

    /// <summary>
    /// Whether the filter can be constructed without arguments: a public parameterless ctor
    /// exists, or every public ctor has all-optional parameters.
    /// </summary>
    public static bool HasParameterlessUsage(INamedTypeSymbol filterType)
    {
        bool anyPublic = false;
        foreach (IMethodSymbol constructor in filterType.InstanceConstructors)
        {
            if (constructor.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            anyPublic = true;
            if (constructor.Parameters.Length == 0 || constructor.Parameters.All(p => p.IsOptional))
            {
                return true;
            }
        }

        return !anyPublic;
    }

    /// <summary>Attribute-legal parameter types: primitives, string, enums, <see cref="Type"/>, and single-dim arrays of those.</summary>
    public static bool IsAttributeLegalType(ITypeSymbol type)
    {
        ITypeSymbol elementType = type is IArrayTypeSymbol { Rank: 1 } array ? array.ElementType : type;
        return elementType.SpecialType is SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_SByte
            or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32
            or SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single or SpecialType.System_Double
            or SpecialType.System_Char or SpecialType.System_String
            || elementType.TypeKind == TypeKind.Enum
            || elementType.Name == "Type" && elementType.ContainingNamespace?.ToDisplayString() == "System";
    }

    /// <summary>Emits the declared default value of an optional parameter as a C# literal.</summary>
    public static string EmitDefaultLiteral(object? value, ITypeSymbol? type)
    {
        if (value is null)
        {
            return "null";
        }

        if (type is not null && type.TypeKind == TypeKind.Enum && type is INamedTypeSymbol enumType)
        {
            string? memberName = EnumValueToMemberName(enumType, value);
            return memberName is null
                ? $"({type.ToDisplayString(FqnFormat)})({Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture)})"
                : $"{type.ToDisplayString(FqnFormat)}.{memberName}";
        }

        return value switch
        {
            string s => SymbolDisplay.FormatLiteral(s, quote: true),
            bool b => b ? "true" : "false",
            char c => SymbolDisplay.FormatLiteral(c, quote: true),
            float f => f.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "F",
            double d => d.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "D",
            decimal m => m.ToString(System.Globalization.CultureInfo.InvariantCulture) + "M",
            long l => l.ToString(System.Globalization.CultureInfo.InvariantCulture) + "L",
            ulong ul => ul.ToString(System.Globalization.CultureInfo.InvariantCulture) + "UL",
            uint ui => ui.ToString(System.Globalization.CultureInfo.InvariantCulture) + "U",
            IFormattable other => other.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ => "null",
        };
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
