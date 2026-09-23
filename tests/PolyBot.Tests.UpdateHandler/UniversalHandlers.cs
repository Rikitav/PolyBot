using PolyBot.Attributes;
using PolyBot.Routing;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PolyBot.Tests.UpdateHandler;

public static class CallRecorder
{
    public static List<string> Calls { get; } = new();

    public static bool StopInPreFilter { get; set; }
}

public sealed class UniversalHandlers
{
    [UpdateHandler(Priority = 100, Types = [UpdateType.Message])]
    public static Task<Result> Audit(Update update, ITelegramBotClient bot, CancellationToken ct)
    {
        CallRecorder.Calls.Add("audit");
        return Task.FromResult(CallRecorder.StopInPreFilter ? Result.StopRouting : Result.Continue);
    }

    [UpdateHandler(Priority = 50)]
    public static Task<Result> AuditSecond(Update update)
    {
        CallRecorder.Calls.Add("audit2");
        return Task.FromResult(Result.Continue);
    }

    [UpdateHandler(Priority = -100)]
    public static Task<Result> Fallback(Update update)
    {
        CallRecorder.Calls.Add("fallback");
        return Task.FromResult(Result.Handled());
    }

    [MessageHandler]
    public static Task<Result> OnMessage(Message msg)
    {
        CallRecorder.Calls.Add("message");
        return Task.FromResult(Result.Handled());
    }
}
