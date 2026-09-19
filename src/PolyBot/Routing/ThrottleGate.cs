using PolyBot.Attributes;

namespace PolyBot.Routing;

/// <summary>
/// Thread-safe sliding-window rate limiter used by the generated <c>[Throttled]</c> guards;
/// buckets are keyed by the scope identity (user/chat pair, or bucket 0 for
/// <see cref="ThrottleScope.Global"/>). Buckets are never evicted — long-running bots with
/// many distinct users should prefer a scope with fewer keys or a custom limiter.
/// </summary>
public sealed class ThrottleGate
{
    private readonly int _limit;
    private readonly long _periodTicks;
    private readonly object _gate = new();
    private readonly Dictionary<(long First, long Second), Bucket> _buckets = new();

    /// <summary>
    /// Creates a gate allowing <paramref name="limit"/> entries per <paramref name="periodMilliseconds"/> window.
    /// </summary>
    public ThrottleGate(int limit, int periodMilliseconds)
    {
        _limit = limit;
        _periodTicks = TimeSpan.FromMilliseconds(periodMilliseconds).Ticks;
    }

    /// <summary>
    /// Single-key entry (<see cref="ThrottleScope.User"/>, <see cref="ThrottleScope.Chat"/> or <see cref="ThrottleScope.Global"/> with key 0).
    /// </summary>
    public bool TryEnter(long key)
    {
        return TryEnter(key, 0);
    }

    /// <summary>
    /// Two-key entry (<see cref="ThrottleScope.UserInChat"/>); also backs the single-key overload.
    /// </summary>
    public bool TryEnter(long first, long second)
    {
        lock (_gate)
        {
            (long, long) identity = (first, second);
            if (!_buckets.TryGetValue(identity, out Bucket? bucket))
            {
                bucket = new Bucket(_limit);
                _buckets.Add(identity, bucket);
            }

            long now = DateTime.UtcNow.Ticks;
            bucket.Prune(now, _periodTicks);
            if (bucket.Count >= _limit)
            {
                return false;
            }

            bucket.Record(now);
            return true;
        }
    }

    private sealed class Bucket
    {
        private readonly long[] _timestamps;
        private int _start;

        public Bucket(int capacity)
        {
            _timestamps = new long[capacity];
        }

        public int Count { get; private set; }

        public void Prune(long now, long period)
        {
            while (Count > 0 && now - _timestamps[_start] >= period)
            {
                _start = (_start + 1) % _timestamps.Length;
                Count--;
            }
        }

        public void Record(long now)
        {
            _timestamps[(_start + Count) % _timestamps.Length] = now;
            Count++;
        }
    }
}
