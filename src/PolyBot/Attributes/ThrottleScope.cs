namespace PolyBot.Attributes;

/// <summary>
/// The bucket a <see cref="ThrottledAttribute"/> rate-limits by.
/// </summary>
public enum ThrottleScope
{
    /// <summary>
    /// Per user id (<c>from.id</c>); updates without a user share bucket 0.
    /// </summary>
    User,

    /// <summary>
    /// Per chat id; updates without a chat share bucket 0.
    /// </summary>
    Chat,

    /// <summary>
    /// Per user-in-chat pair.
    /// </summary>
    UserInChat,

    /// <summary>
    /// A single bucket for the handler across all users and chats.
    /// </summary>
    Global,
}
