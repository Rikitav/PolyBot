using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Requests;

namespace PolyBot.Tests;

[TestClass]
public sealed class AwaitTests
{
    private static List<string> SentTexts(TestHostHandle host)
    {
        return host.Client.SentRequests.OfType<SendMessageRequest>().Select(static r => r.Text).ToList();
    }

    [TestMethod]
    public async Task Register_NonMatchingUpdateRoutesNormallyThenMatchingResumes()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(1, "/register", 777, 700), CancellationToken.None);
        await host.Router.HandleUpdateAsync(host.Client, TestHost.MessageUpdate(2, "my age is", 777, 700), CancellationToken.None);
        CollectionAssert.AreEqual(new List<string> { "echo:my age is" }, SentTexts(host));

        await host.Router.HandleUpdateAsync(host.Client, TestHost.MessageUpdate(3, "my age is 7", 777, 700), CancellationToken.None);

        bool resumed = await TestHost.WaitForAsync(() => SentTexts(host).Contains("register:my age is 7", StringComparer.Ordinal));
        Assert.IsTrue(resumed, "the matching update was claimed by the awaiter and resumed the workflow");
        CollectionAssert.AreEqual(new List<string> { "echo:my age is", "register:my age is 7" }, SentTexts(host));
    }

    [TestMethod]
    public async Task Fast_CompletesWithNullAfterTimeout()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(1, "/fast", 778, 701), CancellationToken.None);

        bool timedOut = await TestHost.WaitForAsync(() => SentTexts(host).Contains("fast:timeout", StringComparer.Ordinal));
        Assert.IsTrue(timedOut, "/fast completed with null after the 80 ms timeout");
        CollectionAssert.AreEqual(new List<string> { "fast:timeout" }, SentTexts(host));
    }

    [TestMethod]
    public async Task Room_CrossUserSameChatDeliveryClaimsTheUpdate()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(1, "/room", 888, 300), CancellationToken.None);
        await host.Router.HandleUpdateAsync(host.Client, TestHost.MessageUpdate(2, "knock knock", 999, 300), CancellationToken.None);

        bool resumed = await TestHost.WaitForAsync(() => SentTexts(host).Contains("room:knock knock", StringComparer.Ordinal));
        Assert.IsTrue(resumed, "a message from another user in the same chat resumed the chat-keyed await");
        CollectionAssert.AreEqual(new List<string> { "room:knock knock" }, SentTexts(host));
    }

    [TestMethod]
    public async Task Room_WhereRejectedUpdateRoutesNormallyAndAwaitStaysPending()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(1, "/room", 888, 300), CancellationToken.None);
        await host.Router.HandleUpdateAsync(host.Client, TestHost.MessageUpdate(2, "hi", 888, 300), CancellationToken.None);

        CollectionAssert.AreEqual(new List<string> { "echo:hi" }, SentTexts(host));

        await host.Router.HandleUpdateAsync(host.Client, TestHost.MessageUpdate(3, "knock knock", 888, 300), CancellationToken.None);

        bool resumed = await TestHost.WaitForAsync(() => SentTexts(host).Contains("room:knock knock", StringComparer.Ordinal));
        Assert.IsTrue(resumed, "after the Where-rejected update routed normally, a passing update still resumed the workflow");
        CollectionAssert.AreEqual(new List<string> { "echo:hi", "room:knock knock" }, SentTexts(host));
    }
}
