using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace PolyBot.Tests;

/// <summary>
/// Runs the source generator in-memory over a handler snippet and asserts the
/// GuestMessage-specific diagnostics (CUR046/CUR047).
/// </summary>
[TestClass]
public sealed class GuestMessageDiagnosticsTests
{
    private const string AnswerCall = "await client.AnswerGuestQuery(msg.GuestQueryId!, new Telegram.Bot.Types.InlineQueryResults.InlineQueryResultArticle(\"1\", \"title\", new Telegram.Bot.Types.InlineQueryResults.InputTextMessageContent(\"text\")));";

    [TestMethod]
    public void GuestHandler_CallingSendMessage_ReportsCUR046()
    {
        ImmutableArray<Diagnostic> diags = RunGenerator($$"""
            using PolyBot.Attributes;
            using PolyBot.Routing;
            using System.Threading.Tasks;
            using Telegram.Bot;
            using Telegram.Bot.Types;

            public static class Handlers
            {
                [GuestMessageHandler]
                public static async Task<Result> Wrong(Message msg, ITelegramBotClient client)
                {
                    await client.SendMessage(msg.Chat, "hello");
                    return Result.StopRouting;
                }
            }
            """);

        Assert.IsTrue(HasId(diags, "CUR046"), $"expected CUR046, got: {Format(diags)}");
        Assert.IsFalse(HasId(diags, "CUR047"), $"unexpected CUR047: {Format(diags)}");
    }

    [TestMethod]
    public void GuestHandler_DoubleAnswer_ReportsCUR047()
    {
        ImmutableArray<Diagnostic> diags = RunGenerator($$"""
            using PolyBot.Attributes;
            using PolyBot.Routing;
            using System.Threading.Tasks;
            using Telegram.Bot;
            using Telegram.Bot.Types;

            public static class Handlers
            {
                [GuestMessageHandler]
                public static async Task<Result> Twice(Message msg, ITelegramBotClient client)
                {
                    {{AnswerCall}}
                    {{AnswerCall}}
                    return Result.StopRouting;
                }
            }
            """);

        Assert.IsTrue(HasId(diags, "CUR047"), $"expected CUR047, got: {Format(diags)}");
        Assert.IsFalse(HasId(diags, "CUR046"), $"unexpected CUR046: {Format(diags)}");
    }

    [TestMethod]
    public void GuestHandler_TwoAnswersOnDifferentIfBranches_ReportsNothing()
    {
        // Control-flow analysis: only one call executes per invocation.
        ImmutableArray<Diagnostic> diags = RunGenerator($$"""
            using PolyBot.Attributes;
            using PolyBot.Routing;
            using System.Threading.Tasks;
            using Telegram.Bot;
            using Telegram.Bot.Types;

            public static class Handlers
            {
                [GuestMessageHandler]
                public static async Task<Result> Branched(Message msg, ITelegramBotClient client)
                {
                    if (msg.GuestQueryId is null)
                    {
                        {{AnswerCall}}
                    }
                    else
                    {
                        {{AnswerCall}}
                    }

                    return Result.StopRouting;
                }
            }
            """);

        Assert.IsFalse(HasId(diags, "CUR047"), $"unexpected CUR047: {Format(diags)}");
    }

    [TestMethod]
    public void GuestHandler_TwoAnswersInSameIfBranch_ReportsCUR047()
    {
        ImmutableArray<Diagnostic> diags = RunGenerator($$"""
            using PolyBot.Attributes;
            using PolyBot.Routing;
            using System.Threading.Tasks;
            using Telegram.Bot;
            using Telegram.Bot.Types;

            public static class Handlers
            {
                [GuestMessageHandler]
                public static async Task<Result> SameBranch(Message msg, ITelegramBotClient client)
                {
                    if (msg.GuestQueryId is null)
                    {
                        {{AnswerCall}}
                        {{AnswerCall}}
                    }

                    return Result.StopRouting;
                }
            }
            """);

        Assert.IsTrue(HasId(diags, "CUR047"), $"expected CUR047, got: {Format(diags)}");
    }

    [TestMethod]
    public void GuestHandler_TwoAnswersInSameLoopBody_ReportsCUR047()
    {
        ImmutableArray<Diagnostic> diags = RunGenerator($$"""
            using PolyBot.Attributes;
            using PolyBot.Routing;
            using System.Threading.Tasks;
            using Telegram.Bot;
            using Telegram.Bot.Types;

            public static class Handlers
            {
                [GuestMessageHandler]
                public static async Task<Result> SameLoop(Message msg, ITelegramBotClient client)
                {
                    while (msg.GuestQueryId is not null)
                    {
                        {{AnswerCall}}
                        {{AnswerCall}}
                        break;
                    }

                    return Result.StopRouting;
                }
            }
            """);

        Assert.IsTrue(HasId(diags, "CUR047"), $"expected CUR047, got: {Format(diags)}");
    }

    [TestMethod]
    public void GuestHandler_LoopAnswerPlusTrailingAnswer_ReportsCUR047()
    {
        ImmutableArray<Diagnostic> diags = RunGenerator($$"""
            using PolyBot.Attributes;
            using PolyBot.Routing;
            using System.Threading.Tasks;
            using Telegram.Bot;
            using Telegram.Bot.Types;

            public static class Handlers
            {
                [GuestMessageHandler]
                public static async Task<Result> LoopThenTrailing(Message msg, ITelegramBotClient client)
                {
                    while (msg.GuestQueryId is null)
                    {
                        {{AnswerCall}}
                    }

                    {{AnswerCall}}
                    return Result.StopRouting;
                }
            }
            """);

        Assert.IsTrue(HasId(diags, "CUR047"), $"expected CUR047, got: {Format(diags)}");
    }

    [TestMethod]
    public void GuestHandler_TwoAnswersInsideLambda_ReportsCUR047()
    {
        // Lambda bodies are separate control-flow graphs reachable from the method's CFG.
        ImmutableArray<Diagnostic> diags = RunGenerator($$"""
            using PolyBot.Attributes;
            using PolyBot.Routing;
            using System.Threading.Tasks;
            using Telegram.Bot;
            using Telegram.Bot.Types;

            public static class Handlers
            {
                [GuestMessageHandler]
                public static async Task<Result> ViaLambda(Message msg, ITelegramBotClient client)
                {
                    Func<Task> answerTwice = async () =>
                    {
                        {{AnswerCall}}
                        {{AnswerCall}}
                    };
                    await answerTwice();
                    return Result.StopRouting;
                }
            }
            """);

        Assert.IsTrue(HasId(diags, "CUR047"), $"expected CUR047, got: {Format(diags)}");
    }

    [TestMethod]
    public void GuestHandler_SingleAnswer_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diags = RunGenerator($$"""
            using PolyBot.Attributes;
            using PolyBot.Routing;
            using System.Threading.Tasks;
            using Telegram.Bot;
            using Telegram.Bot.Types;

            public static class Handlers
            {
                [GuestMessageHandler]
                public static async Task<Result> Right(Message msg, ITelegramBotClient client)
                {
                    {{AnswerCall}}
                    return Result.StopRouting;
                }
            }
            """);

        Assert.IsFalse(HasId(diags, "CUR046"), $"unexpected CUR046: {Format(diags)}");
        Assert.IsFalse(HasId(diags, "CUR047"), $"unexpected CUR047: {Format(diags)}");
    }

    [TestMethod]
    public void GuestHandler_SingleAnswerInsideLoop_ReportsNothing()
    {
        // Each iteration answers a different guest query; only straight-line double
        // answers are reported.
        ImmutableArray<Diagnostic> diags = RunGenerator($$"""
            using PolyBot.Attributes;
            using PolyBot.Routing;
            using System.Threading.Tasks;
            using Telegram.Bot;
            using Telegram.Bot.Types;

            public static class Handlers
            {
                [GuestMessageHandler]
                public static async Task<Result> Looped(Message msg, ITelegramBotClient client)
                {
                    while (msg.GuestQueryId is not null)
                    {
                        {{AnswerCall}}
                        break;
                    }

                    return Result.StopRouting;
                }
            }
            """);

        Assert.IsFalse(HasId(diags, "CUR047"), $"unexpected CUR047: {Format(diags)}");
    }

    [TestMethod]
    public void MessageHandler_CallingSendMessage_ReportsNothing()
    {
        // CUR046 is GuestMessage-scoped; ordinary message handlers send freely.
        ImmutableArray<Diagnostic> diags = RunGenerator("""
            using PolyBot.Attributes;
            using PolyBot.Routing;
            using System.Threading.Tasks;
            using Telegram.Bot;
            using Telegram.Bot.Types;

            public static class Handlers
            {
                [MessageHandler]
                public static async Task<Result> Echo(Message msg, ITelegramBotClient client)
                {
                    await client.SendMessage(msg.Chat, "hello");
                    return Result.StopRouting;
                }
            }
            """);

        Assert.IsFalse(HasId(diags, "CUR046"), $"unexpected CUR046: {Format(diags)}");
        Assert.IsFalse(HasId(diags, "CUR047"), $"unexpected CUR047: {Format(diags)}");
    }

    private static ImmutableArray<Diagnostic> RunGenerator(string source)
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
            "GuestMessageDiagnosticsTests",
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

    private static string CompilationErrors { get; set; } = string.Empty;

    private static bool HasId(ImmutableArray<Diagnostic> diagnostics, string id)
        => diagnostics.Any(d => d.Id == id);

    private static string Format(ImmutableArray<Diagnostic> diagnostics)
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
