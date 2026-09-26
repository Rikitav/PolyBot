using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace PolyBot.SourceGenerators;

/// <summary>
/// Finds concrete, non-generic <c>PolyBot.IUpdateFilter</c> classes (attribute classes excluded); generic ones are reported as CUR012 and skipped.
/// </summary>
internal static class FilterDiscovery
{
    public static List<FilterClassModel> Discover(Compilation compilation, out ImmutableArray<Diagnostic> diagnostics)
    {
        List<Diagnostic> diagnosticList = new();
        List<FilterClassModel> filters = new();
        INamedTypeSymbol? filterInterface = compilation.GetTypeByMetadataName("PolyBot.Routing.IUpdateFilter");
        if (filterInterface is null)
        {
            diagnostics = ImmutableArray<Diagnostic>.Empty;
            return filters;
        }

        INamedTypeSymbol? attributeBase = compilation.GetTypeByMetadataName("System.Attribute");
        HashSet<string> knownBaseNames = UpdateShape.GetGeneratedFilterBaseNames(compilation);
        HashSet<string> seen = new(StringComparer.Ordinal);
        // Only the consumer's own source is scanned; referenced assemblies contribute wrappers and With* extensions as compiled types, resolved semantically where used.
        CollectTypes(compilation.Assembly.GlobalNamespace, filterInterface, attributeBase, knownBaseNames, compilation.Assembly, filters, seen, diagnosticList);

        diagnostics = ImmutableArray.CreateRange(diagnosticList);
        return filters;
    }

    private static void CollectTypes(
        INamespaceOrTypeSymbol container,
        INamedTypeSymbol filterInterface,
        INamedTypeSymbol? attributeBase,
        HashSet<string> knownBaseNames,
        IAssemblySymbol userAssembly,
        List<FilterClassModel> filters,
        HashSet<string> seen,
        List<Diagnostic> diagnostics)
    {
        foreach (ISymbol member in container.GetMembers())
        {
            if (member is INamespaceSymbol namespaceSymbol)
            {
                CollectTypes(namespaceSymbol, filterInterface, attributeBase, knownBaseNames, userAssembly, filters, seen, diagnostics);
                continue;
            }

            if (member is not INamedTypeSymbol typeSymbol)
            {
                continue;
            }

            CollectTypes(typeSymbol, filterInterface, attributeBase, knownBaseNames, userAssembly, filters, seen, diagnostics);

            if (typeSymbol.TypeKind != TypeKind.Class || typeSymbol.IsAbstract)
            {
                continue;
            }

            if (IsAttributeClass(typeSymbol, attributeBase) || !ImplementsFilter(typeSymbol, filterInterface, knownBaseNames))
            {
                continue;
            }

            if (typeSymbol.IsGenericType)
            {
                diagnostics.Add(Diagnostic.Create(
                    PolyBotDiagnostics.GenericFilterClass,
                    typeSymbol.Locations.FirstOrDefault(),
                    typeSymbol.Name));
                continue;
            }

            // Only public or user-assembly-internal types are safe to reference from the generated code.
            if (typeSymbol.DeclaredAccessibility != Accessibility.Public &&
                (typeSymbol.DeclaredAccessibility != Accessibility.Internal ||
                 !SymbolEqualityComparer.Default.Equals(typeSymbol.ContainingAssembly, userAssembly)))
            {
                continue;
            }

            string typeFqn = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (seen.Add(typeFqn))
            {
                filters.Add(new FilterClassModel
                {
                    TypeFqn = typeFqn,
                    ShortName = typeSymbol.Name,
                    DtoBaseName = FindDtoBaseName(typeSymbol, knownBaseNames),
                    Ctors = FilterCtorInfo.ReadMirroredCtors(typeSymbol),
                    HasParameterlessUsage = FilterCtorInfo.HasParameterlessUsage(typeSymbol),
                });
            }
        }
    }

    private static string? FindDtoBaseName(INamedTypeSymbol typeSymbol, HashSet<string> knownBaseNames)
    {
        for (INamedTypeSymbol? baseType = typeSymbol.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (knownBaseNames.Contains(baseType.Name))
            {
                return baseType.Name;
            }
        }

        return null;
    }

    private static bool ImplementsFilter(
        INamedTypeSymbol typeSymbol,
        INamedTypeSymbol filterInterface,
        HashSet<string> knownBaseNames)
    {
        foreach (INamedTypeSymbol interfaceSymbol in typeSymbol.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(interfaceSymbol, filterInterface))
            {
                return true;
            }
        }

        // Single-pass case: filters subclassing the generated DTO bases have an error-type
        // base during this generator's evaluation (the bases are produced in the same
        // pass), so match the error symbol's name against the known generated base names.
        for (INamedTypeSymbol? baseType = typeSymbol.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (baseType is IErrorTypeSymbol errorBase)
            {
                if (knownBaseNames.Contains(errorBase.Name))
                {
                    return true;
                }
            }
            else if (SymbolEqualityComparer.Default.Equals(baseType.OriginalDefinition, filterInterface))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAttributeClass(INamedTypeSymbol typeSymbol, INamedTypeSymbol? attributeBase)
    {
        for (INamedTypeSymbol? baseType = typeSymbol.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (attributeBase is not null &&
                SymbolEqualityComparer.Default.Equals(baseType.OriginalDefinition, attributeBase))
            {
                return true;
            }

            if (baseType.Name == "Attribute" &&
                baseType.ContainingNamespace is { IsGlobalNamespace: false } ns &&
                ns.ToDisplayString() == "System")
            {
                return true;
            }
        }

        return false;
    }
}
