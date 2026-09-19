using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PolyBot.Tests;

[TestClass]
public sealed class PatternTests
{
    private static Update CallbackUpdate(int id, string data, long userId, long chatId)
    {
        return new Update
        {
            Id = id,
            CallbackQuery = new CallbackQuery
            {
                Id = "cb-" + id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                From = new User { Id = userId, FirstName = "Pattern" },
                Data = data,
                Message = new Message { Id = id, Chat = new Chat { Id = chatId, Type = ChatType.Private } },
            },
        };
    }

    private static List<string> SentTexts(TestHostHandle host)
    {
        return host.Client.SentRequests.OfType<SendMessageRequest>().Select(static r => r.Text).ToList();
    }

    [TestMethod]
    public async Task ItemPattern_ExtractsTypedCaptures()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, CallbackUpdate(1, "item:42:delete", 43, 44), CancellationToken.None);

        CollectionAssert.AreEqual(new List<string> { "item:42:delete" }, SentTexts(host));
    }

    [TestMethod]
    public async Task ItemPattern_NonIntSegmentFallsThrough()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, CallbackUpdate(1, "item:abc:delete", 43, 44), CancellationToken.None);

        Assert.HasCount(0, host.Client.SentRequests.OfType<SendMessageRequest>());
        Assert.HasCount(1, host.Client.SentRequests.OfType<AnswerCallbackQueryRequest>());
    }

    [TestMethod]
    public async Task LogsPattern_WildcardCapturesTheRest()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, CallbackUpdate(1, "logs:a/b", 43, 44), CancellationToken.None);

        CollectionAssert.AreEqual(new List<string> { "logs:a/b" }, SentTexts(host));
    }

    [TestMethod]
    public async Task PayPattern_DecimalCapturedInvariantly()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, CallbackUpdate(1, "pay:19.99", 43, 44), CancellationToken.None);

        CollectionAssert.AreEqual(new List<string> { "pay:19.99" }, SentTexts(host));
    }

    [TestMethod]
    public async Task PayPattern_UnparsableAmountFallsThrough()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, CallbackUpdate(1, "pay:oops", 43, 44), CancellationToken.None);

        Assert.HasCount(0, host.Client.SentRequests.OfType<SendMessageRequest>());
        Assert.HasCount(1, host.Client.SentRequests.OfType<AnswerCallbackQueryRequest>());
    }
}
