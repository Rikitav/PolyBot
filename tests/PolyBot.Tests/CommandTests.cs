using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Polling;
using Telegram.Bot.Requests;

namespace PolyBot.Tests;

[TestClass]
public sealed class CommandTests
{
    private static List<string> SentTexts(TestHostHandle host)
    {
        return host.Client.SentRequests.OfType<SendMessageRequest>().Select(static r => r.Text).ToList();
    }

    [TestMethod]
    public async Task Start_ReplyContainsKeyedAndPlainServiceNames()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(1, "/start", 100, 200), CancellationToken.None);

        List<string> texts = SentTexts(host);
        Assert.HasCount(1, texts);
        StringAssert.Contains(texts[0], "keyed-impl");
        StringAssert.Contains(texts[0], "welcome-impl");
    }

    [TestMethod]
    public async Task PlainText_FallsThroughToEcho()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, TestHost.MessageUpdate(2, "hello curator", 100, 200), CancellationToken.None);

        CollectionAssert.AreEqual(new List<string> { "echo:hello curator" }, SentTexts(host));
    }

    [TestMethod]
    public async Task BotUsernameSuffix_MatchingAcceptedWrongRejected()
    {
        TestHostHandle host = TestHost.BuildHost("TestBot");
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(3, "/start@TestBot", 100, 200), CancellationToken.None);
        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(4, "/start@WrongBot", 100, 200), CancellationToken.None);

        List<string> texts = SentTexts(host);
        Assert.HasCount(2, texts);
        StringAssert.Contains(texts[0], "start:");
        Assert.AreEqual("echo:/start@WrongBot", texts[1]);
    }

    [TestMethod]
    public async Task CustomPrefixes_MatchPlainText()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, TestHost.MessageUpdate(5, "!hi", 100, 200), CancellationToken.None);
        await host.Router.HandleUpdateAsync(host.Client, TestHost.MessageUpdate(6, "time", 100, 200), CancellationToken.None);

        CollectionAssert.AreEqual(new List<string> { "hi there", "time now" }, SentTexts(host));
    }

    [TestMethod]
    public async Task CommandTextWithoutEntity_DoesNotMatch()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, TestHost.MessageUpdate(7, "/start", 100, 200), CancellationToken.None);

        CollectionAssert.AreEqual(new List<string> { "echo:/start" }, SentTexts(host));
    }

    [TestMethod]
    public async Task Boom_ThrowsOutOfTheRouter()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(8, "/boom", 100, 200), CancellationToken.None));
        Assert.HasCount(0, SentTexts(host));
    }

    [TestMethod]
    public async Task ExceptionHandler_ReceivesUpdateErrorsAndReplies()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleErrorAsync(
            host.Client,
            new InvalidOperationException("boom"),
            HandleErrorSource.HandleUpdateError,
            CancellationToken.None);

        List<string> texts = SentTexts(host);
        Assert.HasCount(1, texts);
        StringAssert.Contains(texts[0], "error:InvalidOperationException");
    }

    [TestMethod]
    public async Task PositionalConstructorAlias_RoutesLikeAliasesArray()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(9, "/pos", 100, 200), CancellationToken.None);
        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(10, "/positional", 100, 200), CancellationToken.None);

        CollectionAssert.AreEqual(new List<string> { "positional", "positional" }, SentTexts(host));
    }

    [TestMethod]
    public async Task CommandWithoutDescription_RoutesNormally()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(11, "/nodesc", 100, 200), CancellationToken.None);

        CollectionAssert.AreEqual(new List<string> { "nodesc" }, SentTexts(host));
    }
}
