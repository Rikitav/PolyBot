using System.Collections.Immutable;

namespace PolyBot.Tests;

/// <summary>
/// Asserts the GuestMessage-specific diagnostics: CUR046 (chat-bound sends) and CUR047
/// (double answers, control-flow analysis).
/// </summary>
[TestClass]
public sealed class GuestMessageDiagnosticsTests
{
    private const string AnswerCall = "await client.AnswerGuestQuery(msg.GuestQueryId!, new Telegram.Bot.Types.InlineQueryResults.InlineQueryResultArticle(\"1\", \"title\", new Telegram.Bot.Types.InlineQueryResults.InputTextMessageContent(\"text\")));";

    [TestMethod]
    public void GuestHandler_CallingSendMessage_ReportsCUR046()
    {
        ImmutableArray<Diagnostic> diags = GeneratorTestHarness.RunGenerator("""
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

        Assert.IsTrue(GeneratorTestHarness.HasId(diags, "CUR046"), $"expected CUR046, got: {GeneratorTestHarness.Format(diags)}");
        Assert.IsFalse(GeneratorTestHarness.HasId(diags, "CUR047"), $"unexpected CUR047: {GeneratorTestHarness.Format(diags)}");
    }

    [TestMethod]
    public void GuestHandler_DoubleAnswer_ReportsCUR047()
    {
        ImmutableArray<Diagnostic> diags = GeneratorTestHarness.RunGenerator($$"""
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

        Assert.IsTrue(GeneratorTestHarness.HasId(diags, "CUR047"), $"expected CUR047, got: {GeneratorTestHarness.Format(diags)}");
        Assert.IsFalse(GeneratorTestHarness.HasId(diags, "CUR046"), $"unexpected CUR046: {GeneratorTestHarness.Format(diags)}");
    }

    [TestMethod]
    public void GuestHandler_TwoAnswersOnDifferentIfBranches_ReportsNothing()
    {
        // Control-flow analysis: only one call executes per invocation.
        ImmutableArray<Diagnostic> diags = GeneratorTestHarness.RunGenerator($$"""
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

        Assert.IsFalse(GeneratorTestHarness.HasId(diags, "CUR047"), $"unexpected CUR047: {GeneratorTestHarness.Format(diags)}");
    }

    [TestMethod]
    public void GuestHandler_TwoAnswersInSameIfBranch_ReportsCUR047()
    {
        ImmutableArray<Diagnostic> diags = GeneratorTestHarness.RunGenerator($$"""
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

        Assert.IsTrue(GeneratorTestHarness.HasId(diags, "CUR047"), $"expected CUR047, got: {GeneratorTestHarness.Format(diags)}");
    }

    [TestMethod]
    public void GuestHandler_TwoAnswersInSameLoopBody_ReportsCUR047()
    {
        ImmutableArray<Diagnostic> diags = GeneratorTestHarness.RunGenerator($$"""
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

        Assert.IsTrue(GeneratorTestHarness.HasId(diags, "CUR047"), $"expected CUR047, got: {GeneratorTestHarness.Format(diags)}");
    }

    [TestMethod]
    public void GuestHandler_LoopAnswerPlusTrailingAnswer_ReportsCUR047()
    {
        ImmutableArray<Diagnostic> diags = GeneratorTestHarness.RunGenerator($$"""
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

        Assert.IsTrue(GeneratorTestHarness.HasId(diags, "CUR047"), $"expected CUR047, got: {GeneratorTestHarness.Format(diags)}");
    }

    [TestMethod]
    public void GuestHandler_TwoAnswersInsideLambda_ReportsCUR047()
    {
        // Lambda bodies are separate control-flow graphs reachable from the method's CFG.
        ImmutableArray<Diagnostic> diags = GeneratorTestHarness.RunGenerator($$"""
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

        Assert.IsTrue(GeneratorTestHarness.HasId(diags, "CUR047"), $"expected CUR047, got: {GeneratorTestHarness.Format(diags)}");
    }

    [TestMethod]
    public void GuestHandler_SingleAnswer_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diags = GeneratorTestHarness.RunGenerator($$"""
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

        Assert.IsFalse(GeneratorTestHarness.HasId(diags, "CUR046"), $"unexpected CUR046: {GeneratorTestHarness.Format(diags)}");
        Assert.IsFalse(GeneratorTestHarness.HasId(diags, "CUR047"), $"unexpected CUR047: {GeneratorTestHarness.Format(diags)}");
    }

    [TestMethod]
    public void GuestHandler_SingleAnswerInsideLoop_ReportsNothing()
    {
        // Each iteration answers a different guest query; only straight-line double
        // answers are reported.
        ImmutableArray<Diagnostic> diags = GeneratorTestHarness.RunGenerator($$"""
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

        Assert.IsFalse(GeneratorTestHarness.HasId(diags, "CUR047"), $"unexpected CUR047: {GeneratorTestHarness.Format(diags)}");
    }

    [TestMethod]
    public void MessageHandler_CallingSendMessage_ReportsNothing()
    {
        // CUR046 is GuestMessage-scoped; ordinary message handlers send freely.
        ImmutableArray<Diagnostic> diags = GeneratorTestHarness.RunGenerator("""
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

        Assert.IsFalse(GeneratorTestHarness.HasId(diags, "CUR046"), $"unexpected CUR046: {GeneratorTestHarness.Format(diags)}");
        Assert.IsFalse(GeneratorTestHarness.HasId(diags, "CUR047"), $"unexpected CUR047: {GeneratorTestHarness.Format(diags)}");
    }
}
