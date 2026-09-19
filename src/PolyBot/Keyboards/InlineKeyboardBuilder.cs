using Telegram.Bot.Types.ReplyMarkups;

namespace PolyBot.Keyboards;

/// <summary>
/// Fluent builder for <see cref="InlineKeyboardMarkup"/>.
/// </summary>
public sealed class InlineKeyboardBuilder
{
    private readonly List<InlineKeyboardButton> _buttons = new();

    /// <summary>
    /// Appends a callback button to the flat button list.
    /// </summary>
    public InlineKeyboardBuilder WithCallbackButton(string text, string callbackData)
    {
        _buttons.Add(InlineKeyboardButton.WithCallbackData(text, callbackData));
        return this;
    }

    /// <summary>
    /// Builds the markup placing every button on its own row.
    /// </summary>
    public InlineKeyboardMarkup Build()
    {
        List<InlineKeyboardButton[]> rows = new(capacity: _buttons.Count);
        foreach (InlineKeyboardButton button in _buttons)
        {
            rows.Add(new[] { button });
        }

        return new InlineKeyboardMarkup(rows);
    }

    /// <summary>
    /// Builds the markup chunking the flat button list into rows of the given sizes,
    /// repeating the last size for the remaining buttons.
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
