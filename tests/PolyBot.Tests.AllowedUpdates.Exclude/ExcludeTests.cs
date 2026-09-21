using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PolyBot.Attributes;
using PolyBot.Routing;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PolyBot.Tests.AllowedUpdates.Exclude;

public sealed class ExcludeHandlers
{
    [MessageHandler]
    public static Task<Result> MessageHandler(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        return Task.FromResult(Result.Handled());
    }

    [CallbackQueryHandler]
    public static Task<Result> CallbackQueryHandler(CallbackQuery query, ITelegramBotClient bot, CancellationToken ct)
    {
        return Task.FromResult(Result.Handled());
    }
}

[TestClass]
public sealed class ExcludeTests
{
    [TestMethod]
    public void Exclude_RemovesUpdateTypeFromAllowedUpdates()
    {
        ServiceCollection services = new();
        services.AddPolyBotRouter();
        services.AddSingleton<ITelegramBotClient>(new PolyTests());
        using ServiceProvider provider = services.BuildServiceProvider();

        UpdateType[] allowed = provider.GetRequiredService<IAllowedUpdatesProvider>().AllowedUpdates;

        CollectionAssert.DoesNotContain(allowed, UpdateType.Message);
        CollectionAssert.Contains(allowed, UpdateType.CallbackQuery);
    }
}
