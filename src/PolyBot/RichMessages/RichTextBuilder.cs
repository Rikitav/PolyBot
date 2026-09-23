using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PolyBot.RichMessages;

/// <summary>
/// Fluent builder that accumulates <see cref="RichText"/> inline nodes and produces a single
/// <see cref="RichText"/> tree (a <see cref="RichTextArray"/> when more than one node is added).
/// String overloads wrap the text in a plain node automatically; <see cref="RichText"/> overloads
/// allow nesting (e.g. bold inside a URL).
/// </summary>
public sealed class RichTextBuilder
{
    private readonly List<RichText> _nodes = new();

    /// <summary>Appends plain text.</summary>
    public RichTextBuilder Plain(string text)
    {
        _nodes.Add(RichTextFactory.Plain(text));
        return this;
    }

    /// <summary>Appends bold text.</summary>
    public RichTextBuilder Bold(string text) => Add(RichTextFactory.Bold(RichTextFactory.Plain(text)));

    /// <summary>Appends bold formatting around an existing node.</summary>
    public RichTextBuilder Bold(RichText child) => Add(RichTextFactory.Bold(child));

    /// <summary>Appends italic text.</summary>
    public RichTextBuilder Italic(string text) => Add(RichTextFactory.Italic(RichTextFactory.Plain(text)));

    /// <summary>Appends italic formatting around an existing node.</summary>
    public RichTextBuilder Italic(RichText child) => Add(RichTextFactory.Italic(child));

    /// <summary>Appends underlined text.</summary>
    public RichTextBuilder Underline(string text) => Add(RichTextFactory.Underline(RichTextFactory.Plain(text)));

    /// <summary>Appends underline formatting around an existing node.</summary>
    public RichTextBuilder Underline(RichText child) => Add(RichTextFactory.Underline(child));

    /// <summary>Appends strikethrough text.</summary>
    public RichTextBuilder Strikethrough(string text) => Add(RichTextFactory.Strikethrough(RichTextFactory.Plain(text)));

    /// <summary>Appends strikethrough formatting around an existing node.</summary>
    public RichTextBuilder Strikethrough(RichText child) => Add(RichTextFactory.Strikethrough(child));

    /// <summary>Appends spoiler text.</summary>
    public RichTextBuilder Spoiler(string text) => Add(RichTextFactory.Spoiler(RichTextFactory.Plain(text)));

    /// <summary>Appends spoiler formatting around an existing node.</summary>
    public RichTextBuilder Spoiler(RichText child) => Add(RichTextFactory.Spoiler(child));

    /// <summary>Appends marked (highlighted) text.</summary>
    public RichTextBuilder Marked(string text) => Add(RichTextFactory.Marked(RichTextFactory.Plain(text)));

    /// <summary>Appends marked formatting around an existing node.</summary>
    public RichTextBuilder Marked(RichText child) => Add(RichTextFactory.Marked(child));

    /// <summary>Appends inline <c>code</c> text.</summary>
    public RichTextBuilder Code(string text) => Add(RichTextFactory.Code(RichTextFactory.Plain(text)));

    /// <summary>Appends inline code formatting around an existing node.</summary>
    public RichTextBuilder Code(RichText child) => Add(RichTextFactory.Code(child));

    /// <summary>Appends subscript text.</summary>
    public RichTextBuilder Subscript(string text) => Add(RichTextFactory.Subscript(RichTextFactory.Plain(text)));

    /// <summary>Appends superscript text.</summary>
    public RichTextBuilder Superscript(string text) => Add(RichTextFactory.Superscript(RichTextFactory.Plain(text)));

    /// <summary>Appends a URL link with the given visible <paramref name="text"/>.</summary>
    public RichTextBuilder Url(string text, string url) => Add(RichTextFactory.Url(RichTextFactory.Plain(text), url));

    /// <summary>Appends an @username mention.</summary>
    public RichTextBuilder Mention(string text, string username) => Add(RichTextFactory.Mention(RichTextFactory.Plain(text), username));

    /// <summary>Appends a text mention of a specific user.</summary>
    public RichTextBuilder TextMention(string text, User user) => Add(RichTextFactory.TextMention(RichTextFactory.Plain(text), user));

    /// <summary>Appends a custom emoji with fallback text.</summary>
    public RichTextBuilder CustomEmoji(string customEmojiId, string alternativeText)
        => Add(RichTextFactory.CustomEmoji(customEmojiId, alternativeText));

    /// <summary>Appends a bot command reference.</summary>
    public RichTextBuilder BotCommand(string text, string command) => Add(RichTextFactory.BotCommand(RichTextFactory.Plain(text), command));

    /// <summary>Appends a #hashtag.</summary>
    public RichTextBuilder Hashtag(string text, string hashtag) => Add(RichTextFactory.Hashtag(RichTextFactory.Plain(text), hashtag));

    /// <summary>Appends a $cashtag.</summary>
    public RichTextBuilder Cashtag(string text, string cashtag) => Add(RichTextFactory.Cashtag(RichTextFactory.Plain(text), cashtag));

    /// <summary>Appends an e-mail address.</summary>
    public RichTextBuilder Email(string text, string email) => Add(RichTextFactory.Email(RichTextFactory.Plain(text), email));

    /// <summary>Appends a phone number.</summary>
    public RichTextBuilder PhoneNumber(string text, string phone) => Add(RichTextFactory.PhoneNumber(RichTextFactory.Plain(text), phone));

    /// <summary>Appends a bank card number.</summary>
    public RichTextBuilder BankCard(string text, string number) => Add(RichTextFactory.BankCard(RichTextFactory.Plain(text), number));

    /// <summary>Appends a date/time rendered from a Unix timestamp.</summary>
    public RichTextBuilder DateTime(string text, System.DateTime unixTime, string? format = null)
        => Add(RichTextFactory.DateTime(RichTextFactory.Plain(text), unixTime, format));

    /// <summary>Appends a standalone mathematical expression.</summary>
    public RichTextBuilder Math(string expression) => Add(RichTextFactory.Math(expression));

    /// <summary>Appends an anchor target.</summary>
    public RichTextBuilder Anchor(string name) => Add(RichTextFactory.Anchor(name));

    /// <summary>Appends a link to an anchor defined elsewhere.</summary>
    public RichTextBuilder AnchorLink(string text, string anchorName)
        => Add(RichTextFactory.AnchorLink(RichTextFactory.Plain(text), anchorName));

    /// <summary>Appends a named reference.</summary>
    public RichTextBuilder Reference(string text, string name)
        => Add(RichTextFactory.Reference(RichTextFactory.Plain(text), name));

    /// <summary>Appends a link to a named reference.</summary>
    public RichTextBuilder ReferenceLink(string text, string referenceName)
        => Add(RichTextFactory.ReferenceLink(RichTextFactory.Plain(text), referenceName));

    /// <summary>Appends strikethrough text.</summary>
    public RichTextBuilder Button(
        RichText text,
        RichMessageButtonStyle? style = null,
        string? url = null,
        string? callbackData = null,
        WebAppInfo? webApp = null,
        LoginUrl? loginUrl = null,
        string? switchInlineQuery = null,
        string? switchInlineQueryCurrentChat = null,
        SwitchInlineQueryChosenChat? switchInlineQueryChosenChat = null,
        CopyTextButton? copyText = null,
        bool? disabled = null
    ) => Add(RichTextFactory.Button(
        text,
        style,
        url,
        callbackData,
        webApp,
        loginUrl,
        switchInlineQuery,
        switchInlineQueryCurrentChat,
        switchInlineQueryChosenChat,
        copyText,
        disabled
    ));

    public RichTextBuilder CallbackButton(string text, string data, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(RichTextFactory.Button(text, callbackData: data, style: style, disabled: disabled));

    public RichTextBuilder CopyTextButton(string text, string copyText, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(RichTextFactory.Button(text, copyText: copyText, style: style, disabled: disabled));

    public RichTextBuilder UrlButton(string text, Uri url, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(RichTextFactory.Button(text, url: url.ToString(), style: style, disabled: disabled));

    public RichTextBuilder UrlButton(string text, string url, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(RichTextFactory.Button(text, url: url, style: style, disabled: disabled));

    public RichTextBuilder WebAppButton(string text, WebAppInfo webApp, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(RichTextFactory.Button(text, webApp: webApp, style: style, disabled: disabled));

    public RichTextBuilder SwitchInlineQueryButton(string text, string switchInlineQuery, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(RichTextFactory.Button(text, switchInlineQuery: switchInlineQuery, style: style, disabled: disabled));

    public RichTextBuilder SwitchCurrentChatButton(string text, string switchInlineQueryCurrentChat, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(RichTextFactory.Button(text, switchInlineQueryCurrentChat: switchInlineQueryCurrentChat, style: style, disabled: disabled));

    public RichTextBuilder SwitchChosenChatButton(string text, SwitchInlineQueryChosenChat switchInlineQueryChosenChat, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(RichTextFactory.Button(text, switchInlineQueryChosenChat: switchInlineQueryChosenChat, style: style, disabled: disabled));

    /// <summary>
    /// Appends a button whose label text is built from the restricted button-text subset
    /// (plain text, custom emoji, date/time — the only entities Bot API allows inside button text).
    /// </summary>
    public RichTextBuilder Button(
        Action<RichButtonTextBuilder> configure,
        RichMessageButtonStyle? style = null,
        string? url = null,
        string? callbackData = null,
        WebAppInfo? webApp = null,
        LoginUrl? loginUrl = null,
        string? switchInlineQuery = null,
        string? switchInlineQueryCurrentChat = null,
        SwitchInlineQueryChosenChat? switchInlineQueryChosenChat = null,
        CopyTextButton? copyText = null,
        bool? disabled = null
    ) => Button(
        BuildButtonText(configure),
        style,
        url,
        callbackData,
        webApp,
        loginUrl,
        switchInlineQuery,
        switchInlineQueryCurrentChat,
        switchInlineQueryChosenChat,
        copyText,
        disabled);

    /// <summary>Appends a callback button whose label text is built from the restricted button-text subset.</summary>
    public RichTextBuilder CallbackButton(Action<RichButtonTextBuilder> configure, string data, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(RichTextFactory.Button(BuildButtonText(configure), callbackData: data, style: style, disabled: disabled));

    /// <summary>Appends a copy-text button whose label text is built from the restricted button-text subset.</summary>
    public RichTextBuilder CopyTextButton(Action<RichButtonTextBuilder> configure, string copyText, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(RichTextFactory.Button(BuildButtonText(configure), copyText: copyText, style: style, disabled: disabled));

    /// <summary>Appends a URL button whose label text is built from the restricted button-text subset.</summary>
    public RichTextBuilder UrlButton(Action<RichButtonTextBuilder> configure, Uri url, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(RichTextFactory.Button(BuildButtonText(configure), url: url is null ? null : url.ToString(), style: style, disabled: disabled));

    /// <summary>Appends a URL button whose label text is built from the restricted button-text subset.</summary>
    public RichTextBuilder UrlButton(Action<RichButtonTextBuilder> configure, string url, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(RichTextFactory.Button(BuildButtonText(configure), url: url, style: style, disabled: disabled));

    /// <summary>Appends a Web App button whose label text is built from the restricted button-text subset.</summary>
    public RichTextBuilder WebAppButton(Action<RichButtonTextBuilder> configure, WebAppInfo webApp, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(RichTextFactory.Button(BuildButtonText(configure), webApp: webApp, style: style, disabled: disabled));

    /// <summary>Appends a switch-inline-query button whose label text is built from the restricted button-text subset.</summary>
    public RichTextBuilder SwitchInlineQueryButton(Action<RichButtonTextBuilder> configure, string switchInlineQuery, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(RichTextFactory.Button(BuildButtonText(configure), switchInlineQuery: switchInlineQuery, style: style, disabled: disabled));

    /// <summary>Appends a switch-current-chat button whose label text is built from the restricted button-text subset.</summary>
    public RichTextBuilder SwitchCurrentChatButton(Action<RichButtonTextBuilder> configure, string switchInlineQueryCurrentChat, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(RichTextFactory.Button(BuildButtonText(configure), switchInlineQueryCurrentChat: switchInlineQueryCurrentChat, style: style, disabled: disabled));

    /// <summary>Appends a switch-chosen-chat button whose label text is built from the restricted button-text subset.</summary>
    public RichTextBuilder SwitchChosenChatButton(Action<RichButtonTextBuilder> configure, SwitchInlineQueryChosenChat switchInlineQueryChosenChat, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(RichTextFactory.Button(BuildButtonText(configure), switchInlineQueryChosenChat: switchInlineQueryChosenChat, style: style, disabled: disabled));

    private static RichText BuildButtonText(Action<RichButtonTextBuilder> configure)
    {
        RichButtonTextBuilder builder = new();
        configure(builder);
        return builder.Build();
    }

    /// <summary>Appends a pre-built node.</summary>
    public RichTextBuilder Add(RichText node)
    {
        _nodes.Add(node);
        return this;
    }

    /// <summary>Appends several pre-built nodes.</summary>
    public RichTextBuilder AddRange(IEnumerable<RichText> nodes)
    {
        _nodes.AddRange(nodes);
        return this;
    }

    /// <summary>Builds the accumulated nodes into a single <see cref="RichText"/>.</summary>
    public RichText Build() => RichTextFactory.Concat(_nodes);

    /// <summary>Builds the accumulated nodes into a single <see cref="RichText"/>.</summary>
    public static implicit operator RichText(RichTextBuilder builder) => builder.Build();
}
