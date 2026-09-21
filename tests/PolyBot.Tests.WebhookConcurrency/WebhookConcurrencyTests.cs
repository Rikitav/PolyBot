using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PolyBot.Tests.WebhookConcurrency;

[TestClass]
public sealed class WebhookConcurrencyTests
{
    [TestMethod]
    public async Task Webhook_ProcessesUpdatesConcurrently_WhenMaxConcurrentUpdatesIsGreaterThanOne()
    {
        PolyTests client = new();
        ServiceCollection services = new();
        services.AddSingleton<ConcurrencyGate>();
        services.AddPolyBotDefaults();
        services.AddSingleton<ITelegramBotClient>(client);
        services.AddPolyBotWebhook(static options =>
        {
            options.WebhookUrl = "https://example.com/bot";
            options.WebhookMaxConcurrentUpdates = 3;
        });

        await using ServiceProvider provider = services.BuildServiceProvider();
        ConcurrencyGate gate = provider.GetRequiredService<ConcurrencyGate>();

        IHostedService hosted = provider.GetRequiredService<IHostedService>();
        await hosted.StartAsync(CancellationToken.None);

        MethodInfo endpointMethod = typeof(PolyBotWebhookExtensions).GetMethod(
            "HandlePolyBotWebhookAsync",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new AssertFailedException("HandlePolyBotWebhookAsync was not generated");

        for (int i = 0; i < 5; i++)
        {
            Update update = CreateMessageUpdate(100 + i, $"concurrent-{i}", 700 + i, 701);
            string json = JsonSerializer.Serialize(update, Telegram.Bot.JsonBotSerializerContext.Default.Update);
            DefaultHttpContext context = CreateContext(provider, json);
            await ((Task)endpointMethod.Invoke(null, [context])!);
        }

        await Task.Delay(50);
        Assert.AreEqual(3, gate.Entered, "all 3 updates should have entered the handler concurrently");

        gate.Release(50);
        await Task.Delay(50);
        Assert.AreEqual(5, gate.Entered, "after the first update completes, the second one should enter");

        await hosted.StopAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task Webhook_ProcessesUpdatesSequentially_ByDefault()
    {
        PolyTests client = new();
        ServiceCollection services = new();
        services.AddSingleton<ConcurrencyGate>();
        services.AddPolyBotDefaults();
        services.AddSingleton<ITelegramBotClient>(client);
        services.AddPolyBotWebhook(static options =>
        {
            options.WebhookUrl = "https://example.com/bot";
        });

        await using ServiceProvider provider = services.BuildServiceProvider();
        ConcurrencyGate gate = provider.GetRequiredService<ConcurrencyGate>();

        IHostedService hosted = provider.GetRequiredService<IHostedService>();
        await hosted.StartAsync(CancellationToken.None);

        MethodInfo endpointMethod = typeof(PolyBotWebhookExtensions).GetMethod(
            "HandlePolyBotWebhookAsync",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new AssertFailedException("HandlePolyBotWebhookAsync was not generated");

        for (int i = 0; i < 3; i++)
        {
            Update update = CreateMessageUpdate(100 + i, $"sequential-{i}", 700 + i, 701);
            string json = JsonSerializer.Serialize(update, Telegram.Bot.JsonBotSerializerContext.Default.Update);
            DefaultHttpContext context = CreateContext(provider, json);
            await ((Task)endpointMethod.Invoke(null, [context])!);
        }

        await Task.Delay(50);
        Assert.AreEqual(1, gate.Entered, "with WebhookMaxConcurrentUpdates = 1 only one update should enter the handler");

        gate.Release(1);
        await Task.Delay(50);
        Assert.AreEqual(2, gate.Entered, "after the first update completes, the second one should enter");

        gate.Release(2);
        await hosted.StopAsync(CancellationToken.None);
    }

    private static DefaultHttpContext CreateContext(IServiceProvider provider, string body)
    {
        DefaultHttpContext context = new() { RequestServices = provider };
        context.Request.Method = "POST";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return context;
    }

    private static Update CreateMessageUpdate(int updateId, string text, long chatId, long userId)
    {
        return new Update
        {
            Id = updateId,
            Message = new Message
            {
                Id = updateId,
                Text = text,
                Chat = new Chat { Id = chatId, Type = ChatType.Private },
                From = new User { Id = userId, IsBot = false },
                Date = DateTime.UtcNow,
            },
        };
    }
}
