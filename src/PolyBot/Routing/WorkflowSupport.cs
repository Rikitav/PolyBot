using PolyBot.Awaits;

namespace PolyBot.Routing;

/// <summary>
/// Helpers for conversational handlers suspended by <see cref="IUpdateAwaiter"/>. Called
/// by the generated router only.
/// </summary>
public static class WorkflowSupport
{
    /// <summary>
    /// Observes a suspended conversational workflow so an unhandled exception in it does
    /// not terminate the process via an unobserved task exception; its <see cref="Result"/>
    /// does not affect routing.
    /// </summary>
    public static void ObserveFireAndForget(Task<Result> workflow)
    {
        workflow.ContinueWith(
            static task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }
}
