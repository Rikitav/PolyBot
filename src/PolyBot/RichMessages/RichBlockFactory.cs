/*
 * Copyright (c) 2026 Rikitav Tim4ik
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PolyBot.RichMessages;

/// <summary>
/// Low-level factory for building individual rich message blocks (<see cref="InputRichBlock"/> and related
/// container/cell types). The block type discriminator is set automatically by each Telegram.Bot type's
/// constructor, so callers only fill in the data fields.
/// 
/// </summary>
/// <remarks>
/// For the ergonomic fluent API prefer <see cref="RichMessageBuilder"/>.
/// </remarks>
public static class RichBlockFactory
{
    // ---------- Text-bearing blocks ----------

    /// <summary>
	/// Creates a paragraph block.
	/// </summary>
    public static InputRichBlock Paragraph(RichText text) => new InputRichBlockParagraph { Text = text };

    /// <summary>
	/// Creates a section heading. <paramref name="size"/> is the heading level (1-based).
	/// </summary>
    public static InputRichBlock Heading(RichText text, int size = 1)
        => new InputRichBlockSectionHeading { Text = text, Size = size };

    /// <summary>
	/// Creates a footer block.
	/// </summary>
    public static InputRichBlock Footer(RichText text) => new InputRichBlockFooter { Text = text };

    /// <summary>
	/// Creates a thinking-process block (for AI-style reasoning output).
	/// </summary>
    public static InputRichBlock Thinking(RichText text) => new InputRichBlockThinking { Text = text };

    /// <summary>
	/// Creates a preformatted (code) block with an optional language.
	/// </summary>
    public static InputRichBlock Preformatted(RichText text, string language = "")
        => new InputRichBlockPreformatted { Text = text, Language = language };

    /// <summary>
	/// Creates a block-level mathematical expression.
	/// </summary>
    public static InputRichBlock Math(string expression)
        => new InputRichBlockMathematicalExpression { Expression = expression };

    // ---------- Structural blocks ----------

    /// <summary>
	/// Creates a horizontal divider block.
	/// </summary>
    public static InputRichBlock Divider() => new InputRichBlockDivider();

    /// <summary>
	/// Creates an anchor target block that other blocks/links can reference.
	/// </summary>
    public static InputRichBlock Anchor(string name) => new InputRichBlockAnchor { Name = name };

    /// <summary>
	/// Creates a list item from its child <paramref name="blocks"/>.
	/// </summary>
    public static InputRichBlockListItem ListItem(
        IEnumerable<InputRichBlock> blocks,
        bool hasCheckbox = false,
        bool isChecked = false,
        int? value = null)
        => new InputRichBlockListItem { Blocks = blocks, HasCheckbox = hasCheckbox, IsChecked = isChecked, Value = value };

    /// <summary>
	/// Creates a list block from its <paramref name="items"/>.
	/// </summary>
    public static InputRichBlock List(IEnumerable<InputRichBlockListItem> items)
        => new InputRichBlockList { Items = items };

    /// <summary>
	/// Creates a block quotation from raw child <paramref name="blocks"/>, with optional <paramref name="credit"/>.
	/// </summary>
    public static InputRichBlock BlockQuote(IEnumerable<InputRichBlock> blocks, RichText? credit = null)
        => new InputRichBlockBlockQuotation { Blocks = blocks, Credit = credit };

    /// <summary>
	/// Creates a block quotation wrapping a single <paramref name="text"/> paragraph.
	/// </summary>
    public static InputRichBlock BlockQuote(RichText text, RichText? credit = null)
        => new InputRichBlockBlockQuotation { Blocks = new[] { Paragraph(text) }, Credit = credit };

    /// <summary>
	/// Creates a pull quotation.
	/// </summary>
    public static InputRichBlock PullQuote(RichText text, RichText? credit = null)
        => new InputRichBlockPullQuotation { Text = text, Credit = credit };

    /// <summary>
	/// Creates a collapsible details block.
	/// </summary>
    public static InputRichBlock Details(RichText summary, IEnumerable<InputRichBlock> blocks, bool isOpen = false)
        => new InputRichBlockDetails { Summary = summary, Blocks = blocks, IsOpen = isOpen };

    /// <summary>
	/// Creates a collage of media blocks.
	/// </summary>
    public static InputRichBlock Collage(IEnumerable<InputRichBlock> blocks, RichBlockCaption? caption = null)
        => new InputRichBlockCollage { Blocks = blocks, Caption = caption };

    /// <summary>
	/// Creates a slideshow of media blocks.
	/// </summary>
    public static InputRichBlock Slideshow(IEnumerable<InputRichBlock> blocks, RichBlockCaption? caption = null)
        => new InputRichBlockSlideshow { Blocks = blocks, Caption = caption };

    /// <summary>
	/// Creates a map block.
	/// </summary>
    public static InputRichBlock Map(Location location, int zoom, int width, int height, RichBlockCaption? caption = null)
        => new InputRichBlockMap { Location = location, Zoom = zoom, Width = width, Height = height, Caption = caption };

    // ---------- Tables ----------

    /// <summary>
	/// Creates a table block from a two-dimensional sequence of <see cref="RichBlockTableCell"/>.
	/// </summary>
    public static InputRichBlock Table(
        IEnumerable<IEnumerable<RichBlockTableCell>> cells,
        bool isBordered = false,
        bool isStriped = false,
        RichText? caption = null)
        => new InputRichBlockTable { Cells = cells, IsBordered = isBordered, IsStriped = isStriped, Caption = caption };

    /// <summary>
	/// Creates a table cell.
	/// </summary>
    public static RichBlockTableCell Cell(
        RichText text,
        bool isHeader = false,
        int? colspan = null,
        int? rowspan = null,
        RichBlockTableCellAlign align = RichBlockTableCellAlign.Left,
        RichBlockTableCellValign valign = RichBlockTableCellValign.Top)
        => new RichBlockTableCell { Text = text, IsHeader = isHeader, Colspan = colspan, Rowspan = rowspan, Align = align, Valign = valign };

    // ---------- Media blocks ----------

    /// <summary>
	/// Creates a photo block.
	/// </summary>
    public static InputRichBlock Photo(InputMediaPhoto photo, RichBlockCaption? caption = null)
        => new InputRichBlockPhoto { Photo = photo, Caption = caption };

    /// <summary>
	/// Creates a video block.
	/// </summary>
    public static InputRichBlock Video(InputMediaVideo video, RichBlockCaption? caption = null)
        => new InputRichBlockVideo { Video = video, Caption = caption };

    /// <summary>
	/// Creates an animation (GIF) block.
	/// </summary>
    public static InputRichBlock Animation(InputMediaAnimation animation, RichBlockCaption? caption = null)
        => new InputRichBlockAnimation { Animation = animation, Caption = caption };

    /// <summary>
	/// Creates an audio block.
	/// </summary>
    public static InputRichBlock Audio(InputMediaAudio audio, RichBlockCaption? caption = null)
        => new InputRichBlockAudio { Audio = audio, Caption = caption };

    /// <summary>
	/// Creates a voice-note block.
	/// </summary>
    public static InputRichBlock VoiceNote(InputMediaVoiceNote voiceNote, RichBlockCaption? caption = null)
        => new InputRichBlockVoiceNote { VoiceNote = voiceNote, Caption = caption };

    // ---------- Caption helper ----------

    /// <summary>
	/// Creates a caption (text + optional credit) shared by media/collage/slideshow/map blocks.
	/// </summary>
    public static RichBlockCaption Caption(RichText text, RichText? credit = null)
        => new RichBlockCaption { Text = text, Credit = credit };
}
