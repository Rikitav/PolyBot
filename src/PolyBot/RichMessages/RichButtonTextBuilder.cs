using Telegram.Bot.Types;

namespace PolyBot.RichMessages;

/// <summary>
/// Fluent builder for the rich text inside a <see cref="RichMessageButton"/> (inline or block-level).
/// The Bot API restricts button text to a subset of rich text entities: only plain text,
/// <see cref="RichTextCustomEmoji"/> and <see cref="RichTextDateTime"/> are allowed. This builder
/// exposes exactly that subset, so non-conforming nodes cannot be composed in the first place.
/// </summary>
/// <remarks>
/// For unrestricted rich text use <see cref="RichTextBuilder"/>; pass the result anywhere a
/// <see cref="RichText"/> is accepted (the conversion is implicit).
/// </remarks>
public sealed class RichButtonTextBuilder
{
    private readonly List<RichText> _nodes = new();

    /// <summary>Appends plain text.</summary>
    public RichButtonTextBuilder Plain(string text)
    {
        _nodes.Add(RichTextFactory.Plain(text));
        return this;
    }

    /// <summary>Appends a custom emoji with the given fallback text.</summary>
    public RichButtonTextBuilder CustomEmoji(string customEmojiId, string alternativeText)
    {
        _nodes.Add(RichTextFactory.CustomEmoji(customEmojiId, alternativeText));
        return this;
    }

    /// <summary>Appends a date/time rendered from a Unix timestamp.</summary>
    public RichButtonTextBuilder DateTime(string text, System.DateTime unixTime, string? format = null)
    {
        _nodes.Add(RichTextFactory.DateTime(RichTextFactory.Plain(text), unixTime, format));
        return this;
    }

    /// <summary>Builds the accumulated nodes into a single <see cref="RichText"/>.</summary>
    public RichText Build() => RichTextFactory.Concat(_nodes);

    /// <summary>Builds the accumulated nodes into a single <see cref="RichText"/>.</summary>
    public static implicit operator RichText(RichButtonTextBuilder builder) => builder.Build();
}
