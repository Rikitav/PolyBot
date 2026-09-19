using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Requests;

namespace PolyBot.Tests;

[TestClass]
public sealed class ArgumentTests
{
    private static List<string> SentTexts(TestHostHandle host)
    {
        return host.Client.SentRequests.OfType<SendMessageRequest>().Select(static r => r.Text).ToList();
    }

    [TestMethod]
    public async Task Add_ParsesBothIntArguments()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(1, "/add 5 3", 100, 200), CancellationToken.None);

        CollectionAssert.AreEqual(new List<string> { "8" }, SentTexts(host));
    }

    [TestMethod]
    public async Task Add_UnparsableTokenThrows()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        CommandArgsParseException ex = await Assert.ThrowsExactlyAsync<CommandArgsParseException>(
            () => host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(2, "/add abc", 100, 200), CancellationToken.None));
        StringAssert.Contains(ex.Message, "cannot parse");
    }

    [TestMethod]
    public async Task Greet_WithoutTokenBindsNull()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(3, "/greet", 100, 200), CancellationToken.None);

        CollectionAssert.AreEqual(new List<string> { "greet:<null>" }, SentTexts(host));
    }

    [TestMethod]
    public async Task Greet_WithTokenBindsName()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(4, "/greet Bob", 100, 200), CancellationToken.None);

        CollectionAssert.AreEqual(new List<string> { "greet:Bob" }, SentTexts(host));
    }

    [TestMethod]
    public async Task Say_CapturesMultiWordRestVerbatim()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(5, "/say 2 hello world 42", 100, 200), CancellationToken.None);

        CollectionAssert.AreEqual(new List<string> { "say:2:hello world 42" }, SentTexts(host));
    }

    [TestMethod]
    public async Task Say_EmptyRestWhenNothingFollows()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(6, "/say 2", 100, 200), CancellationToken.None);

        CollectionAssert.AreEqual(new List<string> { "say:2:" }, SentTexts(host));
    }

    [TestMethod]
    public async Task Note_RestOnlyCapturesWholeRemainder()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(7, "/note buy milk", 100, 200), CancellationToken.None);

        CollectionAssert.AreEqual(new List<string> { "note:buy milk" }, SentTexts(host));
    }

    [TestMethod]
    public async Task Parse_ScansPatternAndRestClaimsTheTail()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(8, "/parse 7 abc 42 hello world", 100, 200), CancellationToken.None);

        CollectionAssert.AreEqual(new List<string> { "parse:7:42:hello world" }, SentTexts(host));
    }

    [TestMethod]
    public async Task Parse_NoTwoDigitRunThrows()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        CommandArgsParseException ex = await Assert.ThrowsExactlyAsync<CommandArgsParseException>(
            () => host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(9, "/parse 7 abcdef", 100, 200), CancellationToken.None));
        StringAssert.Contains(ex.Message, "does not match pattern");
    }

    [TestMethod]
    public async Task Ping_WithTrailingTokenThrows()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        CommandArgsParseException ex = await Assert.ThrowsExactlyAsync<CommandArgsParseException>(
            () => host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(10, "/ping now", 100, 200), CancellationToken.None));
        StringAssert.Contains(ex.Message, "found unwanted arguments");
    }

    [TestMethod]
    public async Task Ping_WithoutArgumentsMatches()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(11, "/ping", 100, 200), CancellationToken.None);

        CollectionAssert.AreEqual(new List<string> { "pong" }, SentTexts(host));
    }
}
