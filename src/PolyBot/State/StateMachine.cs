using Telegram.Bot.Types;

namespace PolyBot.State;

/// <summary>
/// Default <see cref="IStateMachine"/>: composes keys via <see cref="StateKeyResolver"/>
/// from the update held by an <see cref="IUpdateContextAccessor"/> and delegates storage to
/// an <see cref="IStateStorage"/>. Resolved key strings are cached per current update.
/// </summary>
public sealed class StateMachine : IStateMachine
{
    private readonly IStateStorage _storage;
    private readonly IUpdateContextAccessor _accessor;

    private Update? _cachedUpdate;
    private string? _userKey;
    private string? _chatKey;
    private string? _userInChatKey;

    /// <summary>
    /// Creates the state machine over the given storage and accessor.
    /// </summary>
    public StateMachine(IStateStorage storage, IUpdateContextAccessor accessor)
    {
        _storage = storage;
        _accessor = accessor;
    }

    /// <inheritdoc />
    public ValueTask<TEnum?> GetAsync<TEnum>(StateKey keyType = StateKey.UserId, CancellationToken ct = default)
        where TEnum : struct, Enum
    {
        Update? update = _accessor.Update;
        if (update is null || !TryGetCachedKey(keyType, out string key))
        {
            return new ValueTask<TEnum?>((TEnum?)null);
        }

        return _storage.GetStateAsync<TEnum>(key, ct);
    }

    /// <inheritdoc />
    public ValueTask SetAsync<TEnum>(TEnum state, StateKey keyType = StateKey.UserId, CancellationToken ct = default)
        where TEnum : struct, Enum
    {
        return _storage.SetStateAsync(ResolveKey(keyType), state, ct);
    }

    /// <inheritdoc />
    public ValueTask ResetAsync<TEnum>(StateKey keyType = StateKey.UserId, CancellationToken ct = default)
        where TEnum : struct, Enum
    {
        return _storage.ClearStateAsync<TEnum>(ResolveKey(keyType), ct);
    }

    private string ResolveKey(StateKey keyType)
    {
        if (!TryGetCachedKey(keyType, out string key))
        {
            throw new InvalidOperationException(
                $"Cannot resolve a state key of type '{keyType}' for the current update; the state change would be lost.");
        }

        return key;
    }

    private bool TryGetCachedKey(StateKey keyType, out string key)
    {
        Update? update = _accessor.Update;
        if (update is null)
        {
            key = string.Empty;
            return false;
        }

        if (!ReferenceEquals(update, _cachedUpdate))
        {
            _cachedUpdate = update;
            _userKey = null;
            _chatKey = null;
            _userInChatKey = null;
        }

        string? cached = keyType switch
        {
            StateKey.UserId => _userKey,
            StateKey.ChatId => _chatKey,
            _ => _userInChatKey,
        };
        if (cached is not null)
        {
            key = cached;
            return true;
        }

        if (!StateKeyResolver.TryResolveKey(update, keyType, out key))
        {
            key = string.Empty;
            return false;
        }

        switch (keyType)
        {
            case StateKey.UserId:
                {
                    _userKey = key;
                    break;
                }

            case StateKey.ChatId:
                {
                    _chatKey = key;
                    break;
                }

            default:
                {
                    _userInChatKey = key;
                    break;
                }
        }

        return true;
    }
}
