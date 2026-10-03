using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace PolyBot.Keyboards;

/// <summary>
/// Fluent builder for <see cref="ReplyKeyboardMarkup"/>. Built markups set
/// <see cref="ReplyKeyboardMarkup.ResizeKeyboard"/> so the client sizes the keyboard to its buttons.
/// Buttons are accumulated flat (one per row in <see cref="Build"/>, chunked by
/// <see cref="Adjust"/>).
/// </summary>
public sealed class ReplyKeyboardBuilder
{
    private readonly List<KeyboardButton> _buttons = new();

    /// <summary>
    /// Appends a plain text button to the flat button list.
    /// </summary>
    public ReplyKeyboardBuilder Button(string text)
    {
        _buttons.Add(new KeyboardButton(text));
        return this;
    }

    /// <summary>
    /// Appends a button that requests the user's contact information when pressed.
    /// </summary>
    public ReplyKeyboardBuilder ContactButton(string text)
    {
        _buttons.Add(new KeyboardButton(text) { RequestContact = true });
        return this;
    }

    /// <summary>
    /// Appends a button that requests the user's location when pressed.
    /// </summary>
    public ReplyKeyboardBuilder LocationButton(string text)
    {
        _buttons.Add(new KeyboardButton(text) { RequestLocation = true });
        return this;
    }

    /// <summary>
    /// Appends a button that requests a poll of the given kind (any poll when
    /// <paramref name="poll"/> is <c>null</c>) when pressed.
    /// </summary>
    public ReplyKeyboardBuilder PollButton(string text, KeyboardButtonPollType? poll = null)
    {
        _buttons.Add(new KeyboardButton(text) { RequestPoll = poll ?? new KeyboardButtonPollType() });
        return this;
    }

    /// <summary>
    /// Appends a button that requests users matching <paramref name="request"/> when pressed.
    /// </summary>
    public ReplyKeyboardBuilder RequestUsersButton(string text, KeyboardButtonRequestUsers request)
    {
        _buttons.Add(new KeyboardButton(text) { RequestUsers = request });
        return this;
    }

    /// <summary>
    /// Appends a button that requests a chat matching <paramref name="request"/> when pressed.
    /// </summary>
    public ReplyKeyboardBuilder RequestChatButton(string text, KeyboardButtonRequestChat request)
    {
        _buttons.Add(new KeyboardButton(text) { RequestChat = request });
        return this;
    }

    /// <summary>
    /// Appends a button that requests a managed business bot matching <paramref name="request"/> when pressed.
    /// </summary>
    public ReplyKeyboardBuilder RequestManagedBotButton(string text, KeyboardButtonRequestManagedBot request)
    {
        _buttons.Add(new KeyboardButton(text) { RequestManagedBot = request });
        return this;
    }

    /// <summary>
    /// Appends a Web App button to the flat button list.
    /// </summary>
    public ReplyKeyboardBuilder WebAppButton(string text, WebAppInfo webApp)
    {
        _buttons.Add(new KeyboardButton(text) { WebApp = webApp });
        return this;
    }

    /// <summary>
    /// Appends a pre-built button to the flat button list.
    /// </summary>
    public ReplyKeyboardBuilder Add(KeyboardButton button)
    {
        _buttons.Add(button);
        return this;
    }

    /// <summary>
    /// Builds the markup placing every button on its own row.
    /// </summary>
    public ReplyKeyboardMarkup Build()
    {
        List<KeyboardButton[]> rows = new(capacity: _buttons.Count);
        foreach (KeyboardButton button in _buttons)
        {
            rows.Add([button]);
        }

        return new ReplyKeyboardMarkup(rows) { ResizeKeyboard = true };
    }

    /// <summary>
    /// Builds the markup chunking the flat button list into rows of the given sizes,
    /// repeating the last size for the remaining buttons.
    /// </summary>
    public ReplyKeyboardMarkup Adjust(params int[] rowSizes)
    {
        if (rowSizes.Length == 0)
        {
            throw new ArgumentException("At least one row size is required.", nameof(rowSizes));
        }

        List<KeyboardButton[]> rows = new();
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

        return new ReplyKeyboardMarkup(rows) { ResizeKeyboard = true };
    }
}
