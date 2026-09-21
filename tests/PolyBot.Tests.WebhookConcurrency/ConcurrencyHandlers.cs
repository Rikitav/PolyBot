using PolyBot.Attributes;
using PolyBot.Routing;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace PolyBot.Tests.WebhookConcurrency;

public sealed class ConcurrencyGate
{
    private readonly SemaphoreSlim _semaphore = new(0);

    private int _entered;

    public int Entered => _entered;

    public async Task EnterAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref _entered);
        await _semaphore.WaitAsync(ct);
    }

    public void Release(int count) => _semaphore.Release(count);
}

public sealed class ConcurrencyHandlers
{
    [MessageHandler]
    public static async Task<Result> Concurrent(
        Message msg,
        ConcurrencyGate gate,
        ITelegramBotClient bot,
        CancellationToken ct)
    {
        await gate.EnterAsync(ct);
        await bot.SendMessage(msg.Chat.Id, $"ok:{msg.Text}", cancellationToken: ct);
        return Result.Handled();
    }
}
