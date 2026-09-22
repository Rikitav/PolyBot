using Telegram.Bot.Types;

namespace PolyBot.RichMessages;

#if NET10_0_OR_GREATER
/// <summary>
/// Provides extensions for <see cref="RichMessage"/>
/// </summary>
public static class RichMessageExtensions
{
    extension(RichMessage)
    {
        /// <summary>
        /// Obtains <see cref="RichMessageBuilder"/>
        /// </summary>
        /// <returns></returns>
        public static RichMessageBuilder Builder()
            => new RichMessageBuilder();
    }
}

/// <summary>
/// Provides extensions for <see cref="InputRichMessage"/>
/// </summary>
public static class InputRichMessageExtensions
{
    extension(InputRichMessage)
    {
        /// <summary>
        /// Obtains <see cref="RichMessageBuilder"/>
        /// </summary>
        /// <returns></returns>
        public static RichMessageBuilder Builder()
            => new RichMessageBuilder();
    }
}
#endif

/// <summary>
/// Fluent builder that accumulates rich message blocks and produces an <see cref="InputRichMessage"/>
/// ready to be sent via <c>SendRichMessage</c>. String overloads wrap the text in a paragraph/heading/etc.
/// automatically; <see cref="Action{RichTextBuilder}"/> overloads let you build inline formatting inline.
/// </summary>
public sealed class RichMessageBuilder
{
    private readonly List<InputRichBlock> _blocks = new();
    private readonly List<InputRichMessageMedia> _media = new();
    private string? _html;
    private string? _markdown;
    private bool _isRtl;
    private bool _skipEntityDetection;

    /// <summary>Appends a paragraph built from inline text.</summary>
    public RichMessageBuilder Paragraph(string text) => Block(RichBlockFactory.Paragraph(RichTextFactory.Plain(text)));

    /// <summary>Appends a paragraph built from a pre-built <see cref="RichText"/> node.</summary>
    public RichMessageBuilder Paragraph(RichText text) => Block(RichBlockFactory.Paragraph(text));

    /// <summary>Appends a paragraph, configuring its inline content with a <see cref="RichTextBuilder"/>.</summary>
    public RichMessageBuilder Paragraph(Action<RichTextBuilder> configure)
        => Block(RichBlockFactory.Paragraph(BuildText(configure)));

    /// <summary>Appends a heading built from inline text.</summary>
    public RichMessageBuilder Heading(string text, int size = 1)
        => Block(RichBlockFactory.Heading(RichTextFactory.Plain(text), size));

    /// <summary>Appends a heading built from a pre-built <see cref="RichText"/> node.</summary>
    public RichMessageBuilder Heading(RichText text, int size = 1) => Block(RichBlockFactory.Heading(text, size));

    /// <summary>Appends a heading, configuring its inline content with a <see cref="RichTextBuilder"/>.</summary>
    public RichMessageBuilder Heading(Action<RichTextBuilder> configure, int size = 1)
        => Block(RichBlockFactory.Heading(BuildText(configure), size));

    /// <summary>Appends a footer built from inline text.</summary>
    public RichMessageBuilder Footer(string text) => Block(RichBlockFactory.Footer(RichTextFactory.Plain(text)));

    /// <summary>Appends a footer built from a pre-built <see cref="RichText"/> node.</summary>
    public RichMessageBuilder Footer(RichText text) => Block(RichBlockFactory.Footer(text));

    /// <summary>Appends a footer, configuring its inline content with a <see cref="RichTextBuilder"/>.</summary>
    public RichMessageBuilder Footer(Action<RichTextBuilder> configure)
        => Block(RichBlockFactory.Footer(BuildText(configure)));

    /// <summary>Appends a divider.</summary>
    public RichMessageBuilder Divider() => Block(RichBlockFactory.Divider());

    /// <summary>Appends a block-level mathematical expression.</summary>
    public RichMessageBuilder Math(string expression) => Block(RichBlockFactory.Math(expression));

    /// <summary>Appends a thinking-process block built from inline text.</summary>
    public RichMessageBuilder Thinking(string text) => Block(RichBlockFactory.Thinking(RichTextFactory.Plain(text)));

    /// <summary>Appends a thinking-process block, configuring its inline content with a <see cref="RichTextBuilder"/>.</summary>
    public RichMessageBuilder Thinking(Action<RichTextBuilder> configure)
        => Block(RichBlockFactory.Thinking(BuildText(configure)));

    /// <summary>Appends a preformatted (code) block built from inline text.</summary>
    public RichMessageBuilder Preformatted(string text, string language = "")
        => Block(RichBlockFactory.Preformatted(RichTextFactory.Plain(text), language));

    /// <summary>Appends a preformatted (code) block built from a pre-built <see cref="RichText"/> node.</summary>
    public RichMessageBuilder Preformatted(RichText text, string language = "")
        => Block(RichBlockFactory.Preformatted(text, language));

    /// <summary>Alias for <see cref="Preformatted(string, string)"/>.</summary>
    public RichMessageBuilder CodeBlock(string text, string language = "") => Preformatted(text, language);

    /// <summary>Appends an anchor target that links/blocks can reference.</summary>
    public RichMessageBuilder Anchor(string name) => Block(RichBlockFactory.Anchor(name));

    /// <summary>Appends a list from pre-built list items.</summary>
    public RichMessageBuilder List(IEnumerable<InputRichBlockListItem> items) => Block(RichBlockFactory.List(items));

    /// <summary>Appends a list of paragraphs, one per provided text line.</summary>
    public RichMessageBuilder List(IEnumerable<string> lines)
        => Block(RichBlockFactory.List(lines.Select(l => RichBlockFactory.ListItem(new[] { RichBlockFactory.Paragraph(RichTextFactory.Plain(l)) }))));

    /// <summary>Appends a block quotation built from inline text.</summary>
    public RichMessageBuilder BlockQuote(string text, string? credit = null)
        => Block(RichBlockFactory.BlockQuote(RichTextFactory.Plain(text), credit is null ? null : RichTextFactory.Plain(credit)));

    /// <summary>Appends a block quotation built from raw child blocks.</summary>
    public RichMessageBuilder BlockQuote(IEnumerable<InputRichBlock> blocks, RichText? credit = null)
        => Block(RichBlockFactory.BlockQuote(blocks, credit));

    /// <summary>Appends a pull quotation built from inline text.</summary>
    public RichMessageBuilder PullQuote(string text, string? credit = null)
        => Block(RichBlockFactory.PullQuote(RichTextFactory.Plain(text), credit is null ? null : RichTextFactory.Plain(credit)));

    /// <summary>Appends a pull quotation built from a pre-built <see cref="RichText"/> node.</summary>
    public RichMessageBuilder PullQuote(RichText text, RichText? credit = null) => Block(RichBlockFactory.PullQuote(text, credit));

    /// <summary>Appends a collapsible details block whose body is one paragraph per line.</summary>
    public RichMessageBuilder Details(string summary, IEnumerable<string> lines, bool isOpen = false)
        => Block(RichBlockFactory.Details(RichTextFactory.Plain(summary),
            lines.Select(l => RichBlockFactory.Paragraph(RichTextFactory.Plain(l))), isOpen));

    /// <summary>Appends a collapsible details block from raw child blocks.</summary>
    public RichMessageBuilder Details(RichText summary, IEnumerable<InputRichBlock> blocks, bool isOpen = false)
        => Block(RichBlockFactory.Details(summary, blocks, isOpen));

    /// <summary>Appends a collage of media blocks.</summary>
    public RichMessageBuilder Collage(IEnumerable<InputRichBlock> blocks, RichBlockCaption? caption = null)
        => Block(RichBlockFactory.Collage(blocks, caption));

    /// <summary>Appends a slideshow of media blocks.</summary>
    public RichMessageBuilder Slideshow(IEnumerable<InputRichBlock> blocks, RichBlockCaption? caption = null)
        => Block(RichBlockFactory.Slideshow(blocks, caption));

    /// <summary>Appends a map block.</summary>
    public RichMessageBuilder Map(Location location, int zoom, int width, int height, RichBlockCaption? caption = null)
        => Block(RichBlockFactory.Map(location, zoom, width, height, caption));

    /// <summary>Appends a table block.</summary>
    public RichMessageBuilder Table(
        IEnumerable<IEnumerable<RichBlockTableCell>> cells,
        bool isBordered = false,
        bool isStriped = false,
        RichText? caption = null)
        => Block(RichBlockFactory.Table(cells, isBordered, isStriped, caption));

    /// <summary>Appends a table built from a rectangular grid of plain-text cells.</summary>
    public RichMessageBuilder Table(IEnumerable<IEnumerable<string>> textCells, bool isBordered = false, bool isStriped = false, bool firstRowHeader = false)
        => Block(RichBlockFactory.Table(
            textCells.Select((row, ri) => row.Select(c => RichBlockFactory.Cell(RichTextFactory.Plain(c), isHeader: firstRowHeader && ri == 0))),
            isBordered, isStriped));

    /// <summary>Appends a photo block.</summary>
    public RichMessageBuilder Photo(InputMediaPhoto photo, RichBlockCaption? caption = null) => Block(RichBlockFactory.Photo(photo, caption));

    /// <summary>Appends a video block.</summary>
    public RichMessageBuilder Video(InputMediaVideo video, RichBlockCaption? caption = null) => Block(RichBlockFactory.Video(video, caption));

    /// <summary>Appends an animation block.</summary>
    public RichMessageBuilder Animation(InputMediaAnimation animation, RichBlockCaption? caption = null) => Block(RichBlockFactory.Animation(animation, caption));

    /// <summary>Appends an audio block.</summary>
    public RichMessageBuilder Audio(InputMediaAudio audio, RichBlockCaption? caption = null) => Block(RichBlockFactory.Audio(audio, caption));

    /// <summary>Appends a voice-note block.</summary>
    public RichMessageBuilder VoiceNote(InputMediaVoiceNote voiceNote, RichBlockCaption? caption = null) => Block(RichBlockFactory.VoiceNote(voiceNote, caption));

    /// <summary>Appends a pre-built block.</summary>
    public RichMessageBuilder Block(InputRichBlock block)
    {
        _blocks.Add(block);
        return this;
    }

    /// <summary>Attaches embedded media (referenced by id from <see cref="InputRichMessage.Blocks"/>).</summary>
    public RichMessageBuilder Media(string id, IInputRichMedia media)
    {
        _media.Add(new InputRichMessageMedia { Id = id, Media = media });
        return this;
    }

    /// <summary>Attaches a pre-built embedded media entry.</summary>
    public RichMessageBuilder Media(InputRichMessageMedia media)
    {
        _media.Add(media);
        return this;
    }

    /// <summary>Sets an HTML representation of the message (alternative to <see cref="InputRichMessage.Blocks"/>).</summary>
    public RichMessageBuilder WithHtml(string html)
    {
        _html = html;
        return this;
    }

    /// <summary>Sets a Markdown representation of the message (alternative to <see cref="InputRichMessage.Blocks"/>).</summary>
    public RichMessageBuilder WithMarkdown(string markdown)
    {
        _markdown = markdown;
        return this;
    }

    /// <summary>Marks the message as right-to-left.</summary>
    public RichMessageBuilder Rtl()
    {
        _isRtl = true;
        return this;
    }

    /// <summary>Skip Telegram's entity detection for the message.</summary>
    public RichMessageBuilder SkipEntityDetection()
    {
        _skipEntityDetection = true;
        return this;
    }

    /// <summary>Builds the configured <see cref="InputRichMessage"/>.</summary>
    public InputRichMessage Build()
    {
        InputRichMessage message = new()
        {
            Blocks = _blocks,
            Media = _media,
            IsRtl = _isRtl,
            SkipEntityDetection = _skipEntityDetection
        };

        if (_html is not null)
            message.Html = _html;

        if (_markdown is not null)
            message.Markdown = _markdown;

        return message;
    }

    private static RichText BuildText(Action<RichTextBuilder> configure)
    {
        RichTextBuilder builder = new();
        configure(builder);
        return builder.Build();
    }


    /// <summary>Builds the accumulated nodes into a single <see cref="RichText"/>.</summary>
    public static implicit operator InputRichMessage(RichMessageBuilder builder) => builder.Build();
}
