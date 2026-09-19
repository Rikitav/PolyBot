using Telegram.Bot.Types;

namespace PolyBot.Routing;

/// <summary>
/// A synchronous gate over an incoming update, evaluated by the generated router when a
/// handler carries filter attributes. The source generator discovers concrete
/// implementations and emits a matching <c>XxxFilterAttribute</c>; the router resolves
/// filters from the service collection, falling back to <c>ActivatorUtilities</c>.
/// </summary>
public interface IUpdateFilter
{
    /// <summary>
    /// Evaluates the update; returning <c>false</c> skips the handler.
    /// </summary>
    bool CanPass(Update update);
}
