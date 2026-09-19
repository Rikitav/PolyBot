using Telegram.Bot.Types;

namespace PolyBot.State;

/// <summary>
/// Holds the <see cref="Update"/> currently being routed. Implemented with
/// <see cref="System.Threading.AsyncLocal{T}"/> so the value flows through async handler
/// invocations of one update without leaking into concurrently processed updates.
/// </summary>
public interface IUpdateContextAccessor
{
    /// <summary>
    /// The update currently being routed, or <c>null</c> outside of routing.
    /// </summary>
    Update? Update { get; set; }
}
