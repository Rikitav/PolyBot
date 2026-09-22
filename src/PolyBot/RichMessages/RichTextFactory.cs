using Telegram.Bot.Types;

namespace PolyBot.RichMessages;

/// <summary>
/// Low-level factory for building individual <see cref="RichText"/> nodes used inside rich message blocks.
/// Most wrapping nodes (bold, italic, ...) take an existing <see cref="RichText"/> as their child; use
/// <see cref="Plain"/> to create the leaf text and <see cref="Concat(IEnumerable{RichText})"/> to compose several nodes.
/// </summary>
/// <remarks>
/// For an ergonomic, string-driven fluent API prefer <see cref="RichTextBuilder"/>.
/// </remarks>
public static class RichTextFactory
{
    /// <summary>Creates a plain (unformatted) text node.</summary>
    public static RichText Plain(string text) => new RichTextText { Text = text };

    /// <summary>Creates a custom-emoji node rendered with the given fallback text.</summary>
    public static RichText CustomEmoji(string customEmojiId, string alternativeText)
        => new RichTextCustomEmoji { CustomEmojiId = customEmojiId, AlternativeText = alternativeText };

    /// <summary>Creates a standalone mathematical expression node.</summary>
    public static RichText Math(string expression)
        => new RichTextMathematicalExpression { Expression = expression };

    /// <summary>Creates an anchor target that other nodes can link to via <see cref="AnchorLink"/>.</summary>
    public static RichText Anchor(string name) => new RichTextAnchor { Name = name };

    /// <summary>Wraps <paramref name="child"/> in bold formatting.</summary>
    public static RichText Bold(RichText child) => new RichTextBold { Text = child };

    /// <summary>Wraps <paramref name="child"/> in italic formatting.</summary>
    public static RichText Italic(RichText child) => new RichTextItalic { Text = child };

    /// <summary>Wraps <paramref name="child"/> in underline formatting.</summary>
    public static RichText Underline(RichText child) => new RichTextUnderline { Text = child };

    /// <summary>Wraps <paramref name="child"/> in strikethrough formatting.</summary>
    public static RichText Strikethrough(RichText child) => new RichTextStrikethrough { Text = child };

    /// <summary>Wraps <paramref name="child"/> in a spoiler.</summary>
    public static RichText Spoiler(RichText child) => new RichTextSpoiler { Text = child };

    /// <summary>Wraps <paramref name="child"/> in marked (highlighted) formatting.</summary>
    public static RichText Marked(RichText child) => new RichTextMarked { Text = child };

    /// <summary>Wraps <paramref name="child"/> in inline code formatting.</summary>
    public static RichText Code(RichText child) => new RichTextCode { Text = child };

    /// <summary>Wraps <paramref name="child"/> as subscript.</summary>
    public static RichText Subscript(RichText child) => new RichTextSubscript { Text = child };

    /// <summary>Wraps <paramref name="child"/> as superscript.</summary>
    public static RichText Superscript(RichText child) => new RichTextSuperscript { Text = child };

    /// <summary>Wraps <paramref name="child"/> as a clickable URL.</summary>
    public static RichText Url(RichText child, string url) => new RichTextUrl { Text = child, Url = url };

    /// <summary>Wraps <paramref name="child"/> as an @username mention.</summary>
    public static RichText Mention(RichText child, string username)
        => new RichTextMention { Text = child, Username = username };

    /// <summary>Wraps <paramref name="child"/> as a text mention of a specific <paramref name="user"/>.</summary>
    public static RichText TextMention(RichText child, User user)
        => new RichTextTextMention { Text = child, User = user };

    /// <summary>Wraps <paramref name="child"/> as a bot command reference.</summary>
    public static RichText BotCommand(RichText child, string command)
        => new RichTextBotCommand { Text = child, BotCommand = command };

    /// <summary>Wraps <paramref name="child"/> as a #hashtag.</summary>
    public static RichText Hashtag(RichText child, string hashtag)
        => new RichTextHashtag { Text = child, Hashtag = hashtag };

    /// <summary>Wraps <paramref name="child"/> as a $cashtag.</summary>
    public static RichText Cashtag(RichText child, string cashtag)
        => new RichTextCashtag { Text = child, Cashtag = cashtag };

    /// <summary>Wraps <paramref name="child"/> as an e-mail address.</summary>
    public static RichText Email(RichText child, string email)
        => new RichTextEmailAddress { Text = child, EmailAddress = email };

    /// <summary>Wraps <paramref name="child"/> as a phone number.</summary>
    public static RichText PhoneNumber(RichText child, string phone)
        => new RichTextPhoneNumber { Text = child, PhoneNumber = phone };

    /// <summary>Wraps <paramref name="child"/> as a bank card number.</summary>
    public static RichText BankCard(RichText child, string number)
        => new RichTextBankCardNumber { Text = child, BankCardNumber = number };

    /// <summary>Wraps <paramref name="child"/> as a date/time rendered from a Unix timestamp.</summary>
    public static RichText DateTime(RichText child, System.DateTime unixTime, string? format = null)
        => new RichTextDateTime { Text = child, UnixTime = unixTime, DateTimeFormat = format ?? string.Empty };

    /// <summary>Wraps <paramref name="child"/> as a link to an <see cref="Anchor"/> defined elsewhere.</summary>
    public static RichText AnchorLink(RichText child, string anchorName)
        => new RichTextAnchorLink { Text = child, AnchorName = anchorName };

    /// <summary>Wraps <paramref name="child"/> as a named reference.</summary>
    public static RichText Reference(RichText child, string name)
        => new RichTextReference { Text = child, Name = name };

    /// <summary>Wraps <paramref name="child"/> as a link to a named <see cref="Reference"/>.</summary>
    public static RichText ReferenceLink(RichText child, string referenceName)
        => new RichTextReferenceLink { Text = child, ReferenceName = referenceName };

    /// <summary>Composes several nodes into a single <see cref="RichText"/>. A single node is returned as-is.</summary>
    public static RichText Concat(params RichText[] nodes) => nodes.Length switch
    {
        0 => Plain(string.Empty),
        1 => nodes[0],
        _ => new RichTextArray { Array = nodes }
    };

    /// <summary>Composes several nodes into a single <see cref="RichText"/>.</summary>
    public static RichText Concat(IEnumerable<RichText> nodes) => Concat(nodes.ToArray());
}
