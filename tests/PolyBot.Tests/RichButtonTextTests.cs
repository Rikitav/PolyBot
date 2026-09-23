using Microsoft.VisualStudio.TestTools.UnitTesting;
using PolyBot.RichMessages;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PolyBot.Tests;

[TestClass]
public sealed class RichButtonTextTests
{
    [TestMethod]
    public void Builder_OnlyAllowsRestrictedSubset()
    {
        RichText result = new RichButtonTextBuilder()
            .Plain("hello ")
            .CustomEmoji("emoji-id", "🤖")
            .DateTime("today", new System.DateTime(2026, 1, 1, 0, 0, 0, System.DateTimeKind.Utc), "d")
            .Build();

        RichTextArray array = Assert.IsInstanceOfType<RichTextArray>(result);
        Assert.IsInstanceOfType<RichTextText>(array.Array[0]);
        Assert.IsInstanceOfType<RichTextCustomEmoji>(array.Array[1]);
        Assert.IsInstanceOfType<RichTextDateTime>(array.Array[2]);
    }

    [TestMethod]
    public void SingleNode_IsReturnedAsIs()
    {
        RichText result = new RichButtonTextBuilder().Plain("only").Build();

        RichTextText text = Assert.IsInstanceOfType<RichTextText>(result);
        Assert.AreEqual("only", text.Text);
    }

    [TestMethod]
    public void CallbackButton_Overload_UsesRestrictedText()
    {
        RichText result = new RichTextBuilder()
            .CallbackButton(text => text.Plain("press ").CustomEmoji("e", "🚀"), "payload", RichMessageButtonStyle.Primary)
            .Build();

        RichTextButton button = Assert.IsInstanceOfType<RichTextButton>(result);
        Assert.AreEqual("payload", button.Button.CallbackData);
        Assert.AreEqual(RichMessageButtonStyle.Primary, button.Button.Style);
        Assert.IsInstanceOfType<RichTextArray>(button.Button.Text);
    }

    [TestMethod]
    public void UrlButton_Overload_UsesRestrictedText()
    {
        RichText result = new RichTextBuilder()
            .UrlButton(text => text.Plain("open"), "https://example.com")
            .Build();

        RichTextButton button = Assert.IsInstanceOfType<RichTextButton>(result);
        Assert.AreEqual("https://example.com", button.Button.Url);
        Assert.IsInstanceOfType<RichTextText>(button.Button.Text);
    }

    [TestMethod]
    public void ImplicitConversion_FeedsLowLevelFactory()
    {
        RichButtonTextBuilder label = new RichButtonTextBuilder().Plain("go");

        RichText result = RichTextFactory.Button(label, callbackData: "data");

        RichTextButton button = Assert.IsInstanceOfType<RichTextButton>(result);
        Assert.IsInstanceOfType<RichTextText>(button.Button.Text);
        Assert.AreEqual("data", button.Button.CallbackData);
    }
}
