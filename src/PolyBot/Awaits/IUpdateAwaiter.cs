using Telegram.Bot.Types;

namespace PolyBot.Awaits;

/// <summary>
/// Conversation-style inline awaiting over incoming updates: a handler builds an await via
/// <see cref="WaitForAsync{TUpdate}"/>, marks it up with <c>TextMatches</c>/<c>WithFilter</c>,
/// and awaits a terminal method; the generated router suspends the handler and resumes it
/// when a matching update arrives.
/// </summary>
public interface IUpdateAwaiter
{
    /// <summary>
    /// Starts building a single-shot await for a <typeparamref name="TUpdate"/> payload.
    /// </summary>
    /// <typeparam name="TUpdate">The payload DTO to await (e.g. <see cref="Message"/>). Unknown payload types never match.</typeparam>
    /// <param name="timeout">Optional timeout after which the terminal await completes with <c>null</c>.</param>
    UpdateAwaiterBuilder<TUpdate> WaitForAsync<TUpdate>(TimeSpan? timeout = null)
        where TUpdate : class;

    /// <summary>
    /// Router-facing delivery hook: called by the generated router after the branch's own
    /// static conditions (payload, regexes, <c>Where</c> lambdas, filters) have passed. Does
    /// no matching of its own — probes the numeric identity (most specific first) and, when
    /// a pending await with a matching declarative signature exists, atomically removes it
    /// and completes its task with <paramref name="payload"/>. Returns <c>true</c> when the
    /// update was claimed; <c>false</c> lets it fall through to normal routing.
    /// </summary>
    ValueTask<bool> TryDeliverAsync(long? userId, long? chatId, Update update, object payload, Type payloadType, string[] patterns, Type[] filterTypes, bool hasWhereCondition, CancellationToken ct = default);
}
