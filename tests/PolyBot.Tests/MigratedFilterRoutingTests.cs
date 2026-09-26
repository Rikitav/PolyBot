using Telegram.Bot.Requests;
using Telegram.Bot.Types;

namespace PolyBot.Tests;

/// <summary>
/// Routing integration for migrated <c>PolyBot.Filters</c> and parameterized filter wrappers:
/// ctor arguments captured from handler attributes and from <c>With*</c> await extensions.
/// </summary>
[TestClass]
public sealed class MigratedFilterRoutingTests
{
    private static List<string> SentTexts(TestHostHandle host)
    {
        return host.Client.SentRequests.OfType<SendMessageRequest>().Select(static r => r.Text).ToList();
    }

    [TestMethod]
    public async Task AttributeArgs_FiresOnlyOnExactText()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using var _ = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, TestHost.MessageUpdate(1, "filtertrigger", 100, 200), CancellationToken.None);
        CollectionAssert.Contains(SentTexts(host), "filter-trigger");

        await host.Router.HandleUpdateAsync(host.Client, TestHost.MessageUpdate(2, "filtertrigge", 100, 200), CancellationToken.None);
        Assert.AreEqual(1, SentTexts(host).Count(static t => t == "filter-trigger"));
    }

    [TestMethod]
    public async Task NamedOptionalArg_OrdinalComparisonIsCaseSensitive()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using var _ = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, TestHost.MessageUpdate(1, "prefix", 100, 200), CancellationToken.None);
        CollectionAssert.Contains(SentTexts(host), "ordinal-prefix");

        await host.Router.HandleUpdateAsync(host.Client, TestHost.MessageUpdate(2, "Prefix", 100, 200), CancellationToken.None);
        Assert.AreEqual(1, SentTexts(host).Count(static t => t == "ordinal-prefix"));
    }

    [TestMethod]
    public async Task ParameterlessFilter_FiresOnlyForBotSender()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using var _ = host.Provider;

        Update fromBot = TestHost.MessageUpdate(1, "hello", 100, 200);
        fromBot.Message!.From = new User { Id = 999, FirstName = "Bot", IsBot = true };
        await host.Router.HandleUpdateAsync(host.Client, fromBot, CancellationToken.None);
        CollectionAssert.Contains(SentTexts(host), "from-bot");

        await host.Router.HandleUpdateAsync(host.Client, TestHost.MessageUpdate(2, "hello", 100, 200), CancellationToken.None);
        Assert.AreEqual(1, SentTexts(host).Count(static t => t == "from-bot"));
    }

    [TestMethod]
    public async Task WithFilterArgs_AwaitCompletesOnlyForMatchingText()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using var _ = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(1, "/filterawait", 100, 200), CancellationToken.None);

        // Non-matching text does not claim the await.
        await host.Router.HandleUpdateAsync(host.Client, TestHost.MessageUpdate(2, "something else", 100, 200), CancellationToken.None);
        Assert.AreEqual(0, SentTexts(host).Count(static t => t.StartsWith("filter-await:", StringComparison.Ordinal)));

        // Matching text completes it; the suspended continuation resumes asynchronously.
        await host.Router.HandleUpdateAsync(host.Client, TestHost.MessageUpdate(3, "awaittrigger", 100, 200), CancellationToken.None);
        bool completed = await TestHost.WaitForAsync(() => SentTexts(host).Contains("filter-await:awaittrigger"));
        Assert.IsTrue(completed, "expected the await to complete with the matching message");
    }
}
