namespace PolyBot.Attributes;

/// <summary>
/// The BotFather audience a command is registered for, mapped to the corresponding
/// <c>Telegram.Bot.Types.BotCommandScopes.BotCommandScope</c> by the generated <c>PolyBotBotFatherSync</c>.
/// </summary>
public enum CommandScope
{
    /// <summary>
    /// All chats (the default).
    /// </summary>
    Default,

    /// <summary>
    /// All private chats.
    /// </summary>
    AllPrivateChats,

    /// <summary>
    /// All group chats.
    /// </summary>
    AllGroupChats,

    /// <summary>
    /// All chats where the user is an administrator.
    /// </summary>
    AllChatAdministrators,
}
