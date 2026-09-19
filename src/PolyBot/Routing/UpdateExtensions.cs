using Telegram.Bot.Types;

namespace PolyBot.Routing;

/// <summary>
/// Convenience probes over <see cref="Update"/>'s many optional payloads; the payload
/// properties are probed in a fixed order, so an update carrying several payloads
/// resolves deterministically (first match wins).
/// </summary>
public static class UpdateExtensions
{
    /// <summary>
    /// Gets the id of the first user found in the update's payloads; <c>null</c> when none carries one.
    /// </summary>
    public static long? GetUserId(this Update update)
    {
        if (update.Message?.From?.Id is long messageFromId)
        {
            return messageFromId;
        }

        if (update.EditedMessage?.From?.Id is long editedMessageFromId)
        {
            return editedMessageFromId;
        }

        if (update.ChannelPost?.From?.Id is long channelPostFromId)
        {
            return channelPostFromId;
        }

        if (update.EditedChannelPost?.From?.Id is long editedChannelPostFromId)
        {
            return editedChannelPostFromId;
        }

        if (update.CallbackQuery?.From?.Id is long callbackFromId)
        {
            return callbackFromId;
        }

        if (update.InlineQuery?.From?.Id is long inlineFromId)
        {
            return inlineFromId;
        }

        if (update.ChosenInlineResult?.From?.Id is long chosenInlineFromId)
        {
            return chosenInlineFromId;
        }

        if (update.ShippingQuery?.From?.Id is long shippingFromId)
        {
            return shippingFromId;
        }

        if (update.PreCheckoutQuery?.From?.Id is long preCheckoutFromId)
        {
            return preCheckoutFromId;
        }

        if (update.PollAnswer?.User?.Id is long pollAnswerUserId)
        {
            return pollAnswerUserId;
        }

        if (update.MyChatMember?.From?.Id is long myChatMemberFromId)
        {
            return myChatMemberFromId;
        }

        if (update.ChatMember?.From?.Id is long chatMemberFromId)
        {
            return chatMemberFromId;
        }

        if (update.ChatJoinRequest?.From?.Id is long chatJoinRequestFromId)
        {
            return chatJoinRequestFromId;
        }

        if (update.MessageReaction?.User?.Id is long messageReactionUserId)
        {
            return messageReactionUserId;
        }

        if (update.BusinessConnection?.User?.Id is long businessConnectionUserId)
        {
            return businessConnectionUserId;
        }

        return null;
    }

    /// <summary>
    /// Gets the id of the first chat found in the update's payloads; <c>null</c> when none carries one.
    /// </summary>
    public static long? GetChatId(this Update update)
    {
        if (update.Message?.Chat?.Id is long messageChatId)
        {
            return messageChatId;
        }

        if (update.EditedMessage?.Chat?.Id is long editedMessageChatId)
        {
            return editedMessageChatId;
        }

        if (update.ChannelPost?.Chat?.Id is long channelPostChatId)
        {
            return channelPostChatId;
        }

        if (update.EditedChannelPost?.Chat?.Id is long editedChannelPostChatId)
        {
            return editedChannelPostChatId;
        }

        if (update.CallbackQuery?.Message?.Chat?.Id is long callbackMessageChatId)
        {
            return callbackMessageChatId;
        }

        if (update.PollAnswer?.VoterChat?.Id is long pollAnswerVoterChatId)
        {
            return pollAnswerVoterChatId;
        }

        if (update.MyChatMember?.Chat?.Id is long myChatMemberChatId)
        {
            return myChatMemberChatId;
        }

        if (update.ChatMember?.Chat?.Id is long chatMemberChatId)
        {
            return chatMemberChatId;
        }

        if (update.ChatJoinRequest?.Chat?.Id is long chatJoinRequestChatId)
        {
            return chatJoinRequestChatId;
        }

        if (update.MessageReaction?.Chat?.Id is long messageReactionChatId)
        {
            return messageReactionChatId;
        }

        if (update.ChatBoost?.Chat?.Id is long chatBoostChatId)
        {
            return chatBoostChatId;
        }

        if (update.BusinessConnection?.UserChatId is long businessConnectionChatId)
        {
            return businessConnectionChatId;
        }

        return null;
    }

    /// <summary>
    /// Gets the first <see cref="User"/> found in the update's payloads.
    /// </summary>
    public static User? GetSender(this Update update)
    {
        if (update.Message?.From is User messageFrom)
        {
            return messageFrom;
        }

        if (update.EditedMessage?.From is User editedMessageFrom)
        {
            return editedMessageFrom;
        }

        if (update.ChannelPost?.From is User channelPostFrom)
        {
            return channelPostFrom;
        }

        if (update.EditedChannelPost?.From is User editedChannelPostFrom)
        {
            return editedChannelPostFrom;
        }

        if (update.CallbackQuery?.From is User callbackFrom)
        {
            return callbackFrom;
        }

        if (update.InlineQuery?.From is User inlineFrom)
        {
            return inlineFrom;
        }

        if (update.ChosenInlineResult?.From is User chosenInlineFrom)
        {
            return chosenInlineFrom;
        }

        if (update.ShippingQuery?.From is User shippingFrom)
        {
            return shippingFrom;
        }

        if (update.PreCheckoutQuery?.From is User preCheckoutFrom)
        {
            return preCheckoutFrom;
        }

        if (update.PollAnswer?.User is User pollAnswerUser)
        {
            return pollAnswerUser;
        }

        if (update.MyChatMember?.From is User myChatMemberFrom)
        {
            return myChatMemberFrom;
        }

        if (update.ChatMember?.From is User chatMemberFrom)
        {
            return chatMemberFrom;
        }

        if (update.ChatJoinRequest?.From is User chatJoinRequestFrom)
        {
            return chatJoinRequestFrom;
        }

        if (update.MessageReaction?.User is User messageReactionUser)
        {
            return messageReactionUser;
        }

        if (update.BusinessConnection?.User is User businessConnectionUser)
        {
            return businessConnectionUser;
        }

        return null;
    }

    /// <summary>
    /// Gets the first <see cref="Chat"/> found in the update's payloads.
    /// <see cref="Update.BusinessConnection"/> exposes only its <c>UserChatId</c>, so it
    /// contributes to <see cref="GetChatId"/> but not to this method.
    /// </summary>
    public static Chat? GetChat(this Update update)
    {
        if (update.Message?.Chat is Chat messageChat)
        {
            return messageChat;
        }

        if (update.EditedMessage?.Chat is Chat editedMessageChat)
        {
            return editedMessageChat;
        }

        if (update.ChannelPost?.Chat is Chat channelPostChat)
        {
            return channelPostChat;
        }

        if (update.EditedChannelPost?.Chat is Chat editedChannelPostChat)
        {
            return editedChannelPostChat;
        }

        if (update.CallbackQuery?.Message?.Chat is Chat callbackMessageChat)
        {
            return callbackMessageChat;
        }

        if (update.PollAnswer?.VoterChat is Chat pollAnswerVoterChat)
        {
            return pollAnswerVoterChat;
        }

        if (update.MyChatMember?.Chat is Chat myChatMemberChat)
        {
            return myChatMemberChat;
        }

        if (update.ChatMember?.Chat is Chat chatMemberChat)
        {
            return chatMemberChat;
        }

        if (update.ChatJoinRequest?.Chat is Chat chatJoinRequestChat)
        {
            return chatJoinRequestChat;
        }

        if (update.MessageReaction?.Chat is Chat messageReactionChat)
        {
            return messageReactionChat;
        }

        if (update.ChatBoost?.Chat is Chat chatBoostChat)
        {
            return chatBoostChat;
        }

        return null;
    }

    /// <summary>
    /// Gets the message payload: message, edited message, channel post, or edited channel post (first match wins).
    /// </summary>
    public static Message? GetMessage(this Update update)
    {
        return update.Message ?? update.EditedMessage ?? update.ChannelPost ?? update.EditedChannelPost;
    }

    /// <summary>
    /// Gets the message text, falling back to its caption — media captions are typed
    /// into like message text.
    /// </summary>
    public static string? GetText(this Update update)
    {
        Message? message = update.GetMessage();
        return message?.Text ?? message?.Caption;
    }
}
