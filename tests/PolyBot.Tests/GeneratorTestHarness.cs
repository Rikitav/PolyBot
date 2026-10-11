using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace PolyBot.Tests;

/// <summary>
/// Runs the PolyBot source generator in-memory over a handler snippet and returns the
/// reported diagnostics, so generator diagnostics (CUR0xx) can be asserted in unit tests.
/// </summary>
internal static class GeneratorTestHarness
{
    /// <summary>Compilation errors of the last <see cref="RunGenerator"/> call — surfaced in assertion messages.</summary>
    public static string CompilationErrors { get; private set; } = string.Empty;

    public static ImmutableArray<Diagnostic> RunGenerator(string source)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Latest));

        // Reference assemblies from the .NET targeting pack (the exact surface a real
        // consumer compilation gets) plus PolyBot and Telegram.Bot. Using the runtime
        // assemblies would define System.Threading.Tasks.Task`1 in several facades and
        // Compilation.GetTypeByMetadataName would return null for it, breaking handler
        // return-type classification.
        string dotnetRoot = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        while (!Directory.Exists(Path.Combine(dotnetRoot, "packs")) && Path.GetPathRoot(dotnetRoot) != dotnetRoot)
        {
            dotnetRoot = Path.GetDirectoryName(dotnetRoot.TrimEnd(Path.DirectorySeparatorChar))!;
        }

        string refPackDir = Directory.GetDirectories(Path.Combine(dotnetRoot, "packs", "Microsoft.NETCore.App.Ref"))
            .OrderByDescending(static d => d, StringComparer.Ordinal)
            .Select(static d => Path.Combine(d, "ref", "net10.0"))
            .First(Directory.Exists);

        List<MetadataReference> references = Directory.GetFiles(refPackDir, "*.dll")
            .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
        references.Add(MetadataReference.CreateFromFile(typeof(Routing.Result).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(Telegram.Bot.ITelegramBotClient).Assembly.Location));
        // The always-emitted PolyBotExtensions source references Microsoft.Extensions.DependencyInjection;
        // without it the ad-hoc compilation has error operations and CFG creation fails.
        references.Add(MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions).Assembly.Location));

        CSharpCompilation compilation = CSharpCompilation.Create(
            "GeneratorTestHarness",
            new[] { tree },
            references,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new PolyBot.SourceGenerators.PolyBotGenerator().AsSourceGenerator() },
            optionsProvider: new FakeAnalyzerConfigOptionsProvider());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out ImmutableArray<Diagnostic> diagnostics);
        CompilationErrors = string.Join("; ", outputCompilation.GetDiagnostics()
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .Select(static d => $"{d.Id}: {d.GetMessage()}"));
        return diagnostics;
    }

    public static bool HasId(ImmutableArray<Diagnostic> diagnostics, string id)
        => diagnostics.Any(d => d.Id == id);

    public static string Format(ImmutableArray<Diagnostic> diagnostics)
        => string.Join("; ", diagnostics.Select(d => $"{d.Id}: {d.GetMessage()}")) + $" [compile errors: {CompilationErrors}]";

    /// <summary>Provides build_property.OutputType=Exe so handlers are routed (not CUR036).</summary>
    private sealed class FakeAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private static readonly AnalyzerConfigOptions Options = new FakeAnalyzerConfigOptions();

        public override AnalyzerConfigOptions GlobalOptions => Options;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Options;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Options;

        private sealed class FakeAnalyzerConfigOptions : AnalyzerConfigOptions
        {
            public override bool TryGetValue(string key, out string value)
            {
                if (key == "build_property.OutputType")
                {
                    value = "Exe";
                    return true;
                }

                value = string.Empty;
                return false;
            }
        }
    }
}
