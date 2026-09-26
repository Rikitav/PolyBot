using PolyBot.BotFather;
using PolyBot.Commands;

namespace PolyBot.Attributes;

/// <summary>
/// Restricts a message handler to one or more bot commands (e.g. <c>/start</c>).
/// Only meaningful on handlers whose update payload is a <see cref="Telegram.Bot.Types.Message"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class CommandAttribute : Attribute
{
    /// <summary>
    /// Initializes an empty attribute; configure via <see cref="Aliases"/> and the other
    /// properties.
    /// </summary>
    public CommandAttribute()
    {
    }

    /// <summary>
    /// Initializes the attribute with command aliases — shorthand for
    /// <c>Aliases = [..aliases]</c>, e.g. <c>[Command("start")]</c> is equivalent to
    /// <c>[Command(Aliases = ["start"])]</c>.
    /// </summary>
    /// <param name="aliases">Command names without the leading '/', matched case-insensitively.</param>
    public CommandAttribute(params string[] aliases)
    {
        Aliases = aliases;
    }

    /// <summary>
    /// Command names without the leading '/', matched case-insensitively.
    /// </summary>
    public string[] Aliases { get; set; } = [];

    /// <summary>
    /// When <c>true</c>, the argument parse throws a
    /// <see cref="CommandArgsParseException"/> if the message carries any tokens after
    /// the command; extra tokens are ignored by default.
    /// </summary>
    public bool NoArgs { get; set; }

    /// <summary>
    /// The command prefix, controlling the matching mode. <c>'/'</c> (the default) matches
    /// <c>BotCommand</c> message entities (optionally with an <c>@botsuffix</c> verified
    /// against <see cref="PolyBotOptions.BotUsername"/>). <c>' '</c> or <c>'\0'</c> matches
    /// <see cref="Telegram.Bot.Types.Message.Text"/> via
    /// <see cref="string.StartsWith(string, StringComparison)"/> (ordinal-ignore-case); any
    /// other character requires that character at the start of the text followed by the alias.
    /// </summary>
    public char Prefix { get; set; } = '/';

    /// <summary>
    /// The BotFather menu description used by the generated <see cref="IBotFatherSync"/>;
    /// missing values produce a compile-time warning (unless <see cref="IsHidden"/> is set)
    /// and values longer than 256 characters are a compile-time error.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// BotFather localization code (e.g. <c>"en"</c>, <c>"ru"</c>) the command is
    /// registered under; <c>null</c> (the default) registers it for all languages.
    /// </summary>
    public string? LanguageCode { get; set; }

    /// <summary>
    /// The BotFather audience for the command.
    /// </summary>
    public CommandScope Scope { get; set; } = CommandScope.Default;

    /// <summary>
    /// When <c>true</c>, the command is matched for routing but excluded from BotFather
    /// synchronization. Hidden commands skip the missing-description warning.
    /// </summary>
    public bool IsHidden { get; set; }
}
