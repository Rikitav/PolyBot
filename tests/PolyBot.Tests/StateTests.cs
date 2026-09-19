using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Requests;

namespace PolyBot.Tests;

[TestClass]
public sealed class StateTests
{
    private static List<string> SentTexts(TestHostHandle host)
    {
        return host.Client.SentRequests.OfType<SendMessageRequest>().Select(static r => r.Text).ToList();
    }

    [TestMethod]
    public async Task FsmWalk_NoStateThenAwaitingAgeThenCompleted()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;
        IStateStorage storage = provider.GetRequiredService<IStateStorage>();

        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(1, "/fsm", 100, 200), CancellationToken.None);
        RegistrationStep? afterFirst = await storage.GetStateAsync<RegistrationStep>("100");
        CollectionAssert.AreEqual(new List<string> { "nostate" }, SentTexts(host));
        Assert.AreEqual(RegistrationStep.AwaitingAge, afterFirst);

        await host.Router.HandleUpdateAsync(host.Client, TestHost.MessageUpdate(2, "twenty five", 100, 200), CancellationToken.None);
        RegistrationStep? afterSecond = await storage.GetStateAsync<RegistrationStep>("100");
        CollectionAssert.AreEqual(new List<string> { "nostate", "state:AwaitingAge" }, SentTexts(host));
        Assert.AreEqual(RegistrationStep.Completed, afterSecond);

        await host.Router.HandleUpdateAsync(host.Client, TestHost.MessageUpdate(3, "again", 100, 200), CancellationToken.None);
        RegistrationStep? afterThird = await storage.GetStateAsync<RegistrationStep>("100");
        CollectionAssert.AreEqual(new List<string> { "nostate", "state:AwaitingAge", "echo:again" }, SentTexts(host));
        Assert.AreEqual(RegistrationStep.Completed, afterThird);
    }

    [TestMethod]
    public async Task PollUpdate_DoesNotCrashStateFiltersAndRepliesNothing()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, TestHost.PollUpdate(1), CancellationToken.None);

        Assert.HasCount(0, host.Client.SentRequests);
    }

    [TestMethod]
    public async Task AdvanceAsync_WalksForwardAndClearsPastTheLast()
    {
        MemoryStateStorage storage = new();
        UpdateContextAccessor accessor = new()
        {
            Update = TestHost.MessageUpdate(10, "adv", 555, 556),
        };
        StateMachine fsm = new(storage, accessor);

        RegistrationStep? first = await fsm.AdvanceAsync<RegistrationStep>();
        RegistrationStep? second = await fsm.AdvanceAsync<RegistrationStep>();
        RegistrationStep? third = await fsm.AdvanceAsync<RegistrationStep>();
        RegistrationStep? afterClear = await storage.GetStateAsync<RegistrationStep>("555");

        Assert.AreEqual(RegistrationStep.AwaitingAge, first);
        Assert.AreEqual(RegistrationStep.Completed, second);
        Assert.IsNull(third);
        Assert.IsNull(afterClear);
    }

    [TestMethod]
    public async Task RollbackAsync_WalksBackAndClearsPastTheFirst()
    {
        MemoryStateStorage storage = new();
        UpdateContextAccessor accessor = new()
        {
            Update = TestHost.MessageUpdate(11, "rb", 557, 558),
        };
        StateMachine fsm = new(storage, accessor);

        await fsm.SetAsync(RegistrationStep.Completed);
        RegistrationStep? first = await fsm.RollbackAsync<RegistrationStep>();
        RegistrationStep? second = await fsm.RollbackAsync<RegistrationStep>();
        RegistrationStep? third = await fsm.RollbackAsync<RegistrationStep>();

        Assert.AreEqual(RegistrationStep.AwaitingAge, first);
        Assert.IsNull(second);
        Assert.IsNull(third);
    }

    [TestMethod]
    public async Task MemoryStateStorage_ExpiredStatesReadAsNull()
    {
        MemoryStateStorage storage = new(TimeSpan.FromMilliseconds(60));

        await storage.SetStateAsync("k", RegistrationStep.AwaitingAge);
        await Task.Delay(120);
        RegistrationStep? expired = await storage.GetStateAsync<RegistrationStep>("k");

        Assert.IsNull(expired);
    }

    [TestMethod]
    public async Task MemoryStateStorage_EnumKeysNeverCollide()
    {
        MemoryStateStorage storage = new();

        await storage.SetStateAsync("same-key", RegistrationStep.AwaitingAge);
        await storage.SetStateAsync("same-key", AltState.Beta);
        RegistrationStep? step = await storage.GetStateAsync<RegistrationStep>("same-key");
        AltState? alt = await storage.GetStateAsync<AltState>("same-key");

        Assert.AreEqual(RegistrationStep.AwaitingAge, step);
        Assert.AreEqual(AltState.Beta, alt);
    }

    private enum AltState
    {
        Alpha,
        Beta,
    }
}
