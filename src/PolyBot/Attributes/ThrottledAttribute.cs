namespace PolyBot.Attributes;

/// <summary>
/// Declarative sliding-window rate limiting on a handler. The generated router inlines the
/// guard as a single <c>ThrottleGate.TryEnter</c> call at the top of the handler's branch —
/// before state fetches, filter resolution or handler invocation. Gates are static per handler
/// and thread-safe across concurrent polling loops and parallel webhook requests.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ThrottledAttribute : Attribute
{
    /// <summary>
    /// Maximum number of requests permitted within the window.
    /// </summary>
    public int Limit { get; set; } = 1;

    /// <summary>
    /// Duration window in milliseconds.
    /// </summary>
    public int PeriodMilliseconds { get; set; } = 1000;

    /// <summary>
    /// Scope/key level for rate-limiting bucket.
    /// </summary>
    public ThrottleScope Scope { get; set; } = ThrottleScope.User;

    /// <summary>
    /// Behavior when rate limit is exceeded.
    /// </summary>
    public ThrottleAction Action { get; set; } = ThrottleAction.Ignore;
}
