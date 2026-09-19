using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;

namespace PolyBot.Tests;

// Throttle gates are static per generated handler type, so tests need fresh user ids.
[TestClass]
[DoNotParallelize]
public sealed class ThrottleTests
{
    private static int s_nextUserId = 100000;

    private static long NextUserId()
    {
        return System.Threading.Interlocked.Increment(ref s_nextUserId);
    }

    private static List<string> SentTexts(TestHostHandle host)
    {
        return host.Client.SentRequests.OfType<SendMessageRequest>().Select(static r => r.Text).ToList();
    }

    [TestMethod]
    public async Task Window_FallthroughThenExpiry()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;
        long userId = NextUserId();

        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(1, "/window", userId, 801), CancellationToken.None);
        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(2, "/window", userId, 801), CancellationToken.None);
        await Task.Delay(300);
        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(3, "/window", userId, 801), CancellationToken.None);

        CollectionAssert.AreEqual(new List<string> { "window", "echo:/window", "window" }, SentTexts(host));
    }

    [TestMethod]
    public async Task Burst_ConcurrentCallsCappedAtTheLimit()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;
        long userId = NextUserId();

        Update[] burst = new Update[10];
        for (int i = 0; i < burst.Length; i++)
        {
            burst[i] = TestHost.CommandUpdate(10 + i, "/burst", userId, 802);
        }

        await Task.WhenAll(System.Linq.Enumerable.Select(burst, update => host.Router.HandleUpdateAsync(host.Client, update, CancellationToken.None)));

        List<string> texts = SentTexts(host);
        Assert.AreEqual(5, texts.Count(static t => t == "burst"));
        Assert.AreEqual(5, texts.Count(static t => t == "echo:/burst"));
    }

    [TestMethod]
    public async Task Halted_IgnoreHaltsTheBranchWithNoReplyAtAll()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;
        long userId = NextUserId();

        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(1, "/halted", userId, 803), CancellationToken.None);
        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(2, "/halted", userId, 803), CancellationToken.None);

        CollectionAssert.AreEqual(new List<string> { "halted" }, SentTexts(host));
    }
}
