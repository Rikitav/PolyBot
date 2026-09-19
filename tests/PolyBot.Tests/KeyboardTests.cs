using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Requests;
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
            .WithCallbackButton("a", "a")
            .WithCallbackButton("b", "b")
            .WithCallbackButton("c", "c")
            .WithCallbackButton("d", "d")
            .WithCallbackButton("e", "e")
            .Adjust(2, 1, 2);
        InlineKeyboardButton[][] adjustedRows = Rows(adjusted);
        Assert.HasCount(3, adjustedRows);
        Assert.HasCount(2, adjustedRows[0]);
        Assert.HasCount(1, adjustedRows[1]);
        Assert.HasCount(2, adjustedRows[2]);

        InlineKeyboardMarkup linear = new InlineKeyboardBuilder()
            .WithCallbackButton("a", "a")
            .WithCallbackButton("b", "b")
            .Build();
        InlineKeyboardButton[][] linearRows = Rows(linear);
        Assert.HasCount(2, linearRows);
        Assert.HasCount(1, linearRows[0]);
        Assert.HasCount(1, linearRows[1]);
    }

    [TestMethod]
    public void ReplyBuilder_AdjustRepeatsLastSizeAndResizes()
    {
        ReplyKeyboardMarkup adjusted = new ReplyKeyboardBuilder()
            .WithButton("x")
            .WithButton("y")
            .WithButton("z")
            .Adjust(2);
        KeyboardButton[][] rows = adjusted.Keyboard.Select(static row => row.ToArray()).ToArray();

        Assert.HasCount(2, rows);
        Assert.HasCount(2, rows[0]);
        Assert.HasCount(1, rows[1]);
        Assert.AreEqual("x", rows[0][0].Text);
        Assert.IsTrue(adjusted.ResizeKeyboard);
    }
}
