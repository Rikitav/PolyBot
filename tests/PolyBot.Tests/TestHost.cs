using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PolyBot.Tests;

internal sealed record TestHostHandle(ServiceProvider Provider, PolyTests Client, IUpdateHandler Router);

internal static class TestHost
{
    internal static TestHostHandle BuildHost(Action<IServiceCollection>? extra = null)
    {
        PolyTests client = new();
        ServiceCollection services = new();

        services.AddPolyBotDefaults();
        services.AddPolyBotRouter();
        services.AddSingleton<ITelegramBotClient>(client);
        services.AddKeyedSingleton<IKeyedService>("lol", new KeyedService("keyed-impl"));
        services.AddSingleton<IWelcomeService, WelcomeService>();
        extra?.Invoke(services);

        ServiceProvider provider = services.BuildServiceProvider();
        return new TestHostHandle(provider, client, provider.GetRequiredService<IUpdateHandler>());
    }

    internal static TestHostHandle BuildHost(string botUsername)
    {
        return BuildHost(services => services.AddSingleton(new PolyBotOptions { BotUsername = botUsername }));
    }

    internal static Update MessageUpdate(int id, string text, long userId, long chatId)
    {
        return new Update
        {
            Id = id,
            Message = new Message
            {
                Id = id,
                Text = text,
                From = new User { Id = userId, FirstName = "Test" },
                Chat = new Chat { Id = chatId, Type = ChatType.Private },
            },
        };
    }

    internal static Update CommandUpdate(int id, string text, long userId, long chatId)
    {
        int commandLength = text.IndexOf(' ');
        if (commandLength < 0)
        {
            commandLength = text.Length;
        }

        return new Update
        {
            Id = id,
            Message = new Message
            {
                Id = id,
                Text = text,
                Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = commandLength }],
                From = new User { Id = userId, FirstName = "Test" },
                Chat = new Chat { Id = chatId, Type = ChatType.Private },
            },
        };
    }

    internal static Update PollUpdate(int id)
    {
        return new Update
        {
            Id = id,
            Poll = new Poll
            {
                Id = "poll-" + id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Question = "Q?",
                Options = [],
                TotalVoterCount = 0,
                IsClosed = false,
                IsAnonymous = false,
                Type = PollType.Regular,
            },
        };
    }

    internal static async Task<bool> WaitForAsync(Func<bool> condition)
    {
        for (int i = 0; i < 50; i++)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(20);
        }

        return false;
    }
}
