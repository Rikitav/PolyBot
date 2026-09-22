using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PolyBot.Attributes;
using PolyBot.Routing;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PolyBot.Tests.AllowedUpdates.RawUpdate;

public sealed class RawUpdateHandlers
{
    [MessageHandler]
    public static Task<Result> MessageHandler(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        return Task.FromResult(Result.Handled());
    }

    [MessageHandler]
    public static Task<Result> CatchAllHandler(Update update, ITelegramBotClient bot, CancellationToken ct)
    {
        return Task.FromResult(Result.Handled());
    }
}

[TestClass]
public sealed class RawUpdateTests
{
    [TestMethod]
    public void RawUpdateHandler_FallsBackToAllAllowedUpdates()
    {
        ServiceCollection services = new();
        services.AddPolyBotRouter();
        services.AddSingleton<ITelegramBotClient>(new PolyTests());
        using ServiceProvider provider = services.BuildServiceProvider();

        UpdateType[] allowed = provider.GetRequiredService<IAllowedUpdatesProvider>().AllowedUpdates;

        Assert.IsTrue(allowed.Length > 2, "raw Update handler should request all update types");
        CollectionAssert.Contains(allowed, UpdateType.Message);
        CollectionAssert.Contains(allowed, UpdateType.CallbackQuery);
        CollectionAssert.Contains(allowed, UpdateType.InlineQuery);
        CollectionAssert.DoesNotContain(allowed, UpdateType.Unknown);
    }
}
