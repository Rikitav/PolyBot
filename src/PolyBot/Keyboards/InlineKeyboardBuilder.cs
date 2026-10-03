using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace PolyBot.Keyboards;

/// <summary>
/// Fluent builder for <see cref="InlineKeyboardMarkup"/>. Buttons are accumulated flat
/// (one per row in <see cref="Build"/>, chunked by <see cref="Adjust"/>) or composed into
/// explicit rows with <see cref="Row(System.Action{PolyBot.Keyboards.InlineKeyboardRowBuilder})"/>;
/// composed rows precede the flat buttons in the built markup.
/// </summary>
public sealed class InlineKeyboardBuilder
{
    private readonly List<InlineKeyboardButton> _buttons = new();
    private readonly List<InlineKeyboardButton[]> _rows = new();

    /// <summary>
    /// Appends a callback button to the flat button list.
    /// </summary>
    public InlineKeyboardBuilder CallbackButton(string text, string callbackData)
    {
        _buttons.Add(InlineKeyboardButton.WithCallbackData(text, callbackData));
        return this;
    }

    /// <summary>
    /// Appends a URL button to the flat button list.
    /// </summary>
    public InlineKeyboardBuilder UrlButton(string text, string url)
    {
        _buttons.Add(InlineKeyboardButton.WithUrl(text, url));
        return this;
    }

    /// <summary>
    /// Appends a URL button to the flat button list.
    /// </summary>
    public InlineKeyboardBuilder UrlButton(string text, Uri url)
    {
        _buttons.Add(InlineKeyboardButton.WithUrl(text, url.ToString()));
        return this;
    }

    /// <summary>
    /// Appends a Web App button to the flat button list.
    /// </summary>
    public InlineKeyboardBuilder WebAppButton(string text, WebAppInfo webApp)
    {
        _buttons.Add(InlineKeyboardButton.WithWebApp(text, webApp));
        return this;
    }

    /// <summary>
    /// Appends a button that copies <paramref name="copyText"/> to the clipboard when pressed.
    /// </summary>
    public InlineKeyboardBuilder CopyTextButton(string text, string copyText)
    {
        _buttons.Add(InlineKeyboardButton.WithCopyText(text, new CopyTextButton { Text = copyText }));
        return this;
    }

    /// <summary>
    /// Appends a button that inserts the bot's username and <paramref name="query"/> into the chat's input field.
    /// </summary>
    public InlineKeyboardBuilder SwitchInlineQueryButton(string text, string query)
    {
        _buttons.Add(InlineKeyboardButton.WithSwitchInlineQuery(text, query));
        return this;
    }

    /// <summary>
    /// Appends a button that inserts the bot's username and <paramref name="query"/> into the current chat's input field.
    /// </summary>
    public InlineKeyboardBuilder SwitchCurrentChatButton(string text, string query)
    {
        _buttons.Add(InlineKeyboardButton.WithSwitchInlineQueryCurrentChat(text, query));
        return this;
    }

    /// <summary>
    /// Appends a button that prompts the user to select a chat and inserts the bot's username and the chosen inline query.
    /// </summary>
    public InlineKeyboardBuilder SwitchChosenChatButton(string text, SwitchInlineQueryChosenChat chosenChat)
    {
        _buttons.Add(InlineKeyboardButton.WithSwitchInlineQueryChosenChat(text, chosenChat));
        return this;
    }

    /// <summary>
    /// Appends a login button to the flat button list.
    /// </summary>
    public InlineKeyboardBuilder LoginUrlButton(string text, LoginUrl loginUrl)
    {
        _buttons.Add(InlineKeyboardButton.WithLoginUrl(text, loginUrl));
        return this;
    }

    /// <summary>
    /// Appends a pay button to the flat button list.
    /// </summary>
    public InlineKeyboardBuilder PayButton(string text)
    {
        _buttons.Add(InlineKeyboardButton.WithPay(text));
        return this;
    }

    /// <summary>
    /// Appends a pre-built button to the flat button list.
    /// </summary>
    public InlineKeyboardBuilder Add(InlineKeyboardButton button)
    {
        _buttons.Add(button);
        return this;
    }

    /// <summary>
    /// Appends an explicitly composed row; composed rows precede the flat buttons in <see cref="Build"/>.
    /// </summary>
    public InlineKeyboardBuilder Row(Action<InlineKeyboardRowBuilder> configure)
    {
        InlineKeyboardRowBuilder row = new();
        configure(row);
        _rows.Add(row.Build());
        return this;
    }

    /// <summary>
    /// Appends an explicitly composed row of pre-built buttons; composed rows precede the flat buttons in <see cref="Build"/>.
    /// </summary>
    public InlineKeyboardBuilder Row(params InlineKeyboardButton[] buttons)
    {
        _rows.Add(buttons);
        return this;
    }

    /// <summary>
    /// Appends an explicitly composed row of pre-built buttons; composed rows precede the flat buttons in <see cref="Build"/>.
    /// </summary>
    public InlineKeyboardBuilder Row(IEnumerable<InlineKeyboardButton> buttons)
    {
        _rows.Add(buttons.ToArray());
        return this;
    }

    /// <summary>
    /// Builds the markup: composed rows first, then every flat button on its own row.
    /// </summary>
    public InlineKeyboardMarkup Build()
    {
        List<InlineKeyboardButton[]> rows = new(capacity: _rows.Count + _buttons.Count);
        rows.AddRange(_rows);
        foreach (InlineKeyboardButton button in _buttons)
        {
            rows.Add([button]);
        }

        return new InlineKeyboardMarkup(rows);
    }

    /// <summary>
    /// Builds the markup chunking the flat button list into rows of the given sizes,
    /// repeating the last size for the remaining buttons. Rows composed via
    /// <c>Row(...)</c> are not part of the chunked list; use <see cref="Build"/> to include them.
    /// </summary>
    public InlineKeyboardMarkup Adjust(params int[] rowSizes)
    {
        if (rowSizes.Length == 0)
        {
            throw new ArgumentException("At least one row size is required.", nameof(rowSizes));
        }

        List<InlineKeyboardButton[]> rows = new();
        int index = 0;
        int sizeIndex = 0;
        while (index < _buttons.Count)
        {
            int size = rowSizes[Math.Min(sizeIndex, rowSizes.Length - 1)];
            if (size <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rowSizes), size, "Row sizes must be positive.");
            }

            int take = Math.Min(size, _buttons.Count - index);
            rows.Add(_buttons.GetRange(index, take).ToArray());
            index += take;
            sizeIndex++;
        }

        return new InlineKeyboardMarkup(rows);
    }
}

/// <summary>
/// Fluent builder for one row of an <see cref="InlineKeyboardMarkup"/>, used by
/// <see cref="InlineKeyboardBuilder.Row(System.Action{PolyBot.Keyboards.InlineKeyboardRowBuilder})"/>.
/// </summary>
public sealed class InlineKeyboardRowBuilder
{
    private readonly List<InlineKeyboardButton> _buttons = new();

    /// <summary>Appends a callback button.</summary>
    public InlineKeyboardRowBuilder CallbackButton(string text, string callbackData)
    {
        _buttons.Add(InlineKeyboardButton.WithCallbackData(text, callbackData));
        return this;
    }

    /// <summary>Appends a URL button.</summary>
    public InlineKeyboardRowBuilder UrlButton(string text, string url)
    {
        _buttons.Add(InlineKeyboardButton.WithUrl(text, url));
        return this;
    }

    /// <summary>Appends a URL button.</summary>
    public InlineKeyboardRowBuilder UrlButton(string text, Uri url)
    {
        _buttons.Add(InlineKeyboardButton.WithUrl(text, url.ToString()));
        return this;
    }

    /// <summary>Appends a Web App button.</summary>
    public InlineKeyboardRowBuilder WebAppButton(string text, WebAppInfo webApp)
    {
        _buttons.Add(InlineKeyboardButton.WithWebApp(text, webApp));
        return this;
    }

    /// <summary>Appends a button that copies <paramref name="copyText"/> to the clipboard when pressed.</summary>
    public InlineKeyboardRowBuilder CopyTextButton(string text, string copyText)
    {
        _buttons.Add(InlineKeyboardButton.WithCopyText(text, new CopyTextButton { Text = copyText }));
        return this;
    }

    /// <summary>Appends a button that inserts the bot's username and <paramref name="query"/> into the chat's input field.</summary>
    public InlineKeyboardRowBuilder SwitchInlineQueryButton(string text, string query)
    {
        _buttons.Add(InlineKeyboardButton.WithSwitchInlineQuery(text, query));
        return this;
    }

    /// <summary>Appends a button that inserts the bot's username and <paramref name="query"/> into the current chat's input field.</summary>
    public InlineKeyboardRowBuilder SwitchCurrentChatButton(string text, string query)
    {
        _buttons.Add(InlineKeyboardButton.WithSwitchInlineQueryCurrentChat(text, query));
        return this;
    }

    /// <summary>Appends a button that prompts the user to select a chat and inserts the bot's username and the chosen inline query.</summary>
    public InlineKeyboardRowBuilder SwitchChosenChatButton(string text, SwitchInlineQueryChosenChat chosenChat)
    {
        _buttons.Add(InlineKeyboardButton.WithSwitchInlineQueryChosenChat(text, chosenChat));
        return this;
    }

    /// <summary>Appends a login button.</summary>
    public InlineKeyboardRowBuilder LoginUrlButton(string text, LoginUrl loginUrl)
    {
        _buttons.Add(InlineKeyboardButton.WithLoginUrl(text, loginUrl));
        return this;
    }

    /// <summary>Appends a pay button.</summary>
    public InlineKeyboardRowBuilder PayButton(string text)
    {
        _buttons.Add(InlineKeyboardButton.WithPay(text));
        return this;
    }

    /// <summary>Appends a pre-built button.</summary>
    public InlineKeyboardRowBuilder Add(InlineKeyboardButton button)
    {
        _buttons.Add(button);
        return this;
    }

    /// <summary>Builds the accumulated buttons into a row array.</summary>
    public InlineKeyboardButton[] Build() => _buttons.ToArray();

    /// <summary>Builds the accumulated buttons into a row array.</summary>
    public static implicit operator InlineKeyboardButton[](InlineKeyboardRowBuilder builder) => builder.Build();
}
