using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PolyBot.RichMessages;

/// <summary>
/// Fluent builder that accumulates <see cref="RichMessageButton"/> entries for a buttons
/// block (pass the result to <c>RichMessageBuilder.Buttons(...)</c>).
/// Mirrors the button helpers of <see cref="RichTextBuilder"/>, but produces standalone
/// buttons instead of inline nodes. Labels built from
/// <see cref="Action{RichButtonTextBuilder}"/> overloads are restricted to the entity subset
/// the Bot API allows inside button text (plain text, custom emoji, date/time).
/// </summary>
public sealed class RichButtonsBuilder
{
    private readonly List<RichMessageButton> _buttons = new();

    /// <summary>
    /// Appends a button from a pre-built label <paramref name="text"/> and exactly one action
    /// (<paramref name="url"/>, <paramref name="callbackData"/>, <paramref name="webApp"/>,
    /// <paramref name="loginUrl"/>, <paramref name="switchInlineQuery"/>,
    /// <paramref name="switchInlineQueryCurrentChat"/>,
    /// <paramref name="switchInlineQueryChosenChat"/>, or <paramref name="copyText"/>).
    /// </summary>
    public RichButtonsBuilder Button(
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
    ) => Add(CreateButton(
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
        disabled));

    /// <summary>Appends a callback button with a plain-text label <paramref name="text"/> carrying <paramref name="data"/>.</summary>
    public RichButtonsBuilder CallbackButton(string text, string data, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(CreateButton(text, style, callbackData: data, disabled: disabled));

    /// <summary>Appends a button with label <paramref name="text"/> that copies <paramref name="copyText"/> to the clipboard when pressed.</summary>
    public RichButtonsBuilder CopyTextButton(string text, string copyText, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(CreateButton(text, style, copyText: copyText, disabled: disabled));

    /// <summary>Appends a URL button with label <paramref name="text"/> that opens <paramref name="url"/>.</summary>
    public RichButtonsBuilder UrlButton(string text, Uri url, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(CreateButton(text, style, url: url.ToString(), disabled: disabled));

    /// <summary>Appends a URL button with label <paramref name="text"/> that opens <paramref name="url"/>.</summary>
    public RichButtonsBuilder UrlButton(string text, string url, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(CreateButton(text, style, url: url, disabled: disabled));

    /// <summary>Appends a button with label <paramref name="text"/> that opens the Web App described by <paramref name="webApp"/>.</summary>
    public RichButtonsBuilder WebAppButton(string text, WebAppInfo webApp, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(CreateButton(text, style, webApp: webApp, disabled: disabled));

    /// <summary>Appends a button with label <paramref name="text"/> that inserts the bot's username and <paramref name="switchInlineQuery"/> into the chat's input field.</summary>
    public RichButtonsBuilder SwitchInlineQueryButton(string text, string switchInlineQuery, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(CreateButton(text, style, switchInlineQuery: switchInlineQuery, disabled: disabled));

    /// <summary>Appends a button with label <paramref name="text"/> that inserts the bot's username and <paramref name="switchInlineQueryCurrentChat"/> into the current chat's input field.</summary>
    public RichButtonsBuilder SwitchCurrentChatButton(string text, string switchInlineQueryCurrentChat, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(CreateButton(text, style, switchInlineQueryCurrentChat: switchInlineQueryCurrentChat, disabled: disabled));

    /// <summary>Appends a button with label <paramref name="text"/> that prompts the user to select a chat and inserts the bot's username and the chosen inline query.</summary>
    public RichButtonsBuilder SwitchChosenChatButton(string text, SwitchInlineQueryChosenChat switchInlineQueryChosenChat, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(CreateButton(text, style, switchInlineQueryChosenChat: switchInlineQueryChosenChat, disabled: disabled));

    /// <summary>
    /// Appends a button whose label text is built from the restricted button-text subset
    /// (plain text, custom emoji, datetime — the only entities Bot API allows inside button text).
    /// </summary>
    public RichButtonsBuilder Button(
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
        BuildLabel(configure),
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
    public RichButtonsBuilder CallbackButton(Action<RichButtonTextBuilder> configure, string data, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(CreateButton(BuildLabel(configure), style, callbackData: data, disabled: disabled));

    /// <summary>Appends a copy-text button whose label text is built from the restricted button-text subset.</summary>
    public RichButtonsBuilder CopyTextButton(Action<RichButtonTextBuilder> configure, string copyText, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(CreateButton(BuildLabel(configure), style, copyText: copyText, disabled: disabled));

    /// <summary>Appends a URL button whose label text is built from the restricted button-text subset.</summary>
    public RichButtonsBuilder UrlButton(Action<RichButtonTextBuilder> configure, Uri url, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(CreateButton(BuildLabel(configure), style, url: url.ToString(), disabled: disabled));

    /// <summary>Appends a URL button whose label text is built from the restricted button-text subset.</summary>
    public RichButtonsBuilder UrlButton(Action<RichButtonTextBuilder> configure, string url, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(CreateButton(BuildLabel(configure), style, url: url, disabled: disabled));

    /// <summary>Appends a Web App button whose label text is built from the restricted button-text subset.</summary>
    public RichButtonsBuilder WebAppButton(Action<RichButtonTextBuilder> configure, WebAppInfo webApp, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(CreateButton(BuildLabel(configure), style, webApp: webApp, disabled: disabled));

    /// <summary>Appends a switch-inline-query button whose label text is built from the restricted button-text subset.</summary>
    public RichButtonsBuilder SwitchInlineQueryButton(Action<RichButtonTextBuilder> configure, string switchInlineQuery, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(CreateButton(BuildLabel(configure), style, switchInlineQuery: switchInlineQuery, disabled: disabled));

    /// <summary>Appends a switch-current-chat button whose label text is built from the restricted button-text subset.</summary>
    public RichButtonsBuilder SwitchCurrentChatButton(Action<RichButtonTextBuilder> configure, string switchInlineQueryCurrentChat, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(CreateButton(BuildLabel(configure), style, switchInlineQueryCurrentChat: switchInlineQueryCurrentChat, disabled: disabled));

    /// <summary>Appends a switch-chosen-chat button whose label text is built from the restricted button-text subset.</summary>
    public RichButtonsBuilder SwitchChosenChatButton(Action<RichButtonTextBuilder> configure, SwitchInlineQueryChosenChat switchInlineQueryChosenChat, RichMessageButtonStyle? style = null, bool disabled = false)
        => Add(CreateButton(BuildLabel(configure), style, switchInlineQueryChosenChat: switchInlineQueryChosenChat, disabled: disabled));

    /// <summary>Appends a pre-built button.</summary>
    public RichButtonsBuilder Add(RichMessageButton button)
    {
        _buttons.Add(button);
        return this;
    }

    /// <summary>Appends several pre-built buttons.</summary>
    public RichButtonsBuilder AddRange(IEnumerable<RichMessageButton> buttons)
    {
        _buttons.AddRange(buttons);
        return this;
    }

    /// <summary>Builds the accumulated buttons into an array suitable for a buttons block.</summary>
    public RichMessageButton[] Build() => _buttons.ToArray();

    /// <summary>Builds the accumulated buttons into an array suitable for a buttons block.</summary>
    public static implicit operator RichMessageButton[](RichButtonsBuilder builder) => builder.Build();

    private static RichMessageButton CreateButton(
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
    ) => ((RichTextButton)RichTextFactory.Button(
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
        disabled)).Button;

    private static RichText BuildLabel(Action<RichButtonTextBuilder> configure)
    {
        RichButtonTextBuilder builder = new();
        configure(builder);
        return builder.Build();
    }
}
