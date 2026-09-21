using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace PolyBot.SourceGenerators;

/// <summary>
/// Host-side generator: BotRouter, BotFather sync, filter wrappers, With* extensions and keyboards; the Telegram.Bot-coupled types are emitted by PolyBot.InternalsGenerators into the PolyBot library.
/// </summary>
[Generator]
public sealed class PolyBotGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValueProvider<bool> isLibraryProvider = context.AnalyzerConfigOptionsProvider.Select(
            static (AnalyzerConfigOptionsProvider options, CancellationToken _) =>
                options.GlobalOptions.TryGetValue("build_property.OutputType", out string? outputType) &&
                string.Equals(outputType, "Library", StringComparison.OrdinalIgnoreCase));

        IncrementalValueProvider<GenerationInputs> generated = context.CompilationProvider.Combine(isLibraryProvider).Select(
            static ((Compilation Compilation, bool IsLibrary) pair, CancellationToken _) =>
            {
                List<FilterClassModel> discoveredFilters = FilterDiscovery.Discover(pair.Compilation, out ImmutableArray<Diagnostic> filterDiagnostics);
                EquatableArray<FilterClassModel> filterClasses = new(discoveredFilters.ToArray());
                return new GenerationInputs
                {
                    Compilation = pair.Compilation,
                    FilterAttributeSource = FilterAttributesEmitter.Generate(filterClasses),
                    AwaiterExtensionSource = AwaiterExtensionsEmitter.Generate(pair.Compilation, filterClasses),
                    HostingSource = pair.IsLibrary ? null : HostingEmitter.GenerateHosting(pair.Compilation),
                    WebhookSource = pair.IsLibrary ? null : HostingEmitter.GenerateWebhook(pair.Compilation),
                    IsLibrary = pair.IsLibrary,
                    FilterClasses = filterClasses,
                    Diagnostics = filterDiagnostics,
                };
            });

        IncrementalValuesProvider<HandlerItem> discovered = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (SyntaxNode node, CancellationToken _) =>
                    node is MethodDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: static (GeneratorSyntaxContext syntaxContext, CancellationToken _) => syntaxContext)
            .Combine(generated)
            .Select(static ((GeneratorSyntaxContext Context, GenerationInputs Generated) pair, CancellationToken _) =>
                HandlerDiscovery.Discover(pair.Context, pair.Generated.FilterClasses));

        IncrementalValueProvider<ImmutableArray<HandlerItem>> handlers = discovered.Collect();

        IncrementalValuesProvider<KeyboardItem> discoveredKeyboards = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (SyntaxNode node, CancellationToken _) =>
                    node is MethodDeclarationSyntax { AttributeLists.Count: > 0 } or PropertyDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: static (GeneratorSyntaxContext syntaxContext, CancellationToken _) =>
                    KeyboardDiscovery.Discover(syntaxContext));

        IncrementalValueProvider<ImmutableArray<KeyboardItem>> keyboards = discoveredKeyboards.Collect();

        IncrementalValuesProvider<ExceptionHandlerItem> discoveredExceptionHandlers = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (SyntaxNode node, CancellationToken _) =>
                    node is MethodDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: static (GeneratorSyntaxContext syntaxContext, CancellationToken _) =>
                    ExceptionHandlerDiscovery.Discover(syntaxContext));

        IncrementalValueProvider<ImmutableArray<ExceptionHandlerItem>> exceptionHandlers = discoveredExceptionHandlers.Collect();

        context.RegisterSourceOutput(
            generated.Combine(handlers).Combine(keyboards).Combine(exceptionHandlers),
            static (SourceProductionContext sourceProductionContext, (((GenerationInputs Generated, ImmutableArray<HandlerItem> Items) Pair, ImmutableArray<KeyboardItem> Keyboards) Triple, ImmutableArray<ExceptionHandlerItem> ErrorHandlers) pair) =>
                EmitOutputs(sourceProductionContext, pair.Triple.Pair.Generated, pair.Triple.Pair.Items, pair.Triple.Keyboards, pair.ErrorHandlers));
    }

    private static void EmitOutputs(
        SourceProductionContext sourceProductionContext,
        GenerationInputs generated,
        ImmutableArray<HandlerItem> items,
        ImmutableArray<KeyboardItem> keyboardItems,
        ImmutableArray<ExceptionHandlerItem> exceptionHandlerItems)
    {
        foreach (Diagnostic diagnostic in generated.Diagnostics)
        {
            sourceProductionContext.ReportDiagnostic(diagnostic);
        }

        ExceptionHandlerModel? exceptionHandler = null;
        foreach (ExceptionHandlerItem item in exceptionHandlerItems)
        {
            foreach (Diagnostic diagnostic in item.Diagnostics)
            {
                sourceProductionContext.ReportDiagnostic(diagnostic);
            }

            if (item.Model is null)
            {
                continue;
            }

            if (exceptionHandler is null)
            {
                exceptionHandler = item.Model;
            }
            else
            {
                sourceProductionContext.ReportDiagnostic(Diagnostic.Create(
                    PolyBotDiagnostics.MultipleExceptionHandlers,
                    item.Location ?? Location.None,
                    item.Model.MethodName));
            }
        }

        List<HandlerModel> models = new();
        foreach (HandlerItem item in items)
        {
            foreach (Diagnostic diagnostic in item.Diagnostics)
            {
                sourceProductionContext.ReportDiagnostic(diagnostic);
            }

            if (item.Model is not null)
            {
                models.Add(item.Model);
            }
        }

        List<KeyboardModel> keyboards = new();
        foreach (KeyboardItem item in keyboardItems)
        {
            foreach (Diagnostic diagnostic in item.Diagnostics)
            {
                sourceProductionContext.ReportDiagnostic(diagnostic);
            }

            if (item.Model is not null)
            {
                keyboards.Add(item.Model);
            }
        }

        if (generated.FilterAttributeSource is not null)
        {
            sourceProductionContext.AddSource("PolyBotFilterAttributes.g.cs", generated.FilterAttributeSource);
        }

        if (generated.AwaiterExtensionSource is not null)
        {
            sourceProductionContext.AddSource("PolyBotAwaiterExtensions.g.cs", generated.AwaiterExtensionSource);
        }

        // Libraries emit filters, With* extensions and keyboards only; runnable assemblies get the router, DI extensions and hosting.
        if (!generated.IsLibrary)
        {
            if (models.Count > 0 && generated.HostingSource is not null)
            {
                sourceProductionContext.AddSource("PolyBotHosting.g.cs", generated.HostingSource);
            }

            if (models.Count > 0 && generated.WebhookSource is not null)
            {
                sourceProductionContext.AddSource("PolyBotWebhook.g.cs", generated.WebhookSource);
            }
        }

        string? keyboardSource = KeyboardMarkupEmitter.Generate(keyboards.ToImmutableArray());
        if (keyboardSource is not null)
        {
            sourceProductionContext.AddSource("PolyBotKeyboards.g.cs", keyboardSource);
        }

        if (generated.IsLibrary)
        {
            foreach (HandlerModel model in models)
            {
                sourceProductionContext.ReportDiagnostic(Diagnostic.Create(
                    PolyBotDiagnostics.HandlerInLibraryProject,
                    Location.None,
                    model.MethodName));
            }

            return;
        }

        if (models.Count == 0 && exceptionHandler is null)
        {
            return;
        }

        AllowedUpdatesResult allowedUpdates = AllowedUpdatesInference.Infer(models, generated.Compilation);
        foreach (Diagnostic diagnostic in allowedUpdates.Diagnostics)
        {
            sourceProductionContext.ReportDiagnostic(diagnostic);
        }

        ReportDuplicateBotFatherCommands(models, sourceProductionContext);
        sourceProductionContext.AddSource("PolyBotBotFatherSync.g.cs", BotFatherSyncEmitter.Generate(models));
        sourceProductionContext.AddSource("BotRouter.g.cs", BotRouterEmitter.Generate(models, exceptionHandler, allowedUpdates));
        sourceProductionContext.AddSource("PolyBotExtensions.g.cs", PolyBotExtensionsEmitter.Generate());
    }

    private static void ReportDuplicateBotFatherCommands(List<HandlerModel> models, SourceProductionContext context)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (HandlerModel model in models.OrderBy(static m => m.Priority))
        {
            if (model.Prefix != '/' || model.Aliases.Count == 0 || model.CommandHidden)
            {
                continue;
            }

            string language = model.CommandLanguageCode ?? string.Empty;
            string bucket = model.CommandScope + "\u001F" + language;
            foreach (string alias in model.Aliases)
            {
                string key = bucket + "\u001F" + alias;
                if (!seen.Add(key))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        PolyBotDiagnostics.DuplicateBotFatherCommand,
                        Location.None,
                        alias,
                        model.CommandScope,
                        model.CommandLanguageCode ?? "default"));
                }
            }
        }
    }

    private sealed class GenerationInputs
    {
        public required Compilation Compilation { get; init; }

        public required string? FilterAttributeSource { get; init; }

        public required string? AwaiterExtensionSource { get; init; }

        public required string? HostingSource { get; init; }

        public required string? WebhookSource { get; init; }

        /// <summary>
        /// Whether the consumer compiles as a library — libraries get filters, With* extensions and keyboards only.
        /// </summary>
        public required bool IsLibrary { get; init; }

        public required EquatableArray<FilterClassModel> FilterClasses { get; init; }

        public required ImmutableArray<Diagnostic> Diagnostics { get; init; }
    }
}
