using Microsoft.VisualStudio.TestTools.UnitTesting;
using PolyBot.Filters;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PolyBot.Tests;

[TestClass]
public sealed class MigratedFiltersTests
{
    [TestMethod]
    public void MessageTypeFilter_MatchesComputedMessageType()
    {
        MessageTypeFilter filter = new(MessageType.Text);
        Assert.IsTrue(filter.CanPass(MessageUpdate(1, "hello")));

        MessageTypeFilter photoFilter = new(MessageType.Photo);
        Assert.IsFalse(photoFilter.CanPass(MessageUpdate(1, "hello")));
    }

    [TestMethod]
    public void TextContainsFilter_And_WordBoundary()
    {
        Assert.IsTrue(new TextContainsFilter("cat").CanPass(MessageUpdate(1, "a cat")));
        Assert.IsFalse(new TextContainsFilter("cat", StringComparison.Ordinal).CanPass(MessageUpdate(1, "Cat")));

        TextContainsWordFilter word = new("cat");
        Assert.IsTrue(word.CanPass(MessageUpdate(1, "a cat")));
        Assert.IsFalse(word.CanPass(MessageUpdate(1, "category")));
    }

    [TestMethod]
    public void MessageHasEntityFilter_MatchesContentSubstring()
    {
        Update update = MessageUpdate(1, "hello @world");
        update.Message!.Entities =
        [
            new MessageEntity { Type = MessageEntityType.Mention, Offset = 6, Length = 6 }
        ];

        Assert.IsTrue(new MessageHasEntityFilter(MessageEntityType.Mention, "@world").CanPass(update));
        Assert.IsFalse(new MessageHasEntityFilter(MessageEntityType.Mention, "@mars").CanPass(update));
        Assert.IsFalse(new MessageHasEntityFilter(MessageEntityType.Hashtag, "@world").CanPass(update));
    }

    [TestMethod]
    public void MessageHasReplyFilter_WalksChainDepth()
    {
        Update shallow = MessageUpdate(1, "hi");
        Assert.IsFalse(new MessageHasReplyFilter().CanPass(shallow));

        Update depthOne = MessageUpdate(1, "hi");
        depthOne.Message!.ReplyToMessage = new Message { Id = 10 };
        Assert.IsTrue(new MessageHasReplyFilter().CanPass(depthOne));
        Assert.IsFalse(new MessageHasReplyFilter(2).CanPass(depthOne));

        depthOne.Message.ReplyToMessage.ReplyToMessage = new Message { Id = 11 };
        Assert.IsTrue(new MessageHasReplyFilter(2).CanPass(depthOne));
    }

    [TestMethod]
    public void MeRepliedFilter_RequiresOptionsWithBotUsername()
    {
        Update update = MessageUpdate(1, "hi");
        update.Message!.ReplyToMessage = new Message
        {
            Id = 10,
            From = new User { Id = 99, FirstName = "Bot", Username = "mybot" },
        };

        Assert.IsFalse(new MeRepliedFilter().CanPass(update));
        Assert.IsFalse(new MeRepliedFilter(new PolyBotOptions()).CanPass(update));
        Assert.IsTrue(new MeRepliedFilter(new PolyBotOptions { BotUsername = "mybot" }).CanPass(update));
        Assert.IsTrue(new MeRepliedFilter(new PolyBotOptions { BotUsername = "@mybot" }).CanPass(update));
    }

    [TestMethod]
    public void MentionedFilter_ExplicitUsernameOrBotUsername()
    {
        Update update = MessageUpdate(1, "hi @mybot");
        update.Message!.Entities =
        [
            new MessageEntity { Type = MessageEntityType.Mention, Offset = 3, Length = 6 }
        ];

        Assert.IsTrue(new MentionedFilter("mybot").CanPass(update));
        Assert.IsFalse(new MentionedFilter("otherbot").CanPass(update));
        Assert.IsTrue(new MentionedFilter(options: new PolyBotOptions { BotUsername = "mybot" }).CanPass(update));
        Assert.IsFalse(new MentionedFilter().CanPass(update));
    }

    [TestMethod]
    public void CallbackDataFilters_MatchOnData()
    {
        Update update = new() { Id = 1, CallbackQuery = new CallbackQuery { Id = "cb", Data = "menu:open" } };

        Assert.IsTrue(new CallbackDataStartsWithFilter("menu:").CanPass(update));
        Assert.IsTrue(new CallbackDataContainsFilter("open").CanPass(update));
        Assert.IsFalse(new CallbackDataEndsWithFilter("close").CanPass(update));
        Assert.IsTrue(new CallbackDataFilter("menu:open").CanPass(update));
    }

    [TestMethod]
    public void InlineQueryRegexFilter_MatchesQuery()
    {
        Update update = new()
        {
            Id = 1,
            InlineQuery = new InlineQuery
            {
                Id = "q",
                From = new User { Id = 1, FirstName = "T" },
                Query = "find something",
                Offset = string.Empty,
            },
        };

        Assert.IsTrue(new InlineQueryRegexFilter("^find").CanPass(update));
        Assert.IsFalse(new InlineQueryRegexFilter("^lost").CanPass(update));
    }

    [TestMethod]
    public void PollTypeFilter_MatchesPollType()
    {
        Update update = TestHost.PollUpdate(1);
        Assert.IsTrue(new PollTypeFilter(PollType.Regular).CanPass(update));
        Assert.IsFalse(new PollTypeFilter(PollType.Quiz).CanPass(update));
        Assert.IsTrue(new PollIsClosedFilter(false).CanPass(update));
    }

    [TestMethod]
    public void ChatJoinRequestInviteLinkFilter_MatchesInviteLink()
    {
        Update update = new()
        {
            Id = 1,
            ChatJoinRequest = new ChatJoinRequest
            {
                Chat = new Chat { Id = 1, Type = ChatType.Supergroup },
                From = new User { Id = 1, FirstName = "T" },
                InviteLink = new ChatInviteLink { InviteLink = "https://t.me/+abc" },
            },
        };

        Assert.IsTrue(new ChatJoinRequestInviteLinkFilter("https://t.me/+abc").CanPass(update));
        Assert.IsFalse(new ChatJoinRequestInviteLinkFilter("https://t.me/+xyz").CanPass(update));
    }

    [TestMethod]
    public void EnvironmentVariableFilter_ExistsAndValueSemantics()
    {
        const string variable = "POLYBOT_TEST_FILTER_VAR";
        string? original = Environment.GetEnvironmentVariable(variable);
        try
        {
            Environment.SetEnvironmentVariable(variable, "expected");

            Assert.IsTrue(new EnvironmentVariableFilter(variable).CanPass(MessageUpdate(1, "x")));
            Assert.IsTrue(new EnvironmentVariableFilter(variable, "expected").CanPass(MessageUpdate(1, "x")));
            Assert.IsFalse(new EnvironmentVariableFilter(variable, "other").CanPass(MessageUpdate(1, "x")));

            Environment.SetEnvironmentVariable(variable, null);
            Assert.IsFalse(new EnvironmentVariableFilter(variable).CanPass(MessageUpdate(1, "x")));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, original);
        }
    }

    [TestMethod]
    public void DiceThrowedFilter_MatchesValueAndEmoji()
    {
        Update update = MessageUpdate(1, null);
        update.Message!.Dice = new Dice { Emoji = "🎲", Value = 5 };

        Assert.IsTrue(new DiceThrowedFilter(5).CanPass(update));
        Assert.IsFalse(new DiceThrowedFilter(3).CanPass(update));
        Assert.IsTrue(new DiceThrowedFilter(DiceType.Dice, 5).CanPass(update));
        Assert.IsFalse(new DiceThrowedFilter(DiceType.Darts, 5).CanPass(update));
    }

    [TestMethod]
    public void FromBotFilter_MatchesBotSender()
    {
        Update fromBot = MessageUpdate(1, "hi");
        fromBot.Message!.From = new User { Id = 99, FirstName = "Bot", IsBot = true };
        Assert.IsTrue(new FromBotFilter().CanPass(fromBot));
        Assert.IsFalse(new FromBotFilter().CanPass(MessageUpdate(1, "hi")));
    }

    [TestMethod]
    public void MessageChatIdFilter_MatchesChat()
    {
        Update update = MessageUpdate(1, "hi");
        update.Message!.Chat = new Chat { Id = 42, Type = ChatType.Private };
        Assert.IsTrue(new MessageChatIdFilter(42).CanPass(update));
        Assert.IsFalse(new MessageChatIdFilter(43).CanPass(update));
    }

    private static Update MessageUpdate(int id, string? text)
    {
        return new Update
        {
            Id = id,
            Message = new Message
            {
                Id = id,
                Text = text,
                From = new User { Id = 1, FirstName = "Test" },
                Chat = new Chat { Id = 1, Type = ChatType.Private },
            },
        };
    }
}
