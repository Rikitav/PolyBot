using PolyBot.Attributes;
using PolyBot.Routing;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PolyBot.Tests.UpdateHandler.Typed;

public static class CallRecorder
{
    public static List<string> Calls { get; } = new();
}

public sealed class TypedHandlers
{
    [UpdateHandler(Types = [UpdateType.Poll, UpdateType.InlineQuery])]
    public static Task<Result> Multi(Update update)
    {
        CallRecorder.Calls.Add("multi:" + update.Type);
        return Task.FromResult(Result.Handled());
    }

    [MessageHandler]
    public static Task<Result> OnMessage(Message msg)
    {
        CallRecorder.Calls.Add("message");
        return Task.FromResult(Result.Handled());
    }
}
