using PolyBot.State;
using System.Collections.Concurrent;
using Telegram.Bot.Types;

namespace PolyBot.Awaits;

/// <summary>
/// Default <see cref="IUpdateAwaiter"/>: pending awaits live in a
/// <see cref="ConcurrentDictionary{TKey, TValue}"/> keyed by numeric
/// <see cref="AwaitIdentity"/> pairs (no key strings formatted per update), bucketed by
/// declarative signature (payload type, <c>TextMatches</c> patterns, filter types, Where
/// flag). Delivery is single-winner via <c>Interlocked.Exchange</c> gating so a timeout
/// racing a delivery cannot double-complete. Registered as a singleton by <c>AddPolyBot</c>.
/// </summary>
public sealed class UpdateAwaiter : IUpdateAwaiter
{
    private readonly IUpdateContextAccessor _accessor;
    private readonly ConcurrentDictionary<AwaitIdentity, List<PendingAwait>> _pending = new();

    /// <summary>
    /// Creates the awaiter over the given update context accessor.
    /// </summary>
    public UpdateAwaiter(IUpdateContextAccessor accessor)
    {
        _accessor = accessor;
    }

    internal IUpdateContextAccessor Accessor => _accessor;

    /// <inheritdoc />
    public UpdateAwaiterBuilder<TUpdate> WaitForAsync<TUpdate>(TimeSpan? timeout = null)
        where TUpdate : class
    {
        return new UpdateAwaiterBuilder<TUpdate>(this, timeout);
    }

    /// <inheritdoc />
    public ValueTask<bool> TryDeliverAsync(long? userId, long? chatId, Update update, object payload, Type payloadType, string[] patterns, Type[] filterTypes, bool hasWhereCondition, CancellationToken ct = default)
    {
        if (userId.HasValue && chatId.HasValue &&
            TryClaim(new AwaitIdentity(userId.Value, chatId.Value), update, payload, payloadType, patterns, filterTypes, hasWhereCondition))
        {
            return new ValueTask<bool>(true);
        }

        if (userId.HasValue &&
            TryClaim(new AwaitIdentity(userId.Value, 0), update, payload, payloadType, patterns, filterTypes, hasWhereCondition))
        {
            return new ValueTask<bool>(true);
        }

        if (chatId.HasValue &&
            TryClaim(new AwaitIdentity(0, chatId.Value), update, payload, payloadType, patterns, filterTypes, hasWhereCondition))
        {
            return new ValueTask<bool>(true);
        }

        return new ValueTask<bool>(false);
    }

    internal void RegisterAwait(AwaitIdentity identity, TimeSpan? timeout, PendingAwait pending)
    {
        List<PendingAwait> bucket = _pending.GetOrAdd(identity, static _ => new List<PendingAwait>());
        lock (bucket)
        {
            foreach (PendingAwait existing in bucket)
            {
                if (existing.Matches(pending.PayloadType, pending.Patterns, pending.FilterTypes, pending.HasWhere))
                {
                    throw new InvalidOperationException("An update await with the same key and filter set is already pending.");
                }
            }

            bucket.Add(pending);
        }

        if (timeout.HasValue)
        {
            Task.Delay(timeout.Value, pending.TimeoutCts.Token).ContinueWith(
                (task, state) => TimeoutAwaitElapsed((ValueTuple<AwaitIdentity, PendingAwait>)state!),
                (identity, pending),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Default);
        }
    }

    internal void CancelAwait(AwaitIdentity identity, TaskCompletionSource<object?> tcs)
    {
        if (!_pending.TryGetValue(identity, out List<PendingAwait>? bucket))
        {
            return;
        }

        lock (bucket)
        {
            for (int i = bucket.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(bucket[i].Tcs, tcs))
                {
                    Interlocked.Exchange(ref bucket[i].Completed, 1);
                    bucket[i].TimeoutCts.Cancel();
                    bucket.RemoveAt(i);
                    break;
                }
            }

            if (bucket.Count == 0)
            {
                _pending.TryRemove(identity, out _);
            }
        }
    }

    private bool TryClaim(AwaitIdentity identity, Update update, object payload, Type payloadType, string[] patterns, Type[] filterTypes, bool hasWhere)
    {
        if (!_pending.TryGetValue(identity, out List<PendingAwait>? bucket))
        {
            return false;
        }

        lock (bucket)
        {
            for (int i = 0; i < bucket.Count; i++)
            {
                PendingAwait pending = bucket[i];
                if (!pending.Matches(payloadType, patterns, filterTypes, hasWhere))
                {
                    continue;
                }

                bucket.RemoveAt(i);
                if (bucket.Count == 0)
                {
                    _pending.TryRemove(identity, out _);
                }

                if (Interlocked.Exchange(ref pending.Completed, 1) != 0)
                {
                    return false;
                }

                pending.TimeoutCts.Cancel();

                // Set the current update BEFORE completing the task so the resumed
                // workflow keys its next await on the delivered update.
                _accessor.Update = update;
                pending.Tcs.TrySetResult(payload);
                return true;
            }
        }

        return false;
    }

    private void TimeoutAwaitElapsed(ValueTuple<AwaitIdentity, PendingAwait> state)
    {
        PendingAwait pending = state.Item2;
        pending.TimeoutCts.Cancel();

        if (Interlocked.Exchange(ref pending.Completed, 1) != 0)
        {
            return;
        }

        if (_pending.TryGetValue(state.Item1, out List<PendingAwait>? bucket))
        {
            lock (bucket)
            {
                bucket.Remove(pending);
                if (bucket.Count == 0)
                {
                    _pending.TryRemove(state.Item1, out _);
                }
            }
        }

        pending.Tcs.TrySetResult(null);
    }

    internal sealed class PendingAwait
    {
        public PendingAwait(Type payloadType, string[] patterns, Type[] filterTypes, bool hasWhere, TaskCompletionSource<object?> tcs, CancellationTokenSource timeoutCts)
        {
            PayloadType = payloadType;
            Patterns = patterns;
            FilterTypes = filterTypes;
            HasWhere = hasWhere;
            Tcs = tcs;
            TimeoutCts = timeoutCts;
        }

        public Type PayloadType { get; }

        public string[] Patterns { get; }

        public Type[] FilterTypes { get; }

        public bool HasWhere { get; }

        public TaskCompletionSource<object?> Tcs { get; }

        public CancellationTokenSource TimeoutCts { get; }

        public int Completed;

        public bool Matches(Type payloadType, string[] patterns, Type[] filterTypes, bool hasWhere)
        {
            if (PayloadType != payloadType || HasWhere != hasWhere ||
                Patterns.Length != patterns.Length || FilterTypes.Length != filterTypes.Length)
            {
                return false;
            }

            for (int i = 0; i < Patterns.Length; i++)
            {
                if (!string.Equals(Patterns[i], patterns[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            for (int i = 0; i < FilterTypes.Length; i++)
            {
                if (FilterTypes[i] != filterTypes[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
