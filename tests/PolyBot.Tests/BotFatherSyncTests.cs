using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Types;

namespace PolyBot.Tests;

[TestClass]
public sealed class BotFatherSyncTests
{
    [TestMethod]
    public void DiscoveredCommands_CarryMetadataAndExcludeHidden()
    {
        TestHostHandle host = TestHost.BuildHost();
        using ServiceProvider provider = host.Provider;

        IBotFatherSync sync = provider.GetRequiredService<IBotFatherSync>();
        List<BotCommand> commands = sync.DiscoveredCommands.ToList();

        Assert.IsTrue(commands.Any(static c => c.Command == "start" && c.Description == "Start the bot"));
        Assert.IsTrue(commands.Any(static c => c.Command == "start" && c.Description == "Запустить бота"));
        Assert.IsTrue(commands.Any(static c => c.Command == "add" && c.Description == "Add two numbers"));
        Assert.IsFalse(commands.Any(static c => c.Command == "secret"));
        Assert.AreEqual(2, commands.Count(static c => c.Command == "start"));
    }
}
