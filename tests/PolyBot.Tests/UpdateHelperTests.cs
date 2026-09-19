using Telegram.Bot.Types;

namespace PolyBot.Tests;

[TestClass]
public sealed class UpdateHelperTests
{
    [TestMethod]
    public void MessageUpdate_ProbesIdsAndText()
    {
        Update update = TestHost.MessageUpdate(30, "probe me", 100, 200);

        Assert.AreEqual(100, update.GetUserId());
        Assert.AreEqual(200, update.GetChatId());
        Assert.AreEqual("probe me", update.GetText());
        Assert.AreEqual(100, update.GetSender()?.Id);
        Assert.AreEqual(200, update.GetChat()?.Id);
        Assert.AreEqual(30, update.GetMessage()?.Id);
    }

    [TestMethod]
    public void CaptionOnlyMessage_TextFallsBackToCaption()
    {
        Update update = new()
        {
            Id = 31,
            Message = new Message
            {
                Id = 31,
                Caption = "media caption",
                From = new User { Id = 100, FirstName = "Test" },
                Chat = new Chat { Id = 200, Type = Telegram.Bot.Types.Enums.ChatType.Private },
            },
        };

        Assert.AreEqual("media caption", update.GetText());
        Assert.AreEqual(31, update.GetMessage()?.Id);
    }

    [TestMethod]
    public void PollUpdate_ProbesReturnNull()
    {
        Update update = TestHost.PollUpdate(32);

        Assert.IsNull(update.GetUserId());
        Assert.IsNull(update.GetChatId());
        Assert.IsNull(update.GetText());
        Assert.IsNull(update.GetSender());
        Assert.IsNull(update.GetChat());
        Assert.IsNull(update.GetMessage());
    }

    [TestMethod]
    public void CallbackUpdate_ProbesItsSender()
    {
        Update update = new()
        {
            Id = 33,
            CallbackQuery = new CallbackQuery
            {
                Id = "cb-33",
                From = new User { Id = 555, FirstName = "Cb" },
                Data = "pressed",
            },
        };

        Assert.AreEqual(555, update.GetUserId());
        Assert.AreEqual(555, update.GetSender()?.Id);
    }
}
