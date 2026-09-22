using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PolyBot.Attributes;
using PolyBot.Routing;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PolyBot.Tests.AllowedUpdates.IncludeAll;

public sealed class IncludeAllHandlers
{
    [MessageHandler]
    public static Task<Result> MessageHandler(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        return Task.FromResult(Result.Handled());
    }
}

[TestClass]
public sealed class IncludeAllTests
{
    [TestMethod]
    public void IncludeAll_EmitsEmptyAllowedUpdates()
    {
        ServiceCollection services = new();
        services.AddPolyBotRouter();
        services.AddSingleton<ITelegramBotClient>(new PolyTests());
        using ServiceProvider provider = services.BuildServiceProvider();

        UpdateType[] allowed = provider.GetRequiredService<IAllowedUpdatesProvider>().AllowedUpdates;

        Assert.AreEqual(0, allowed.Length);
    }
}
