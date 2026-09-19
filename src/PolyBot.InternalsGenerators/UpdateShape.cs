using Microsoft.CodeAnalysis;

namespace PolyBot.SourceGenerators;

internal sealed record UpdateTypeMapping(string MemberName, string PropertyName);

/// <summary>
/// Telegram.Bot <c>Update</c>/<c>UpdateType</c> shape analysis shared by the internals
/// and host generator projects. Linked into both.
/// </summary>
internal static class UpdateShape
{
    public static List<FilterGroup> BuildGroups(INamedTypeSymbol updateClass, INamedTypeSymbol updateTypeEnum)
    {
        List<IPropertySymbol> payloadProperties = updateClass.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => !p.IsStatic && p.Type.IsReferenceType && p.Type.SpecialType != SpecialType.System_String)
            .ToList();

        List<FilterGroup> groups = new();
        foreach (ISymbol member in updateTypeEnum.GetMembers())
        {
            if (member is not IFieldSymbol { HasConstantValue: true } field || field.Name == "Unknown")
            {
                continue;
            }

            IPropertySymbol? property = FindPayloadProperty(payloadProperties, field.Name);
            if (property is null)
            {
                // No payload property (scalar or absent in this Telegram.Bot version) — skip silently.
                continue;
            }

            FilterGroup? group = null;
            foreach (FilterGroup candidate in groups)
            {
                if (SymbolEqualityComparer.Default.Equals(candidate.DtoType, property.Type))
                {
                    group = candidate;
                    break;
                }
            }

            if (group is null)
            {
                group = new FilterGroup(property.Type);
                groups.Add(group);
            }

            group.Mappings.Add(new UpdateTypeMapping(field.Name, property.Name));
        }

        foreach (FilterGroup group in groups)
        {
            foreach (IPropertySymbol property in payloadProperties)
            {
                if (SymbolEqualityComparer.Default.Equals(property.Type, group.DtoType))
                {
                    group.ExtractionChain.Add(property.Name);
                }
            }
        }

        return groups;
    }

    /// <summary>
    /// Ordinal match first, then case-insensitive; no fallback to the member name.
    /// </summary>
    public static IPropertySymbol? FindPayloadProperty(List<IPropertySymbol> payloadProperties, string memberName)
    {
        foreach (IPropertySymbol property in payloadProperties)
        {
            if (string.Equals(property.Name, memberName, StringComparison.Ordinal))
            {
                return property;
            }
        }

        foreach (IPropertySymbol property in payloadProperties)
        {
            if (string.Equals(property.Name, memberName, StringComparison.OrdinalIgnoreCase))
            {
                return property;
            }
        }

        return null;
    }

    public static string GroupBaseName(FilterGroup group)
    {
        string dtoName = group.DtoType.Name;
        return dtoName == group.Mappings[0].MemberName ? dtoName + "Filter" : group.Mappings[0].MemberName + "Filter";
    }

    /// <summary>
    /// Generated filter base class names; filter discovery matches error-symbol base
    /// classes against these because the generated bases do not exist during the single
    /// evaluation pass.
    /// </summary>
    public static HashSet<string> GetGeneratedFilterBaseNames(Compilation compilation)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        INamedTypeSymbol? updateClass = compilation.GetTypeByMetadataName("Telegram.Bot.Types.Update");
        INamedTypeSymbol? updateTypeEnum = compilation.GetTypeByMetadataName("Telegram.Bot.Types.Enums.UpdateType");
        if (updateClass is null || updateTypeEnum is null)
        {
            return names;
        }

        foreach (FilterGroup group in BuildGroups(updateClass, updateTypeEnum))
        {
            names.Add(GroupBaseName(group));
            for (int i = 1; i < group.Mappings.Count; i++)
            {
                names.Add(group.Mappings[i].MemberName + "Filter");
            }
        }

        return names;
    }

    public static string FindUpdatePropertyName(INamedTypeSymbol updateClass, string memberName)
    {
        foreach (IPropertySymbol property in updateClass.GetMembers().OfType<IPropertySymbol>())
        {
            if (string.Equals(property.Name, memberName, StringComparison.Ordinal))
            {
                return property.Name;
            }
        }

        foreach (IPropertySymbol property in updateClass.GetMembers().OfType<IPropertySymbol>())
        {
            if (string.Equals(property.Name, memberName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Name;
            }
        }

        return memberName;
    }

    public sealed class FilterGroup
    {
        public FilterGroup(ITypeSymbol dtoType)
        {
            DtoType = dtoType;
        }

        public ITypeSymbol DtoType { get; }

        public List<UpdateTypeMapping> Mappings { get; } = new();

        public List<string> ExtractionChain { get; } = new();
    }
}
