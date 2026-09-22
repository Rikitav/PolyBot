---
title: "Rich Messages: Block Layout, Inline Formatting & Media in PolyBot"
sidebarTitle: "Rich Messages"
description: "Compose structured Telegram rich messages with paragraphs, headings, tables, quotations, and rich inline text using the fluent RichMessageBuilder."
---

PolyBot provides a fluent, type-safe builder hierarchy to construct structured `InputRichMessage` payloads for Telegram. Instead of manually stitching together raw block objects and formatting entities, `RichMessageBuilder` and `RichTextBuilder` let you compose layout blocks, rich inline typography, tables, and embedded media declaratively.

## Composing rich messages

Use `RichMessageBuilder` to assemble high-level document blocks like paragraphs, headings, block quotations, collapsible details, and media.

```csharp
InputRichMessage rich = new RichMessageBuilder()
    .Heading("Welcome to PolyBot", size: 1)
    .Paragraph(b => b
        .Plain("This is a ")
        .Bold("rich message")
        .Plain(" with ")
        .Spoiler("secret content")
        .Plain("!"))
    .Divider()
    .List(["First step", "Second step", "Third step"])
    .Build();
```

<Note>
`RichMessageBuilder` implements an implicit conversion operator to `InputRichMessage`. You can pass the builder directly anywhere an `InputRichMessage` is expected.
</Note>

## Structural & layout blocks

`RichMessageBuilder` provides helpers for every standard block type. Overloads accept raw strings for quick plain text, child block collections, or an inline `Action<RichTextBuilder>` to format text inline.

```csharp
RichMessageBuilder builder = new RichMessageBuilder()
    // Headings (1-based level)
    .Heading("Release Notes", size: 1)
    
    // Standard paragraphs
    .Paragraph("Simple plain-text paragraph")
    .Paragraph(b => b.Plain("Formatted ").Italic("paragraph"))
    
    // Preformatted code blocks
    .Preformatted("git commit -m 'feat: add rich messages'", language: "bash")
    // or using the alias
    .CodeBlock("SELECT * FROM users;", language: "sql")
    
    // Mathematical formulas
    .Math(@"\int_{0}^{\infty} e^{-x^2} dx = \frac{\sqrt{\pi}}{2}")
    
    // Quotations with optional attribution credit
    .BlockQuote("Simplicity is prerequisite for reliability.", credit: "Edsger W. Dijkstra")
    .PullQuote("Highlights or pull quotations stand out visually.")
    
    // Collapsible sections
    .Details("Click to expand logs", ["Row 1: OK", "Row 2: Warning"], isOpen: false)
    
    // Footers and separators
    .Divider()
    .Footer("Generated automatically by PolyBot");
```

### Tables

You can render tables using grid matrices of text or customize cells using `RichBlockFactory.Cell` for alignment and spans.

```csharp
// Simple rectangular grid with header
builder.Table(
    textCells: [
        ["ID", "Status", "Latency"],
        ["srv-1", "Healthy", "12ms"],
        ["srv-2", "Degraded", "145ms"]
    ],
    isBordered: true,
    isStriped: true,
    firstRowHeader: true
);

// Advanced custom table cells
builder.Table(
    cells: [
        [
            RichBlockFactory.Cell(
                RichTextFactory.Bold(RichTextFactory.Plain("Metric")),
                isHeader: true,
                align: RichBlockTableCellAlign.Left),
            RichBlockFactory.Cell(
                RichTextFactory.Bold(RichTextFactory.Plain("Value")),
                isHeader: true,
                align: RichBlockTableCellAlign.Right)
        ],
        [
            RichBlockFactory.Cell(RichTextFactory.Plain("Uptime")),
            RichBlockFactory.Cell(RichTextFactory.Plain("99.98%"), align: RichBlockTableCellAlign.Right)
        ]
    ],
    isBordered: true
);
```

### Media blocks & attachments

Attach media directly to block sequences or associate upload identifiers with embedded media definitions.

```csharp
builder
    .Photo(new InputMediaPhoto(InputFile.FromUri("https://example.com/banner.png")),
        caption: RichBlockFactory.Caption(RichTextFactory.Plain("Overview diagram")))
    .Video(new InputMediaVideo(InputFile.FromFileId("BAADAgAD...")))
    .Animation(new InputMediaAnimation(InputFile.FromUri("https://example.com/demo.gif")))
    .Audio(new InputMediaAudio(InputFile.FromFileId("CQADAgAD...")))
    .VoiceNote(new InputMediaVoiceNote(InputFile.FromFileId("AwADAgAD...")))
    // Reference detached media by ID
    .Media("media-ref-1", customMediaInstance);
```

## Inline text formatting: `RichTextBuilder`

Whenever you pass `Action<RichTextBuilder>` into a block, you can chain inline formatting styles, mentions, links, and entity tokens without escaping delimiters manually.

```csharp
new RichMessageBuilder()
    .Paragraph(t => t
        .Plain("PolyBot supports ")
        .Bold("bold")
        .Plain(", ")
        .Italic("italic")
        .Plain(", ")
        .Underline("underline")
        .Plain(", ")
        .Strikethrough("strikethrough")
        .Plain(", ")
        .Spoiler("spoiler")
        .Plain(", ")
        .Marked("highlighted")
        .Plain(", and ")
        .Code("inline code")
        .Plain("."));
```

### Advanced inline entities

`RichTextBuilder` supports special Telegram entity types such as timestamps, custom emoji, user mentions, and deep anchors:

```csharp
builder.Paragraph(t => t
    // Web links and mentions
    .Url("Documentation", "https://poly-bot.mintlify.site")
    .Mention("Bot Developer", "username")
    .TextMention("Alice", userInstance)
    
    // Commands, hashtags and financial tags
    .BotCommand("/settings", "settings")
    .Hashtag("#polybot", "polybot")
    .Cashtag("$TON", "TON")
    
    // Custom emoji and math
    .CustomEmoji(customEmojiId: "5368324170671202286", alternativeText: "🚀")
    .Math("x^2 + y^2 = r^2")
    
    // Contact & banking data
    .Email("support@example.com", "support@example.com")
    .PhoneNumber("+1234567890", "+1234567890")
    .BankCard("Card Number", "4000 1234 5678 9010")
    
    // Formatted unix timestamps
    .DateTime("Deploy time", DateTime.UtcNow, "HH:mm:ss")
    
    // Internal anchors and references
    .Anchor("section-1")
    .AnchorLink("Jump to Section 1", "section-1"));
```

### Nested formatting

All `RichTextBuilder` formatting methods provide overloads accepting pre-built `RichText` nodes, making deep nesting straightforward:

```csharp
RichText inner = new RichTextBuilder()
    .Bold("clickable bold text")
    .Build();

RichText linked = new RichTextBuilder()
    .Url(inner.ToString()!, "https://example.com")
    .Build();
```

## Low-level composition: `RichBlockFactory` & `RichTextFactory`

For scenarios where dynamic block construction or AST-like manipulation is required, use `RichBlockFactory` and `RichTextFactory` directly. The factory handles setting block discriminators automatically so you only populate content models.

```csharp
// Low-level text node composition
RichText text = RichTextFactory.Concat(
    RichTextFactory.Plain("Execution: "),
    RichTextFactory.Bold(RichTextFactory.Plain("Successful"))
);

// Low-level block composition
InputRichBlock block = RichBlockFactory.BlockQuote(
    text,
    credit: RichTextFactory.Plain("System Monitor")
);

InputRichMessage message = new RichMessageBuilder()
    .Block(block)
    .Build();
```

### Special considerations for `Thinking` blocks

The `Thinking` block represents a collapsible AI thought process placeholder, mapping to the custom `<tg-thinking>` tag.

An `InputRichBlockThinking` block cannot be sent inside standard `SendRichTextMessage` requests. Calling `SendRichTextMessage` with a thinking block will throw an API exception:

```text
Telegram.Bot.Exceptions.ApiRequestException: Bad Request: RICH_MESSAGE_BLOCK_UNSUPPORTED
```

Thinking blocks are restricted by Telegram exclusively to `SendRichMessageDraft` calls and will never appear on received message payloads.

When configuring thinking blocks, you can pair them with Telegram's official [AI Actions custom emoji](https://t.me/addemoji/AIActions?utm_source=gemini) to indicate progress states:

```csharp
InputRichMessage draft = new RichMessageBuilder()
    .Thinking(t => t
        .CustomEmoji(customEmojiId: "5368324170671202286", alternativeText: "🤔")
        .Plain(" Analyzing repository dependencies...")
    )
    .Paragraph("Here is what I found so far.")
    .Build();

```

## Message configurations

`RichMessageBuilder` includes flags to control message text direction and rendering behavior before calling `Build()`:

| Method | Description |
|---|---|
| `WithHtml(string html)` | Supplies raw HTML representation alternative to block sequences |
| `WithMarkdown(string markdown)` | Supplies raw Markdown representation alternative to block sequences |
| `Rtl()` | Flags the rich message as right-to-left layout (`IsRtl = true`) |
| `SkipEntityDetection()` | Bypasses Telegram's automated server-side entity detection parser |
| `Build()` | Validates and compiles the builder into an immutable `InputRichMessage` |
