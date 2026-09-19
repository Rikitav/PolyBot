namespace PolyBot.Attributes;

/// <summary>
/// What a throttled handler does when its sliding window is exhausted.
/// </summary>
public enum ThrottleAction
{
    /// <summary>
    /// Halts the whole routing branch for this update (as if the handler returned <c>Result.StopRouting</c>).
    /// </summary>
    Ignore,

    /// <summary>
    /// Skips this handler and continues evaluating the remaining candidate handlers.
    /// </summary>
    Fallthrough,
}
