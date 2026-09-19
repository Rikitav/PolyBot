using Telegram.Bot.Types.ReplyMarkups;

namespace PolyBot.Keyboards;

/// <summary>
/// Fluent builder for <see cref="ReplyKeyboardMarkup"/>. Built markups set
/// <see cref="ReplyKeyboardMarkup.ResizeKeyboard"/> so the client sizes the keyboard to its buttons.
/// </summary>
public sealed class ReplyKeyboardBuilder
{
    private readonly List<KeyboardButton> _buttons = new();

    /// <summary>
    /// Appends a button to the flat button list.
    /// </summary>
    public ReplyKeyboardBuilder WithButton(string text)
    {
        _buttons.Add(new KeyboardButton(text));
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
