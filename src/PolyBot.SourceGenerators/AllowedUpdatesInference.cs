using Microsoft.CodeAnalysis;

namespace PolyBot.SourceGenerators;

/// <summary>
/// Result of inferring the <c>AllowedUpdates</c> array from declared handlers and assembly-level overrides.
/// </summary>
internal sealed class AllowedUpdatesResult
{
    public required IReadOnlyList<string> AllowedUpdateMemberNames { get; init; }

    public required bool IsAllUpdates { get; init; }

    public required IReadOnlyList<Diagnostic> Diagnostics { get; init; }

    public bool IsEmpty => !IsAllUpdates && AllowedUpdateMemberNames.Count == 0;
}

/// <summary>
/// Infers the exact subset of <c>Telegram.Bot.Types.Enums.UpdateType</c> values a bot is capable of handling.
/// </summary>
internal static class AllowedUpdatesInference
{
    public static AllowedUpdatesResult Infer(IReadOnlyList<HandlerModel> handlers, Compilation compilation)
    {
        List<Diagnostic> diagnostics = new();
        List<string> memberNames = new();
        bool rawUpdateHandler = false;
        string? firstRawUpdateHandlerName = null;

        foreach (HandlerModel handler in handlers)
        {
            memberNames.Add(handler.UpdateTypeMemberName);

            if (handler.AcceptsRawUpdate)
            {
                rawUpdateHandler = true;
                firstRawUpdateHandlerName ??= handler.MethodName;
            }
        }

        foreach (HandlerModel handler in handlers)
        {
            foreach (AwaitSiteModel site in handler.AwaitSites.Items)
            {
                foreach (UpdateTypeMapping mapping in site.Members.Items)
                {
                    memberNames.Add(mapping.MemberName);
                }
            }
        }

        HashSet<string> inferred = new(StringComparer.Ordinal);
        foreach (string name in memberNames)
        {
            if (!string.Equals(name, "Unknown", StringComparison.Ordinal))
            {
                inferred.Add(name);
            }
        }

        if (rawUpdateHandler)
        {
            foreach (string name in GetAllUpdateTypeMemberNames(compilation))
            {
                inferred.Add(name);
            }

            if (firstRawUpdateHandlerName is not null)
            {
                diagnostics.Add(Diagnostic.Create(
                    PolyBotDiagnostics.RawUpdateHandlerFallsBackToAllAllowedUpdates,
                    Location.None,
                    firstRawUpdateHandlerName));
            }
        }

        AllowedUpdatesOverrides overrides = ReadAssemblyOverrides(compilation);

        if (overrides.IncludeAll)
        {
            return new AllowedUpdatesResult
            {
                AllowedUpdateMemberNames = Array.Empty<string>(),
                IsAllUpdates = true,
                Diagnostics = diagnostics,
            };
        }

        foreach (string include in overrides.Include)
        {
            inferred.Add(include);
        }

        HashSet<string> excluded = new(overrides.Exclude, StringComparer.Ordinal);
        if (excluded.Count > 0)
        {
            foreach (HandlerModel handler in handlers)
            {
                if (excluded.Contains(handler.UpdateTypeMemberName))
                {
                    diagnostics.Add(Diagnostic.Create(
                        PolyBotDiagnostics.HandlerUpdateTypeExcluded,
                        handler.HandlerAttributeLocation ?? Location.None,
                        handler.MethodName,
                        handler.UpdateTypeMemberName));
                }
            }

            inferred.ExceptWith(excluded);
        }

        List<string> sorted = inferred
            .Where(n => !string.Equals(n, "Unknown", StringComparison.Ordinal))
            .OrderBy(m => GetUpdateTypeOrdinal(compilation, m))
            .ToList();

        if (sorted.Count > 0)
        {
            diagnostics.Add(Diagnostic.Create(
                PolyBotDiagnostics.InferredAllowedUpdates,
                Location.None,
                sorted.Count,
                string.Join(", ", sorted)));
        }

        return new AllowedUpdatesResult
        {
            AllowedUpdateMemberNames = sorted,
            IsAllUpdates = false,
            Diagnostics = diagnostics,
        };
    }

    private static IEnumerable<string> GetAllUpdateTypeMemberNames(Compilation compilation)
    {
        INamedTypeSymbol? updateTypeEnum = compilation.GetTypeByMetadataName("Telegram.Bot.Types.Enums.UpdateType");
        if (updateTypeEnum is null)
        {
            yield break;
        }

        foreach (ISymbol member in updateTypeEnum.GetMembers())
        {
            if (member is IFieldSymbol { HasConstantValue: true } field &&
                !string.Equals(field.Name, "Unknown", StringComparison.Ordinal))
            {
                yield return field.Name;
            }
        }
    }

    private static int GetUpdateTypeOrdinal(Compilation compilation, string memberName)
    {
        INamedTypeSymbol? updateTypeEnum = compilation.GetTypeByMetadataName("Telegram.Bot.Types.Enums.UpdateType");
        if (updateTypeEnum is null)
        {
            return int.MaxValue;
        }

        foreach (ISymbol member in updateTypeEnum.GetMembers())
        {
            if (member is IFieldSymbol { HasConstantValue: true } field &&
                string.Equals(field.Name, memberName, StringComparison.Ordinal) &&
                field.ConstantValue is int ordinal)
            {
                return ordinal;
            }
        }

        return int.MaxValue;
    }

    private static AllowedUpdatesOverrides ReadAssemblyOverrides(Compilation compilation)
    {
        INamedTypeSymbol? attributeType = compilation.GetTypeByMetadataName("PolyBot.Attributes.AllowedUpdatesAttribute");
        if (attributeType is null)
        {
            return new AllowedUpdatesOverrides();
        }

        AllowedUpdatesOverrides result = new();
        foreach (AttributeData attribute in compilation.Assembly.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
            {
                continue;
            }

            if (GetNamedArgument(attribute, "IncludeAll") is { Kind: TypedConstantKind.Primitive, Value: true })
            {
                result.IncludeAll = true;
            }

            result.Include.AddRange(ReadUpdateTypeArray(attribute, "Include"));
            result.Exclude.AddRange(ReadUpdateTypeArray(attribute, "Exclude"));
        }

        return result;
    }

    private static IEnumerable<string> ReadUpdateTypeArray(AttributeData attribute, string propertyName)
    {
        TypedConstant? constant = GetNamedArgument(attribute, propertyName);
        if (constant is not { Kind: TypedConstantKind.Array })
        {
            yield break;
        }

        foreach (TypedConstant element in constant.Value.Values)
        {
            if (element.Kind == TypedConstantKind.Enum && element.Value is int ordinal)
            {
                string? name = EnumValueToMemberName(element.Type, ordinal);
                if (!string.IsNullOrEmpty(name) && !string.Equals(name, "Unknown", StringComparison.Ordinal))
                {
                    yield return name!;
                }
            }
        }
    }

    private static TypedConstant? GetNamedArgument(AttributeData attribute, string name)
    {
        foreach (KeyValuePair<string, TypedConstant> pair in attribute.NamedArguments)
        {
            if (pair.Key == name)
            {
                return pair.Value;
            }
        }

        return null;
    }

    private static string? EnumValueToMemberName(ITypeSymbol? enumType, int value)
    {
        if (enumType is not INamedTypeSymbol named || named.TypeKind != TypeKind.Enum)
        {
            return null;
        }

        foreach (ISymbol member in named.GetMembers())
        {
            if (member is IFieldSymbol { HasConstantValue: true } field &&
                field.ConstantValue is int memberValue &&
                memberValue == value)
            {
                return field.Name;
            }
        }

        return null;
    }

    private sealed class AllowedUpdatesOverrides
    {
        public bool IncludeAll { get; set; }

        public List<string> Include { get; } = new();

        public List<string> Exclude { get; } = new();
    }
}
