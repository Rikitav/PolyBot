using PolyBot.RichMessages;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PolyBot.Tests;

[TestClass]
public sealed class RichButtonsBuilderTests
{
    [TestMethod]
    public void CallbackButton_BindsDataAndStyle()
    {
        RichMessageButton[] buttons = new RichButtonsBuilder()
            .CallbackButton("Press me", "payload", RichMessageButtonStyle.Primary)
            .Build();

        Assert.HasCount(1, buttons);
        Assert.AreEqual("payload", buttons[0].CallbackData);
        Assert.AreEqual(RichMessageButtonStyle.Primary, buttons[0].Style);
        RichTextText label = Assert.IsInstanceOfType<RichTextText>(buttons[0].Text);
        Assert.AreEqual("Press me", label.Text);
    }

    [TestMethod]
    public void MixedButtons_PreserveDeclarationOrder()
    {
        RichMessageButton[] buttons = new RichButtonsBuilder()
            .UrlButton("Docs", "https://example.com")
            .CallbackButton("OK", "ok")
            .CopyTextButton("Copy", "copied text")
            .WebAppButton("Open", new WebAppInfo { Url = "https://app.example.com" })
            .SwitchInlineQueryButton("Share", "query")
            .SwitchCurrentChatButton("Here", "query2")
            .SwitchChosenChatButton("Pick", new SwitchInlineQueryChosenChat { Query = "q3" })
            .Build();

        Assert.HasCount(7, buttons);
        Assert.AreEqual("https://example.com", buttons[0].Url);
        Assert.AreEqual("ok", buttons[1].CallbackData);
        Assert.AreEqual("copied text", buttons[2].CopyText!.Text);
        Assert.AreEqual("https://app.example.com", buttons[3].WebApp!.Url);
        Assert.AreEqual("query", buttons[4].SwitchInlineQuery);
        Assert.AreEqual("query2", buttons[5].SwitchInlineQueryCurrentChat);
        Assert.AreEqual("q3", buttons[6].SwitchInlineQueryChosenChat!.Query);
    }

    [TestMethod]
    public void DisabledButton_SetsDisabledMarker()
    {
        RichMessageButton button = new RichButtonsBuilder()
            .CallbackButton("Nope", "nope", disabled: true)
            .Build()[0];

        Assert.IsNotNull(button.Disabled);
    }

    [TestMethod]
    public void RestrictedTextOverload_BuildsLabelFromSubset()
    {
        RichMessageButton button = new RichButtonsBuilder()
            .CallbackButton(text => text.Plain("launch ").CustomEmoji("e", "🚀"), "go")
            .Build()[0];

        Assert.AreEqual("go", button.CallbackData);
        RichTextArray label = Assert.IsInstanceOfType<RichTextArray>(button.Text);
        Assert.IsInstanceOfType<RichTextText>(label.Array[0]);
        Assert.IsInstanceOfType<RichTextCustomEmoji>(label.Array[1]);
    }

    [TestMethod]
    public void Button_Overload_SupportsLoginUrl()
    {
        RichMessageButton button = new RichButtonsBuilder()
            .Button("Login", loginUrl: new LoginUrl { Url = "https://login.example.com" })
            .Build()[0];

        Assert.AreEqual("https://login.example.com", button.LoginUrl!.Url);
    }

    [TestMethod]
    public void ImplicitConversion_ProducesArray()
    {
        RichMessageButton[] buttons = new RichButtonsBuilder()
            .CallbackButton("A", "a")
            .CallbackButton("B", "b");

        Assert.HasCount(2, buttons);
    }

    [TestMethod]
    public void RichMessageBuilder_ButtonsOverload_UsesBuilder()
    {
        InputRichMessage message = new RichMessageBuilder()
            .Paragraph("Confirm?")
            .Buttons(buttons => buttons
                .CallbackButton("Yes", "yes", RichMessageButtonStyle.Success)
                .CallbackButton("No", "no"), RichBlockTableCellAlign.Center)
            .Build();

        InputRichBlockButtons block = Assert.IsInstanceOfType<InputRichBlockButtons>(message.Blocks!.ElementAt(1));
        Assert.AreEqual(RichBlockTableCellAlign.Center, block.Align);
        List<RichMessageButton> buttons = block.Buttons!.ToList();
        Assert.HasCount(2, buttons);
        Assert.AreEqual("yes", buttons[0].CallbackData);
    }
}
