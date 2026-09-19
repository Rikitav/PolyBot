using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PolyBot.Tests;

[TestClass]
public sealed class ClientTests
{
    [TestMethod]
    public void BotClient_BeforeStartThrows()
    {
        PolyBotClient client = new PolyBotClient();

        Assert.ThrowsExactly<InvalidOperationException>(() => client.BotClient);
    }

    [TestMethod]
    public async Task RunPollingAsync_WithoutTokenThrows()
    {
        await using PolyBotClient client = new PolyBotClient();
        client.Services.AddPolyBotRouter();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => client.RunPollingAsync(new CancellationToken(canceled: true)));
    }

    [TestMethod]
    public async Task RunTest_RoutesSyntheticUpdatesWithMonotonicIdsAndBindsJsonConfig()
    {
        string configPath = Path.Combine(Path.GetTempPath(), "curator-tests-config.json");
        File.WriteAllText(configPath, """{ "PolyBot": { "BotToken": "1:cfg", "BotUsername": "CfgBot" } }""");
        try
        {
            await using PolyBotClient client = new PolyBotClient();
            client.Configuration.AddJsonFile(configPath);
            client.Services.AddKeyedSingleton<IKeyedService>("lol", new KeyedService("keyed-impl"));
            client.Services.AddSingleton<IWelcomeService, WelcomeService>();
            client.Services.AddPolyBotRouter();

            UpdateMocker mocker = client.RunTest();
            Update msgUpdate = await mocker.Message("mock hello", userId: 900);
            Update cmdUpdate = await mocker.Command("start", userId: 901);
            Update cbUpdate = await mocker.Callback("ping-cb", userId: 902);
            Update rawUpdate = await mocker.Update(new Update
            {
                Message = new Message
                {
                    Text = "raw",
                    From = new User { Id = 903, FirstName = "Test" },
                    Chat = new Chat { Id = 1, Type = ChatType.Private },
                },
            });

            Assert.IsTrue(0 < msgUpdate.Id && msgUpdate.Id < cmdUpdate.Id && cmdUpdate.Id < cbUpdate.Id && cbUpdate.Id < rawUpdate.Id);
            Assert.AreEqual("1:cfg", mocker.Services.GetRequiredService<PolyBotOptions>().BotToken);
            Assert.AreEqual("CfgBot", mocker.Services.GetRequiredService<PolyBotOptions>().BotUsername);

            IReadOnlyList<object> sent = mocker.BotClient.SentRequests;
            Assert.IsTrue(sent.OfType<SendMessageRequest>().Any(static r => r.Text == "echo:mock hello"));
            Assert.IsTrue(sent.OfType<SendMessageRequest>().Any(static r => r.Text.StartsWith("start:", StringComparison.Ordinal)));
            Assert.IsTrue(sent.OfType<AnswerCallbackQueryRequest>().Any());
        }
        finally
        {
            File.Delete(configPath);
        }
    }
}
