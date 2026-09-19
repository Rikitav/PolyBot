namespace PolyBot.State;

/// <summary>
/// Determines which identity of the current <see cref="Telegram.Bot.Types.Update"/> a
/// state key is resolved from (see <see cref="StateKeyResolver"/>).
/// </summary>
public enum StateKey
{
    /// <summary>
    /// The sending user's id; usable for most update types.
    /// </summary>
    UserId,

    /// <summary>
    /// The chat's id; usable for message-, callback- and chat-related update types.
    /// </summary>
    ChatId,

    /// <summary>
    /// Combined <c>"{chatId}_{userId}"</c> key;
    /// requires both ids to be present.
    /// </summary>
    UserInChat,
}
