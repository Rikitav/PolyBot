using Telegram.Bot.Types.Enums;

namespace PolyBot.Attributes;

/// <summary>
/// Marks a method as a handler operating on the raw <see cref="Telegram.Bot.Types.Update"/>,
/// independent of any specific update type. Use for catch-all / fallback handlers
/// (logging, telemetry, audit trails), cross-type filtering, or update types that have no
/// specialized helper attribute yet.
/// </summary>
/// <remarks>
/// A handler method must declare a parameter of type <see cref="Telegram.Bot.Types.Update"/>;
/// other parameters (DI services, <c>ITelegramBotClient</c>, <c>CancellationToken</c>, etc.)
/// are resolved as for any other handler.
/// <para>
/// Universal handlers (no <see cref="Types"/>) run outside the <c>switch (update.Type)</c>:
/// with <see cref="HandlerAttribute.Priority"/> greater than or equal to <c>0</c> they run
/// before the switch (global pre-filters), with negative priority they run after it
/// (unhandled-update fallbacks). Higher priority values run earlier. Handlers returning
/// <see cref="PolyBot.Routing.Result.StopRouting"/> halt processing of the update.
/// </para>
/// <para>
/// A universal handler requests all update types from Telegram; declare <see cref="Types"/>
/// to restrict it to explicit update types, which also restricts the inferred
/// <c>AllowedUpdates</c> set.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class UpdateHandlerAttribute : HandlerAttribute
{
    /// <summary>
    /// Optional explicit filter for specific <see cref="UpdateType"/> values.
    /// If omitted or empty, the handler matches ALL incoming update types (universal catch-all).
    /// </summary>
    public UpdateType[]? Types { get; set; }
}
