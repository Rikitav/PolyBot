using System.Collections.Immutable;

namespace PolyBot.Tests;

/// <summary>
/// Asserts the handler API-usage diagnostics CUR048-CUR056: double answers for
/// callback/inline queries, un-answered callback queries, keyboard callback data
/// literals, inline result limits, command alias/prefix mismatches and async hygiene.
/// </summary>
[TestClass]
public sealed class ApiUsageDiagnosticsTests
{
    private const string AnswerCallback = "await client.AnswerCallbackQuery(query.Id);";
    private const string InlineResult = "new Telegram.Bot.Types.InlineQueryResults.InlineQueryResultArticle(\"1\", \"title\", new Telegram.Bot.Types.InlineQueryResults.InputTextMessageContent(\"text\"))";

    [TestMethod]
    public void GuestHandler_CallingSendPhoto_ReportsCUR046()
    {
        // The whole chat-bound Send* family is caught, not just SendMessage.
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
                    await client.SendPhoto(msg.Chat, "https://example.com/p.png");
                    return Result.StopRouting;
                }
            }
            """);

        Assert.IsTrue(GeneratorTestHarness.HasId(diags, "CUR046"), $"expected CUR046, got: {GeneratorTestHarness.Format(diags)}");
    }

    [TestMethod]
    public void CallbackHandler_DoubleAnswer_ReportsCUR048()
    {
        ImmutableArray<Diagnostic> diags = GeneratorTestHarness.RunGenerator($$"""
            using PolyBot.Attributes;
            using PolyBot.Routing;
            using System.Threading.Tasks;
            using Telegram.Bot;
            using Telegram.Bot.Types;

            public static class Handlers
            {
                [CallbackQueryHandler]
                public static async Task<Result> OnCallback(CallbackQuery query, ITelegramBotClient client)
                {
                    {{AnswerCallback}}
                    {{AnswerCallback}}
                    return Result.StopRouting;
                }
            }
            """);

        Assert.IsTrue(GeneratorTestHarness.HasId(diags, "CUR048"), $"expected CUR048, got: {GeneratorTestHarness.Format(diags)}");
    }

    [TestMethod]
    public void CallbackHandler_SingleAnswer_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diags = GeneratorTestHarness.RunGenerator($$"""
            using PolyBot.Attributes;
            using PolyBot.Routing;
            using System.Threading.Tasks;
            using Telegram.Bot;
            using Telegram.Bot.Types;

            public static class Handlers
            {
                [CallbackQueryHandler]
                public static async Task<Result> OnCallback(CallbackQuery query, ITelegramBotClient client)
                {
                    {{AnswerCallback}}
                    return Result.StopRouting;
                }
            }
            """);

        Assert.IsFalse(GeneratorTestHarness.HasId(diags, "CUR048"), $"unexpected CUR048: {GeneratorTestHarness.Format(diags)}");
        Assert.IsFalse(GeneratorTestHarness.HasId(diags, "CUR056"), $"unexpected CUR056: {GeneratorTestHarness.Format(diags)}");
    }

    [TestMethod]
    public void CallbackHandler_NeverAnswers_ReportsCUR056()
    {
        ImmutableArray<Diagnostic> diags = GeneratorTestHarness.RunGenerator("""
            using PolyBot.Attributes;
            using PolyBot.Routing;
            using System.Threading.Tasks;
            using Telegram.Bot;
            using Telegram.Bot.Types;

            public static class Handlers
            {
                [CallbackQueryHandler]
                public static Task<Result> OnCallback(CallbackQuery query)
                {
                    return Task.FromResult(Result.StopRouting);
                }
            }
            """);

        Assert.IsTrue(GeneratorTestHarness.HasId(diags, "CUR056"), $"expected CUR056, got: {GeneratorTestHarness.Format(diags)}");
    }

    [TestMethod]
    public void InlineHandler_DoubleAnswer_ReportsCUR049()
    {
        ImmutableArray<Diagnostic> diags = GeneratorTestHarness.RunGenerator($$"""
            using PolyBot.Attributes;
            using PolyBot.Routing;
            using System.Threading.Tasks;
            using Telegram.Bot;
            using Telegram.Bot.Types;
            using Telegram.Bot.Types.InlineQueryResults;

            public static class Handlers
            {
                [InlineQueryHandler]
                public static async Task<Result> OnInline(InlineQuery query, ITelegramBotClient client)
                {
                    InlineQueryResult[] results = [{{InlineResult}}];
                    await client.AnswerInlineQuery(query.Id, results);
                    await client.AnswerInlineQuery(query.Id, results);
                    return Result.StopRouting;
                }
            }
            """);

        Assert.IsTrue(GeneratorTestHarness.HasId(diags, "CUR049"), $"expected CUR049, got: {GeneratorTestHarness.Format(diags)}");
    }

    [TestMethod]
    public void InlineHandler_TooManyResults_ReportsCUR051()
    {
        string results = string.Join(", ", Enumerable.Repeat(InlineResult, 51));
        ImmutableArray<Diagnostic> diags = GeneratorTestHarness.RunGenerator($$"""
            using PolyBot.Attributes;
            using PolyBot.Routing;
            using System.Threading.Tasks;
            using Telegram.Bot;
            using Telegram.Bot.Types;
            using Telegram.Bot.Types.InlineQueryResults;

            public static class Handlers
            {
                [InlineQueryHandler]
                public static async Task<Result> OnInline(InlineQuery query, ITelegramBotClient client)
                {
                    InlineQueryResult[] results = [{{results}}];
                    await client.AnswerInlineQuery(query.Id, results);
                    return Result.StopRouting;
                }
            }
            """);

        Assert.IsTrue(GeneratorTestHarness.HasId(diags, "CUR051"), $"expected CUR051, got: {GeneratorTestHarness.Format(diags)}");
    }

    [TestMethod]
    public void InlineHandler_FiftyResults_ReportsNothing()
    {
        string results = string.Join(", ", Enumerable.Repeat(InlineResult, 50));
        ImmutableArray<Diagnostic> diags = GeneratorTestHarness.RunGenerator($$"""
            using PolyBot.Attributes;
            using PolyBot.Routing;
            using System.Threading.Tasks;
            using Telegram.Bot;
            using Telegram.Bot.Types;
            using Telegram.Bot.Types.InlineQueryResults;

            public static class Handlers
            {
                [InlineQueryHandler]
                public static async Task<Result> OnInline(InlineQuery query, ITelegramBotClient client)
                {
                    InlineQueryResult[] results = [{{results}}];
                    await client.AnswerInlineQuery(query.Id, results);
                    return Result.StopRouting;
                }
            }
            """);

        Assert.IsFalse(GeneratorTestHarness.HasId(diags, "CUR051"), $"unexpected CUR051: {GeneratorTestHarness.Format(diags)}");
    }

    [TestMethod]
    public void Handler_CallbackDataTooLong_ReportsCUR050()
    {
        string longData = new('a', 65);
        ImmutableArray<Diagnostic> diags = GeneratorTestHarness.RunGenerator($$"""
            using PolyBot.Attributes;
            using PolyBot.Routing;
            using System.Threading.Tasks;
            using Telegram.Bot;
            using Telegram.Bot.Types;

            public static class Handlers
            {
                [MessageHandler]
                public static Task<Result> OnMessage(Message msg)
                {
                    _ = Telegram.Bot.Types.InlineKeyboardButton.WithCallbackData("pick", "{{longData}}");
                    return Task.FromResult(Result.StopRouting);
                }
            }
            """);

        Assert.IsTrue(GeneratorTestHarness.HasId(diags, "CUR050"), $"expected CUR050, got: {GeneratorTestHarness.Format(diags)}");
    }

    [TestMethod]
    public void Handler_CallbackDataEmpty_ReportsCUR050()
    {
        ImmutableArray<Diagnostic> diags = GeneratorTestHarness.RunGenerator("""
            using PolyBot.Attributes;
            using PolyBot.Routing;
            using System.Threading.Tasks;
            using Telegram.Bot;
            using Telegram.Bot.Types;

            public static class Handlers
            {
                [MessageHandler]
                public static Task<Result> OnMessage(Message msg)
                {
                    _ = Telegram.Bot.Types.InlineKeyboardButton.WithCallbackData("pick", "");
                    return Task.FromResult(Result.StopRouting);
                }
            }
            """);

        Assert.IsTrue(GeneratorTestHarness.HasId(diags, "CUR050"), $"expected CUR050, got: {GeneratorTestHarness.Format(diags)}");
    }

    [TestMethod]
    public void Handler_CallbackDataInInitializerTooLong_ReportsCUR050()
    {
        string longData = new('a', 65);
        ImmutableArray<Diagnostic> diags = GeneratorTestHarness.RunGenerator($$"""
            using PolyBot.Attributes;
            using PolyBot.Routing;
            using System.Threading.Tasks;
            using Telegram.Bot;
            using Telegram.Bot.Types;

            public static class Handlers
            {
                [MessageHandler]
                public static Task<Result> OnMessage(Message msg)
                {
                    _ = new Telegram.Bot.Types.InlineKeyboardButton("pick") { CallbackData = "{{longData}}" };
                    return Task.FromResult(Result.StopRouting);
                }
            }
            """);

        Assert.IsTrue(GeneratorTestHarness.HasId(diags, "CUR050"), $"expected CUR050, got: {GeneratorTestHarness.Format(diags)}");
    }

    [TestMethod]
    public void Handler_InterpolatedCallbackData_ReportsCUR055()
    {
        ImmutableArray<Diagnostic> diags = GeneratorTestHarness.RunGenerator("""
            using PolyBot.Attributes;
            using PolyBot.Routing;
            using System.Threading.Tasks;
            using Telegram.Bot;
            using Telegram.Bot.Types;

            public static class Handlers
            {
                [MessageHandler]
                public static Task<Result> OnMessage(Message msg)
                {
                    int id = 1;
                    _ = Telegram.Bot.Types.InlineKeyboardButton.WithCallbackData("pick", $"item:{id}");
                    return Task.FromResult(Result.StopRouting);
                }
            }
            """);

        Assert.IsTrue(GeneratorTestHarness.HasId(diags, "CUR055"), $"expected CUR055, got: {GeneratorTestHarness.Format(diags)}");
    }

    [TestMethod]
    public void CommandHandler_AliasStartsWithPrefix_ReportsCUR052()
    {
        ImmutableArray<Diagnostic> diags = GeneratorTestHarness.RunGenerator("""
            using PolyBot.Attributes;
            using PolyBot.Routing;
            using System.Threading.Tasks;
            using Telegram.Bot;
            using Telegram.Bot.Types;

            public static class Handlers
            {
                [MessageHandler]
                [Command(Aliases = ["/start"])]
                public static Task<Result> BadAlias(Message msg)
                {
                    return Task.FromResult(Result.StopRouting);
                }
            }
            """);

        Assert.IsTrue(GeneratorTestHarness.HasId(diags, "CUR052"), $"expected CUR052, got: {GeneratorTestHarness.Format(diags)}");
    }

    [TestMethod]
    public void CommandHandler_PlainAlias_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diags = GeneratorTestHarness.RunGenerator("""
            using PolyBot.Attributes;
            using PolyBot.Routing;
            using System.Threading.Tasks;
            using Telegram.Bot;
            using Telegram.Bot.Types;

            public static class Handlers
            {
                [MessageHandler]
                [Command(Aliases = ["start"])]
                public static Task<Result> GoodAlias(Message msg)
                {
                    return Task.FromResult(Result.StopRouting);
                }
            }
            """);

        Assert.IsFalse(GeneratorTestHarness.HasId(diags, "CUR052"), $"unexpected CUR052: {GeneratorTestHarness.Format(diags)}");
    }

    [TestMethod]
    public void Handler_TaskResultBlocking_ReportsCUR053()
    {
        ImmutableArray<Diagnostic> diags = GeneratorTestHarness.RunGenerator("""
            using PolyBot.Attributes;
            using PolyBot.Routing;
            using System.Threading.Tasks;
            using Telegram.Bot;
            using Telegram.Bot.Types;

            public static class Handlers
            {
                [MessageHandler]
                public static Task<Result> OnMessage(Message msg, ITelegramBotClient client)
                {
                    _ = client.GetMe().Result;
                    return Task.FromResult(Result.StopRouting);
                }
            }
            """);

        Assert.IsTrue(GeneratorTestHarness.HasId(diags, "CUR053"), $"expected CUR053, got: {GeneratorTestHarness.Format(diags)}");
    }

    [TestMethod]
    public void Handler_TaskWaitBlocking_ReportsCUR053()
    {
        ImmutableArray<Diagnostic> diags = GeneratorTestHarness.RunGenerator("""
            using PolyBot.Attributes;
            using PolyBot.Routing;
            using System.Threading.Tasks;
            using Telegram.Bot;
            using Telegram.Bot.Types;

            public static class Handlers
            {
                [MessageHandler]
                public static Task<Result> OnMessage(Message msg, ITelegramBotClient client)
                {
                    client.GetMe().Wait();
                    return Task.FromResult(Result.StopRouting);
                }
            }
            """);

        Assert.IsTrue(GeneratorTestHarness.HasId(diags, "CUR053"), $"expected CUR053, got: {GeneratorTestHarness.Format(diags)}");
    }

    [TestMethod]
    public void Handler_NewTelegramBotClient_ReportsCUR054()
    {
        ImmutableArray<Diagnostic> diags = GeneratorTestHarness.RunGenerator("""
            using PolyBot.Attributes;
            using PolyBot.Routing;
            using System.Threading.Tasks;
            using Telegram.Bot;
            using Telegram.Bot.Types;

            public static class Handlers
            {
                [MessageHandler]
                public static Task<Result> OnMessage(Message msg)
                {
                    _ = new TelegramBotClient("123456:ABC-DEF1234ghIkl-zyx57W2v1u123ew11");
                    return Task.FromResult(Result.StopRouting);
                }
            }
            """);

        Assert.IsTrue(GeneratorTestHarness.HasId(diags, "CUR054"), $"expected CUR054, got: {GeneratorTestHarness.Format(diags)}");
    }

    [TestMethod]
    public void Handler_CleanAsyncCode_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diags = GeneratorTestHarness.RunGenerator("""
            using PolyBot.Attributes;
            using PolyBot.Routing;
            using System.Threading.Tasks;
            using Telegram.Bot;
            using Telegram.Bot.Types;

            public static class Handlers
            {
                [MessageHandler]
                public static async Task<Result> OnMessage(Message msg, ITelegramBotClient client)
                {
                    User me = await client.GetMe();
                    await client.SendMessage(msg.Chat, me.Username ?? "bot");
                    return Result.StopRouting;
                }
            }
            """);

        Assert.IsFalse(GeneratorTestHarness.HasId(diags, "CUR053"), $"unexpected CUR053: {GeneratorTestHarness.Format(diags)}");
        Assert.IsFalse(GeneratorTestHarness.HasId(diags, "CUR054"), $"unexpected CUR054: {GeneratorTestHarness.Format(diags)}");
        Assert.IsFalse(GeneratorTestHarness.HasId(diags, "CUR055"), $"unexpected CUR055: {GeneratorTestHarness.Format(diags)}");
    }
}
