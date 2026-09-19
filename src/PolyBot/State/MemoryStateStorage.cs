using System.Collections.Concurrent;

namespace PolyBot.State;

/// <summary>
/// In-memory <see cref="IStateStorage"/> keyed by (enum type, raw key) pairs so different
/// enums never collide; expired entries are removed on read, which then returns <c>null</c>.
/// </summary>
/// <remarks>
/// Process-local — meant for development and single-instance bots; register a custom
/// <see cref="IStateStorage"/> before <c>AddPolyBot</c> to override it.
/// </remarks>
public sealed class MemoryStateStorage : IStateStorage
{
    private readonly ConcurrentDictionary<(Type EnumType, string Key), Entry> _states = new();
    private readonly TimeSpan? _defaultExpiration;

    /// <param name="defaultExpiration">
    /// Optional lifetime counted from the moment a state is written; <c>null</c> (default)
    /// stores without expiration.
    /// </param>
    public MemoryStateStorage(TimeSpan? defaultExpiration = null)
    {
        _defaultExpiration = defaultExpiration;
    }

    /// <inheritdoc />
    public ValueTask<TEnum?> GetStateAsync<TEnum>(string key, CancellationToken ct = default)
        where TEnum : struct, Enum
    {
        (Type, string) storageKey = (typeof(TEnum), key);
        if (_states.TryGetValue(storageKey, out Entry entry) && entry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _states.TryRemove(storageKey, out _);
            entry = default;
        }

        return new ValueTask<TEnum?>((TEnum?)entry.State);
    }

    /// <inheritdoc />
    public ValueTask SetStateAsync<TEnum>(string key, TEnum state, CancellationToken ct = default)
        where TEnum : struct, Enum
    {
        DateTimeOffset expiresAt = _defaultExpiration.HasValue
            ? DateTimeOffset.UtcNow.Add(_defaultExpiration.Value)
            : DateTimeOffset.MaxValue;
        _states[(typeof(TEnum), key)] = new Entry(state, expiresAt);
        return default;
    }

    /// <inheritdoc />
    public ValueTask ClearStateAsync<TEnum>(string key, CancellationToken ct = default)
        where TEnum : struct, Enum
    {
        _states.TryRemove((typeof(TEnum), key), out _);
        return default;
    }

    private readonly struct Entry
    {
        public Entry(object state, DateTimeOffset expiresAt)
        {
            State = state;
            ExpiresAt = expiresAt;
        }

        public object State { get; }

        public DateTimeOffset ExpiresAt { get; }
    }
}
