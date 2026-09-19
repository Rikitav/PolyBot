using Microsoft.Extensions.DependencyInjection;
using PolyBot.Tests.Fixtures;
using System.Reflection;
using Telegram.Bot.Requests;

namespace PolyBot.Tests;

[TestClass]
public sealed class FilterTests
{
    private static List<string> SentTexts(TestHostHandle host)
    {
        return host.Client.SentRequests.OfType<SendMessageRequest>().Select(static r => r.Text).ToList();
    }

    [TestMethod]
    public async Task AllowAllFilter_Passes()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(1, "/allow", 100, 200), CancellationToken.None);

        CollectionAssert.AreEqual(new List<string> { "allowed" }, SentTexts(host));
    }

    [TestMethod]
    public async Task DenyAllFilter_VetoesAndFallsThroughToEcho()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(2, "/deny", 100, 200), CancellationToken.None);

        CollectionAssert.AreEqual(new List<string> { "echo:/deny" }, SentTexts(host));
    }

    [TestMethod]
    public async Task DiConstructedFilter_PassesOnceThenVetoes()
    {
        TestHostHandle host = TestHost.BuildHost(services =>
        {
            services.AddSingleton<IGateService, GateService>();
            services.AddSingleton<DiGateFilter>();
        });
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(3, "/gate", 100, 200), CancellationToken.None);
        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(4, "/gate", 100, 200), CancellationToken.None);

        CollectionAssert.AreEqual(new List<string> { "gate:open", "echo:/gate" }, SentTexts(host));
    }

    [TestMethod]
    public async Task ReferencedAssemblyFilter_WrapperComesFromTheLibraryAndGatesRouting()
    {
        TestHostHandle host = TestHost.BuildHost();
        await host.Router.HandleUpdateAsync(host.Client, TestHost.MessageUpdate(1, "with MAGICWORD payload", 5001, 5002), CancellationToken.None);
        await host.Router.HandleUpdateAsync(host.Client, TestHost.MessageUpdate(2, "nothing here", 5001, 5002), CancellationToken.None);
        CollectionAssert.AreEqual(new List<string> { "fixture-pass", "echo:nothing here" }, SentTexts(host));
    }

    [TestMethod]
    public async Task ReferencedAssemblyWithExtension_ResolvesSemanticallyInAwaitChains()
    {
        TestHostHandle host = TestHost.BuildHost();
        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(10, "/fixtureawait", 5003, 5004), CancellationToken.None);
        await host.Router.HandleUpdateAsync(host.Client, TestHost.MessageUpdate(11, "contains magicword payload", 5003, 5004), CancellationToken.None);
        bool resumed = await TestHost.WaitForAsync(() => SentTexts(host).Contains("fixture-await:contains magicword payload"));
        Assert.IsTrue(resumed);
    }

    [TestMethod]
    public void LibraryAssembly_ContributesWrappersButNoRoutingInfrastructure()
    {
        Assembly fixtureAssembly = typeof(FixtureFilter).Assembly;
        Assert.IsNotNull(fixtureAssembly.GetType("PolyBot.Attributes.FixtureFilterAttribute"));
        Assert.IsNotNull(fixtureAssembly.GetType("PolyBot.Attributes.PolyBotAwaiterExtensions"));
        Assert.IsNull(fixtureAssembly.GetType("PolyBot.BotRouter"));
        Assert.IsNull(fixtureAssembly.GetType("PolyBot.PolyBotExtensions"));
        Assert.IsNull(fixtureAssembly.GetType("PolyBot.PolyBotBotFatherSync"));
    }
}
