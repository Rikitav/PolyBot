using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;

namespace PolyBot.Tests;

[TestClass]
public sealed class HostingTests
{
    [TestMethod]
    public async Task HostedPolling_DeliversEnqueuedUpdatesAndDeletesWebhook()
    {
        PolyTests client = new();
        ServiceCollection services = new();
        services.AddPolyBotRouter();
        services.AddKeyedSingleton<IKeyedService>("lol", new KeyedService("keyed-impl"));
        services.AddSingleton<IWelcomeService, WelcomeService>();
        services.AddSingleton<Telegram.Bot.ITelegramBotClient>(client);
        services.AddPolyBotHostedPolling();
        await using ServiceProvider provider = services.BuildServiceProvider();

        IHostedService hosted = provider.GetRequiredService<IHostedService>();
        await hosted.StartAsync(CancellationToken.None);
        client.EnqueueUpdate(TestHost.CommandUpdate(500, "/start", 710, 711));

        bool delivered = await TestHost.WaitForAsync(
            () => client.SentRequests.OfType<SendMessageRequest>().Any(static r => r.Text.StartsWith("start:", StringComparison.Ordinal)));
        await hosted.StopAsync(CancellationToken.None);

        Assert.IsTrue(delivered, "the enqueued update flowed through the real polling loop into the router");
        Assert.HasCount(1, client.SentRequests.OfType<DeleteWebhookRequest>());
        Assert.IsTrue(client.SentRequests.OfType<GetUpdatesRequest>().Any());
    }

    [TestMethod]
    public async Task Webhook_LifecycleSetsAndDeletesWebhook()
    {
        PolyTests client = new();
        ServiceCollection services = new();
        services.AddPolyBotDefaults();
        services.AddKeyedSingleton<IKeyedService>("lol", new KeyedService("keyed-impl"));
        services.AddSingleton<IWelcomeService, WelcomeService>();
        services.AddSingleton<Telegram.Bot.ITelegramBotClient>(client);
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
        Assert.AreEqual("https://example.com/bot", set.Url);
        Assert.AreEqual("s3cr3t", set.SecretToken);
        Assert.HasCount(1, client.SentRequests.OfType<DeleteWebhookRequest>());
    }

    [TestMethod]
    public async Task Webhook_EndpointVerifiesSecretDeserializesAndRoutes()
    {
        PolyTests client = new();
        ServiceCollection services = new();
        services.AddPolyBotDefaults();
        services.AddKeyedSingleton<IKeyedService>("lol", new KeyedService("keyed-impl"));
        services.AddSingleton<IWelcomeService, WelcomeService>();
        services.AddSingleton<Telegram.Bot.ITelegramBotClient>(client);
        services.AddPolyBotWebhook(static options =>
        {
            options.WebhookUrl = "https://example.com/bot";
            options.WebhookSecretToken = "s3cr3t";
        });
        await using ServiceProvider provider = services.BuildServiceProvider();

        // Resolving the hosted service applies the options configure delegate (url + secret).
        _ = provider.GetRequiredService<IHostedService>();

        MethodInfo endpointMethod = typeof(PolyBotWebhookExtensions).GetMethod(
            "HandlePolyBotWebhookAsync",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new AssertFailedException("HandlePolyBotWebhookAsync was not generated");

        DefaultHttpContext missingSecret = CreateWebhookContext(provider, "{}", secret: null);
        await ((Task)endpointMethod.Invoke(null, [missingSecret])!);
        DefaultHttpContext wrongSecret = CreateWebhookContext(provider, "{}", secret: "wrong");
        await ((Task)endpointMethod.Invoke(null, [wrongSecret])!);

        Update update = TestHost.MessageUpdate(90, "via webhook", 700, 701);
        string updateJson = JsonSerializer.Serialize(update, Telegram.Bot.JsonBotSerializerContext.Default.Update);
        DefaultHttpContext validRequest = CreateWebhookContext(provider, updateJson, secret: "s3cr3t");
        await ((Task)endpointMethod.Invoke(null, [validRequest])!);

        DefaultHttpContext garbageBody = CreateWebhookContext(provider, "not json", secret: "s3cr3t");
        await ((Task)endpointMethod.Invoke(null, [garbageBody])!);

        Assert.AreEqual(400, missingSecret.Response.StatusCode);
        Assert.AreEqual(401, wrongSecret.Response.StatusCode);
        Assert.AreEqual(200, validRequest.Response.StatusCode);
        Assert.AreEqual(400, garbageBody.Response.StatusCode);
        Assert.IsTrue(
            client.SentRequests.OfType<SendMessageRequest>().Any(static r => r.Text == "echo:via webhook"),
            "the deserialized update was handed to the router, which replied through the test client");
    }

    [TestMethod]
    public void MapPolyBotWebhook_BindsOnAWebApplication()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        using WebApplication app = builder.Build();

        app.MapPolyBotWebhook();
    }

    private static DefaultHttpContext CreateWebhookContext(IServiceProvider provider, string body, string? secret)
    {
        DefaultHttpContext context = new() { RequestServices = provider };
        context.Request.Method = "POST";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        if (secret is not null)
        {
            context.Request.Headers["X-Telegram-Bot-Api-Secret-Token"] = secret;
        }

        return context;
    }
}
