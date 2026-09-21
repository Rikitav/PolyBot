using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Requests;
using Telegram.Bot.Types.Enums;

namespace PolyBot.Tests;

[TestClass]
public sealed class AllowedUpdatesTests
{
    [TestMethod]
    public async Task BotRouter_AllowedUpdates_InferredFromHandlersAndAwaits()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        UpdateType[] allowed = provider.GetRequiredService<IAllowedUpdatesProvider>().AllowedUpdates;

        CollectionAssert.Contains(allowed, UpdateType.Message);
        CollectionAssert.Contains(allowed, UpdateType.CallbackQuery);
        CollectionAssert.DoesNotContain(allowed, UpdateType.InlineQuery);
        CollectionAssert.DoesNotContain(allowed, UpdateType.Unknown);
    }

    [TestMethod]
    public async Task BotRouter_AllowedUpdates_ExposedAsStaticArray()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        UpdateType[] fromProvider = provider.GetRequiredService<IAllowedUpdatesProvider>().AllowedUpdates;
        UpdateType[] fromRouter = BotRouter.AllowedUpdates;

        CollectionAssert.AreEqual(fromRouter, fromProvider);
    }

    [TestMethod]
    public void HostedPolling_UsesInferredAllowedUpdates()
    {
        PolyTests client = new();
        ServiceCollection services = new();
        services.AddPolyBotRouter();
        services.AddSingleton<ITelegramBotClient>(client);
        services.AddPolyBotHostedPolling();
        using ServiceProvider provider = services.BuildServiceProvider();

        ReceiverOptions receiverOptions = provider.GetRequiredService<ReceiverOptions>();

        CollectionAssert.AreEqual(BotRouter.AllowedUpdates, receiverOptions.AllowedUpdates);
    }

    [TestMethod]
    public void HostedPolling_ExplicitAllowedUpdatesPreserved()
    {
        PolyTests client = new();
        ServiceCollection services = new();
        services.AddPolyBotRouter();
        services.AddSingleton<ITelegramBotClient>(client);
        services.AddPolyBotHostedPolling(static options => options.AllowedUpdates = new[] { UpdateType.InlineQuery });
        using ServiceProvider provider = services.BuildServiceProvider();

        ReceiverOptions receiverOptions = provider.GetRequiredService<ReceiverOptions>();

        CollectionAssert.AreEqual(new[] { UpdateType.InlineQuery }, receiverOptions.AllowedUpdates);
    }

    [TestMethod]
    public async Task Webhook_UsesInferredAllowedUpdates()
    {
        PolyTests client = new();
        ServiceCollection services = new();
        services.AddPolyBotDefaults();
        services.AddSingleton<ITelegramBotClient>(client);
        services.AddPolyBotWebhook(static options =>
        {
            options.WebhookUrl = "https://example.com/bot";
            options.WebhookSecretToken = "s3cr3t";
        });
        await using ServiceProvider provider = services.BuildServiceProvider();

        IHostedService hosted = provider.GetRequiredService<IHostedService>();
        await hosted.StartAsync(CancellationToken.None);
        await hosted.StopAsync(CancellationToken.None);

        SetWebhookRequest set = client.SentRequests.OfType<SetWebhookRequest>().Single();
        CollectionAssert.AreEqual(BotRouter.AllowedUpdates, set.AllowedUpdates?.ToArray());
    }

    [TestMethod]
    public async Task Webhook_ExplicitAllowedUpdatesPreserved()
    {
        PolyTests client = new();
        ServiceCollection services = new();
        services.AddPolyBotDefaults();
        services.AddSingleton<ITelegramBotClient>(client);
        services.AddPolyBotWebhook(static options =>
        {
            options.WebhookUrl = "https://example.com/bot";
            options.WebhookSecretToken = "s3cr3t";
            options.AllowedUpdates = new[] { UpdateType.InlineQuery };
        });
        await using ServiceProvider provider = services.BuildServiceProvider();

        IHostedService hosted = provider.GetRequiredService<IHostedService>();
        await hosted.StartAsync(CancellationToken.None);
        await hosted.StopAsync(CancellationToken.None);

        SetWebhookRequest set = client.SentRequests.OfType<SetWebhookRequest>().Single();
        CollectionAssert.AreEqual(new[] { UpdateType.InlineQuery }, set.AllowedUpdates?.ToArray());
    }

    [TestMethod]
    public void Client_RunTest_UsesInferredAllowedUpdates()
    {
        PolyBotClient client = new();
        client.Services.AddPolyBotRouter();
        client.Services.AddSingleton<ITelegramBotClient>(new PolyTests());

        UpdateMocker mocker = client.RunTest();
        PolyBotOptions options = mocker.Services.GetRequiredService<PolyBotOptions>();

        CollectionAssert.AreEqual(BotRouter.AllowedUpdates, options.AllowedUpdates);
    }
}
