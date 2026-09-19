using PolyBot.Routing;
using PolyBot.State;
using Telegram.Bot.Types;

namespace PolyBot.Awaits;

/// <summary>
/// Immutable-fluent registration builder for a single-shot update await: mark it up with
/// <see cref="TextMatches(string)"/> and the <c>WithFilter</c> overloads, then complete the
/// registration with a terminal method (<see cref="ByUserId"/>, <see cref="ByChatId"/>,
/// <see cref="ByUserInChat"/>).
/// </summary>
/// <remarks>
/// The marker methods are consumed by the source generator, which emits their checks into
/// the router branch — the engine stores and checks nothing at delivery. <c>Where</c>
/// requires <c>static</c>, non-async, expression-bodied lambdas (else <c>CUR016</c>); a
/// filter passed as a variable is neither checked nor keyed.
/// </remarks>
/// <typeparam name="TUpdate">The awaited payload DTO.</typeparam>
public sealed class UpdateAwaiterBuilder<TUpdate>
    where TUpdate : class
{
    private readonly UpdateAwaiter _engine;
    private readonly TimeSpan? _timeout;
    private readonly List<string> _patterns;
    private readonly List<Type> _filterTypes;
    private bool _hasWhere;

    internal UpdateAwaiterBuilder(UpdateAwaiter engine, TimeSpan? timeout)
    {
        _engine = engine;
        _timeout = timeout;
        _patterns = new List<string>();
        _filterTypes = new List<Type>();
    }
    /// <summary>
    /// Generator marker: the generated router branch matches the pattern against the
    /// payload's <see cref="Message.Text"/> (falling back to <see cref="Message.Caption"/>);
    /// the pattern also becomes part of the registration key. Non-message payloads are a
    /// generator error.
    /// </summary>
    /// <param name="pattern">The regular expression scanned against the text.</param>
    public UpdateAwaiterBuilder<TUpdate> TextMatches(string pattern)
    {
        _patterns.Add(pattern);
        return this;
    }

    /// <summary>
    /// Generator marker: the lambda is re-emitted verbatim into the generated router branch
    /// and invoked there. Only <c>static</c>, non-async, expression-bodied lambdas
    /// referencing only the parameter are allowed (else <c>CUR016</c>).
    /// </summary>
    /// <param name="predicate">The payload predicate (marker only, never invoked at runtime).</param>
    public UpdateAwaiterBuilder<TUpdate> Where(Func<TUpdate, bool> predicate)
    {
        _hasWhere = true;
        return this;
    }

    /// <summary>
    /// Generator marker: the filter's <see cref="IUpdateFilter.CanPass"/> is evaluated by
    /// the generated router branch and its type becomes part of the registration key. A
    /// filter passed as a variable is not statically analyzable — no check is emitted and
    /// the type is not keyed.
    /// </summary>
    /// <param name="filter">The filter the update must pass (marker only).</param>
    public UpdateAwaiterBuilder<TUpdate> WithFilter(IUpdateFilter filter)
    {
        _filterTypes.Add(filter.GetType());
        return this;
    }

    /// <summary>
    /// Generator marker: <typeparamref name="TFilter"/> is resolved from DI (with an
    /// <c>ActivatorUtilities</c> fallback), its <see cref="IUpdateFilter.CanPass"/> is
    /// evaluated by the generated router branch, and the type becomes part of the
    /// registration key.
    /// </summary>
    /// <typeparam name="TFilter">The filter type to apply (marker only).</typeparam>
    public UpdateAwaiterBuilder<TUpdate> WithFilter<TFilter>()
        where TFilter : class, IUpdateFilter
    {
        _filterTypes.Add(typeof(TFilter));
        return this;
    }

    /// <summary>
    /// Registers the await keyed by the CURRENT update's user id; on timeout the task
    /// completes with <c>null</c> and the registration is removed.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No current update, unresolvable user id, or an await with the same identity and
    /// signature already pending — slot conflicts throw instead of silently replacing.
    /// </exception>
    public Task<TUpdate?> ByUserId(CancellationToken ct = default)
    {
        return AwaitByKey(StateKey.UserId, ct);
    }

    /// <summary>
    /// Registers the await keyed by the CURRENT update's chat id. See <see cref="ByUserId"/>.
    /// </summary>
    public Task<TUpdate?> ByChatId(CancellationToken ct = default)
    {
        return AwaitByKey(StateKey.ChatId, ct);
    }

    /// <summary>
    /// Registers the await keyed by the CURRENT update's combined user-in-chat identity. See <see cref="ByUserId"/>.
    /// </summary>
    public Task<TUpdate?> ByUserInChat(CancellationToken ct = default)
    {
        return AwaitByKey(StateKey.UserInChat, ct);
    }

    private async Task<TUpdate?> AwaitByKey(StateKey keyType, CancellationToken ct)
    {
        Update? current = _engine.Accessor.Update;
        if (current is null ||
            !StateKeyResolver.TryResolveIds(current, keyType, out long userId, out long chatId))
        {
            throw new InvalidOperationException(
                $"Cannot resolve a {keyType} key for the current update; the await would never complete.");
        }

        AwaitIdentity identity = new AwaitIdentity(userId, chatId);
        TaskCompletionSource<object?> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        UpdateAwaiter.PendingAwait pending = new UpdateAwaiter.PendingAwait(
            typeof(TUpdate), _patterns.ToArray(), _filterTypes.ToArray(), _hasWhere, tcs, new CancellationTokenSource());
        _engine.RegisterAwait(identity, _timeout, pending);
        if (!ct.CanBeCanceled)
        {
            object? result = await tcs.Task.ConfigureAwait(false);
            return result as TUpdate;
        }

        return await AwaitWithCancellation(identity, tcs, ct).ConfigureAwait(false);
    }

    private async Task<TUpdate?> AwaitWithCancellation(AwaitIdentity identity, TaskCompletionSource<object?> tcs, CancellationToken ct)
    {
        TaskCompletionSource<bool> canceled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using (ct.Register(static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), canceled))
        {
            Task completed = await Task.WhenAny(tcs.Task, canceled.Task).ConfigureAwait(false);
            if (ReferenceEquals(completed, canceled.Task))
            {
                _engine.CancelAwait(identity, tcs);
                throw new OperationCanceledException(ct);
            }
        }

        object? result = await tcs.Task.ConfigureAwait(false);
        return result as TUpdate;
    }
}
