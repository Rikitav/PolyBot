using PolyBot.Routing;
using System.Globalization;
using Telegram.Bot.Types;

namespace PolyBot.State;

/// <summary>
/// Resolves state storage keys from the update being routed, delegating to the probe
/// chains in <see cref="UpdateExtensions"/>. Ids are formatted invariant-culture;
/// <see cref="StateKey.UserInChat"/> combines both ids and requires both to be present.
/// Updates with no resolvable identity (e.g. plain polls) resolve nothing.
/// </summary>
public static class StateKeyResolver
{
    /// <summary>
    /// Attempts to resolve a state key of type <paramref name="key"/> from
    /// <paramref name="update"/>, returning <see cref="string.Empty"/> when nothing resolves.
    /// </summary>
    public static bool TryResolveKey(Update update, StateKey key, out string resolved)
    {
        switch (key)
        {
            case StateKey.UserId:
                {
                    long? userId = update.GetUserId();
                    resolved = userId.HasValue ? userId.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
                    return userId.HasValue;
                }

            case StateKey.ChatId:
                {
                    long? chatId = update.GetChatId();
                    resolved = chatId.HasValue ? chatId.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
                    return chatId.HasValue;
                }

            case StateKey.UserInChat:
                {
                    long? chatId = update.GetChatId();
                    long? userId = update.GetUserId();
                    if (!chatId.HasValue || !userId.HasValue)
                    {
                        resolved = string.Empty;
                        return false;
                    }

                    resolved = chatId.Value.ToString(CultureInfo.InvariantCulture) + "_" + userId.Value.ToString(CultureInfo.InvariantCulture);
                    return true;
                }

            default:
                {
                    resolved = string.Empty;
                    return false;
                }
        }
    }

    /// <summary>
    /// Resolves the raw ids behind a <see cref="StateKey"/> without formatting them — the
    /// non-allocating counterpart of <see cref="TryResolveKey"/>. A missing id is returned
    /// as <c>0</c> (Telegram ids are positive, so 0 means "not part of the identity").
    /// </summary>
    public static bool TryResolveIds(Update update, StateKey key, out long userId, out long chatId)
    {
        userId = 0;
        chatId = 0;
        switch (key)
        {
            case StateKey.UserId:
                {
                    long? id = update.GetUserId();
                    userId = id ?? 0;
                    return id.HasValue;
                }

            case StateKey.ChatId:
                {
                    long? id = update.GetChatId();
                    chatId = id ?? 0;
                    return id.HasValue;
                }

            case StateKey.UserInChat:
                {
                    long? uid = update.GetUserId();
                    long? cid = update.GetChatId();
                    userId = uid ?? 0;
                    chatId = cid ?? 0;
                    return uid.HasValue && cid.HasValue;
                }

            default:
                {
                    return false;
                }
        }
    }
}
