using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace PolyBot.Tests;

[TestClass]
public sealed class KeyboardTests
{
    private static InlineKeyboardButton[][] Rows(InlineKeyboardMarkup markup)
    {
        return markup.InlineKeyboard.Select(static row => row.ToArray()).ToArray();
    }

    [TestMethod]
    public async Task SendCommand_DeliversGeneratedKeyboard()
    {
        TestHostHandle host = TestHost.BuildHost();
        await using ServiceProvider provider = host.Provider;

        await host.Router.HandleUpdateAsync(host.Client, TestHost.CommandUpdate(1, "/send", 100, 200), CancellationToken.None);

        SendMessageRequest sent = host.Client.SentRequests.OfType<SendMessageRequest>().Single();
        Assert.AreEqual("menu", sent.Text);
        InlineKeyboardMarkup markup = sent.ReplyMarkup as InlineKeyboardMarkup
            ?? throw new AssertFailedException("expected an InlineKeyboardMarkup reply");
        InlineKeyboardButton[][] rows = Rows(markup);
        Assert.HasCount(2, rows);
        Assert.HasCount(2, rows[0]);
        Assert.HasCount(2, rows[1]);
        Assert.AreEqual("first:5", rows[0][0].CallbackData);
        Assert.AreEqual("apply:5", rows[1][1].CallbackData);
    }

    [TestMethod]
    public void GeneratedKeyboard_RowsMirrorAttributesAndInterpolate()
    {
        InlineKeyboardButton[][] rows = Rows(TestHandlers.TestKeyboard(5));

        Assert.HasCount(2, rows);
        Assert.AreEqual("First", rows[0][0].Text);
        Assert.AreEqual("first:5", rows[0][0].CallbackData);
        Assert.AreEqual("second:5", rows[0][1].CallbackData);
        Assert.AreEqual("cancel", rows[1][0].CallbackData);
        Assert.AreEqual("apply:5", rows[1][1].CallbackData);
    }

    [TestMethod]
    public void GeneratedKeyboard_PartialPropertyBuildsRows()
    {
        InlineKeyboardButton[][] rows = Rows(TestHandlers.ConfirmKeyboard);

        Assert.HasCount(1, rows);
        Assert.HasCount(2, rows[0]);
        Assert.AreEqual("yes", rows[0][0].CallbackData);
        Assert.AreEqual("no", rows[0][1].CallbackData);
    }

    [TestMethod]
    public void InlineBuilder_AdjustChunksRowsAndBuildIsLinear()
    {
        InlineKeyboardMarkup adjusted = new InlineKeyboardBuilder()
            .CallbackButton("a", "a")
            .CallbackButton("b", "b")
            .CallbackButton("c", "c")
            .CallbackButton("d", "d")
            .CallbackButton("e", "e")
            .Adjust(2, 1, 2);
        InlineKeyboardButton[][] adjustedRows = Rows(adjusted);
        Assert.HasCount(3, adjustedRows);
        Assert.HasCount(2, adjustedRows[0]);
        Assert.HasCount(1, adjustedRows[1]);
        Assert.HasCount(2, adjustedRows[2]);

        InlineKeyboardMarkup linear = new InlineKeyboardBuilder()
            .CallbackButton("a", "a")
            .CallbackButton("b", "b")
            .Build();
        InlineKeyboardButton[][] linearRows = Rows(linear);
        Assert.HasCount(2, linearRows);
        Assert.HasCount(1, linearRows[0]);
        Assert.HasCount(1, linearRows[1]);
    }

    [TestMethod]
    public void InlineBuilder_AllButtonKinds_BindTheirActions()
    {
        InlineKeyboardMarkup markup = new InlineKeyboardBuilder()
            .CallbackButton("cb", "data")
            .UrlButton("url", "https://example.com")
            .UrlButton("uri", new Uri("https://example.org"))
            .CopyTextButton("copy", "copied")
            .WebAppButton("webapp", new Telegram.Bot.Types.WebAppInfo { Url = "https://app.example.com" })
            .SwitchInlineQueryButton("share", "q")
            .SwitchCurrentChatButton("here", "q2")
            .SwitchChosenChatButton("pick", new SwitchInlineQueryChosenChat { Query = "q3" })
            .LoginUrlButton("login", new LoginUrl { Url = "https://login.example.com" })
            .PayButton("pay")
            .Build();

        InlineKeyboardButton[][] rows = Rows(markup);
        Assert.HasCount(10, rows);
        Assert.AreEqual("data", rows[0][0].CallbackData);
        Assert.AreEqual("https://example.com", rows[1][0].Url);
        Assert.AreEqual("https://example.org/", rows[2][0].Url);
        Assert.AreEqual("copied", rows[3][0].CopyText!.Text);
        Assert.AreEqual("https://app.example.com", rows[4][0].WebApp!.Url);
        Assert.AreEqual("q", rows[5][0].SwitchInlineQuery);
        Assert.AreEqual("q2", rows[6][0].SwitchInlineQueryCurrentChat);
        Assert.AreEqual("q3", rows[7][0].SwitchInlineQueryChosenChat!.Query);
        Assert.AreEqual("https://login.example.com", rows[8][0].LoginUrl!.Url);
        Assert.IsTrue(rows[9][0].Pay);
    }

    [TestMethod]
    public void InlineBuilder_ComposedRows_PrecedeFlatButtons()
    {
        InlineKeyboardMarkup markup = new InlineKeyboardBuilder()
            .Row(row => row
                .CallbackButton("Yes", "yes")
                .UrlButton("Docs", "https://example.com"))
            .Row(InlineKeyboardButton.WithCallbackData("Prebuilt", "pre"))
            .CallbackButton("Flat", "flat")
            .Build();

        InlineKeyboardButton[][] rows = Rows(markup);
        Assert.HasCount(3, rows);
        Assert.HasCount(2, rows[0]);
        Assert.AreEqual("yes", rows[0][0].CallbackData);
        Assert.AreEqual("https://example.com", rows[0][1].Url);
        Assert.AreEqual("pre", rows[1][0].CallbackData);
        Assert.HasCount(1, rows[2]);
        Assert.AreEqual("flat", rows[2][0].CallbackData);
    }

    [TestMethod]
    public void ReplyBuilder_AdjustRepeatsLastSizeAndResizes()
    {
        ReplyKeyboardMarkup adjusted = new ReplyKeyboardBuilder()
            .Button("x")
            .Button("y")
            .Button("z")
            .Adjust(2);
        KeyboardButton[][] rows = adjusted.Keyboard.Select(static row => row.ToArray()).ToArray();

        Assert.HasCount(2, rows);
        Assert.HasCount(2, rows[0]);
        Assert.HasCount(1, rows[1]);
        Assert.AreEqual("x", rows[0][0].Text);
        Assert.IsTrue(adjusted.ResizeKeyboard);
    }

    [TestMethod]
    public void ReplyBuilder_RequestButtons_BindTheirRequests()
    {
        ReplyKeyboardMarkup markup = new ReplyKeyboardBuilder()
            .ContactButton("contact")
            .LocationButton("location")
            .PollButton("poll", new KeyboardButtonPollType { Type = PollType.Quiz })
            .RequestUsersButton("users", new KeyboardButtonRequestUsers { RequestId = 7, MaxQuantity = 2 })
            .RequestChatButton("chat", new KeyboardButtonRequestChat { RequestId = 8, ChatIsChannel = true })
            .RequestManagedBotButton("managed", new KeyboardButtonRequestManagedBot { RequestId = 9 })
            .WebAppButton("webapp", new WebAppInfo { Url = "https://app.example.com" })
            .Build();

        KeyboardButton[] buttons = markup.Keyboard.SelectMany(static row => row).ToArray();
        Assert.HasCount(7, buttons);
        Assert.IsTrue(buttons[0].RequestContact);
        Assert.IsTrue(buttons[1].RequestLocation);
        Assert.AreEqual(PollType.Quiz, buttons[2].RequestPoll!.Type);
        Assert.AreEqual(7, buttons[3].RequestUsers!.RequestId);
        Assert.AreEqual(8, buttons[4].RequestChat!.RequestId);
        Assert.IsTrue(buttons[4].RequestChat!.ChatIsChannel);
        Assert.AreEqual(9, buttons[5].RequestManagedBot!.RequestId);
        Assert.AreEqual("https://app.example.com", buttons[6].WebApp!.Url);
    }
}
