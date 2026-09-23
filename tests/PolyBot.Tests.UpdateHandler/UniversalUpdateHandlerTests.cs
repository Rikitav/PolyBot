using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PolyBot.Tests.UpdateHandler;

[TestClass]
public sealed class UniversalUpdateHandlerTests
{
    [TestInitialize]
    public void ResetRecorder()
    {
        CallRecorder.Calls.Clear();
        CallRecorder.StopInPreFilter = false;
    }

    [TestMethod]
    public async Task PreFiltersRunBeforeSpecializedHandler_InPriorityOrder()
    {
        using ServiceProvider provider = BuildProvider();
        IUpdateHandler router = provider.GetRequiredService<IUpdateHandler>();
        ITelegramBotClient client = provider.GetRequiredService<ITelegramBotClient>();

        await router.HandleUpdateAsync(client, MessageUpdate(1, "hello"), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "audit", "audit2", "message" }, CallRecorder.Calls);
    }

    [TestMethod]
    public async Task StopRoutingInPreFilterHaltsSpecializedHandler()
    {
        CallRecorder.StopInPreFilter = true;
        using ServiceProvider provider = BuildProvider();
        IUpdateHandler router = provider.GetRequiredService<IUpdateHandler>();
        ITelegramBotClient client = provider.GetRequiredService<ITelegramBotClient>();

        await router.HandleUpdateAsync(client, MessageUpdate(2, "hello"), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "audit" }, CallRecorder.Calls);
    }

    [TestMethod]
    public async Task FallbackRunsForUpdateTypeWithoutHandlers()
    {
        using ServiceProvider provider = BuildProvider();
        IUpdateHandler router = provider.GetRequiredService<IUpdateHandler>();
        ITelegramBotClient client = provider.GetRequiredService<ITelegramBotClient>();

        await router.HandleUpdateAsync(client, PollUpdate(3), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "audit", "audit2", "fallback" }, CallRecorder.Calls);
    }

    [TestMethod]
    public async Task FallbackNotReachedWhenSwitchHandlerStopsRouting()
    {
        using ServiceProvider provider = BuildProvider();
        IUpdateHandler router = provider.GetRequiredService<IUpdateHandler>();
        ITelegramBotClient client = provider.GetRequiredService<ITelegramBotClient>();

        await router.HandleUpdateAsync(client, MessageUpdate(4, "hello"), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "audit", "audit2", "message" }, CallRecorder.Calls);
    }

    [TestMethod]
    public void UniversalHandler_InfersAllAllowedUpdates()
    {
        using ServiceProvider provider = BuildProvider();

        UpdateType[] allowed = provider.GetRequiredService<IAllowedUpdatesProvider>().AllowedUpdates;

        Assert.IsTrue(allowed.Length > 10, "expected the full update-type set, got: " + string.Join(", ", allowed));
        CollectionAssert.Contains(allowed, UpdateType.Message);
        CollectionAssert.Contains(allowed, UpdateType.Poll);
        CollectionAssert.Contains(allowed, UpdateType.ChatMember);
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
}
