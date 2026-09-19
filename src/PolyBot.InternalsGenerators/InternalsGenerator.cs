using Microsoft.CodeAnalysis;

namespace PolyBot.InternalsGenerators;

/// <summary>
/// Emits the Telegram.Bot-coupled types (handler attributes, filter bases, <c>WaitFor*</c>
/// awaiter extensions) into the PolyBot library itself; they depend only on the
/// Telegram.Bot version PolyBot references.
/// </summary>
[Generator]
public sealed class InternalsGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(
            context.CompilationProvider,
            static (SourceProductionContext sourceContext, Compilation compilation) => EmitSources(sourceContext, compilation));
    }

    private static void EmitSources(SourceProductionContext sourceContext, Compilation compilation)
    {
        AddSource(sourceContext, "PolyBotAttributes.g.cs", HandlerAttributesEmitter.Generate(compilation));
        AddSource(sourceContext, "PolyBotUpdateFilters.g.cs", UpdateFiltersEmitter.Generate(compilation));
        AddSource(sourceContext, "PolyBotAwaiterExtensions.g.cs", WaitForExtensionsEmitter.Generate(compilation));
    }

    private static void AddSource(SourceProductionContext sourceContext, string hintName, string? source)
    {
        if (source is not null)
        {
            sourceContext.AddSource(hintName, source);
        }
    }
}
