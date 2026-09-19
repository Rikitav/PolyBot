namespace PolyBot.Attributes;

/// <summary>
/// Marks the bot's single exception handler: the generated <c>PolyBot.BotRouter.HandleErrorAsync</c>
/// invokes it for every update-handling failure instead of swallowing it. Parameters resolve
/// like handler parameters — <see cref="System.Exception"/>, <see cref="Telegram.Bot.ITelegramBotClient"/>
/// and <see cref="System.Threading.CancellationToken"/> come from the error callback, anything else
/// from the DI container (keyed with <see cref="KeyAttribute"/>) — but there is no current update in
/// the error path, so update-bound parameters are rejected at compile time. The method must return
/// <see cref="System.Threading.Tasks.Task"/> or <see cref="System.Threading.Tasks.ValueTask"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ExceptionHandlerAttribute : Attribute
{
}
