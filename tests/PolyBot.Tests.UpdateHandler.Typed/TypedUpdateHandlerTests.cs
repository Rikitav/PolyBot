using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PolyBot.Tests.UpdateHandler.Typed;

[TestClass]
public sealed class TypedUpdateHandlerTests
{
    [TestInitialize]
    public void ResetRecorder()
    {
        CallRecorder.Calls.Clear();
    }

    [TestMethod]
    public async Task TypedHandlerReceivesFirstDeclaredType()
    {
        using ServiceProvider provider = BuildProvider();
        IUpdateHandler router = provider.GetRequiredService<IUpdateHandler>();
        ITelegramBotClient client = provider.GetRequiredService<ITelegramBotClient>();

        await router.HandleUpdateAsync(client, PollUpdate(1), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "multi:" + UpdateType.Poll }, CallRecorder.Calls);
    }

    [TestMethod]
    public async Task TypedHandlerReceivesSecondDeclaredType()
    {
        using ServiceProvider provider = BuildProvider();
        IUpdateHandler router = provider.GetRequiredService<IUpdateHandler>();
        ITelegramBotClient client = provider.GetRequiredService<ITelegramBotClient>();

        await router.HandleUpdateAsync(client, InlineQueryUpdate(2), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "multi:" + UpdateType.InlineQuery }, CallRecorder.Calls);
    }

    [TestMethod]
    public async Task TypedHandlerNotInvokedForUndeclaredType()
    {
        using ServiceProvider provider = BuildProvider();
        IUpdateHandler router = provider.GetRequiredService<IUpdateHandler>();
        ITelegramBotClient client = provider.GetRequiredService<ITelegramBotClient>();

        await router.HandleUpdateAsync(client, MessageUpdate(3, "hello"), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "message" }, CallRecorder.Calls);
    }

    [TestMethod]
    public void TypedHandler_InfersExactlyTheDeclaredTypes()
    {
        using ServiceProvider provider = BuildProvider();

        UpdateType[] allowed = provider.GetRequiredService<IAllowedUpdatesProvider>().AllowedUpdates;

        UpdateType[] expected = new[] { UpdateType.Message, UpdateType.InlineQuery, UpdateType.Poll }
            .OrderBy(t => (int)t)
            .ToArray();
        CollectionAssert.AreEqual(expected, allowed);
    }

    private static ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        services.AddPolyBotRouter();
        services.AddSingleton<ITelegramBotClient>(new PolyTests());
        return services.BuildServiceProvider();
    }

    private static Update MessageUpdate(int id, string text)
    {
        return new Update
        {
            Id = id,
            Message = new Message
            {
                Id = id,
                Text = text,
                From = new User { Id = 1, FirstName = "Test" },
                Chat = new Chat { Id = 1, Type = ChatType.Private },
            },
        };
    }

    private static Update PollUpdate(int id)
    {
        return new Update
        {
            Id = id,
            Poll = new Poll
            {
                Id = "poll-" + id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Question = "Q?",
                Options = [],
                TotalVoterCount = 0,
                IsClosed = false,
                IsAnonymous = false,
                Type = PollType.Regular,
            },
        };
    }

    private static Update InlineQueryUpdate(int id)
    {
        return new Update
        {
            Id = id,
            InlineQuery = new InlineQuery
            {
                Id = "query-" + id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                From = new User { Id = 1, FirstName = "Test" },
                Query = "find something",
                Offset = string.Empty,
            },
        };
    }
}
