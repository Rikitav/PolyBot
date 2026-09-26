using Telegram.Bot.Types;

namespace PolyBot.Filters;

/// <summary>
/// Filter that checks if the chat join request was made via the specified invite link.
/// </summary>
public sealed class ChatJoinRequestInviteLinkFilter : ChatJoinRequestFilter
{
    private readonly string _inviteLink;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatJoinRequestInviteLinkFilter"/> class.
    /// </summary>
    /// <param name="inviteLink">The invite link to filter by.</param>
    public ChatJoinRequestInviteLinkFilter(string inviteLink) => _inviteLink = inviteLink;

    /// <inheritdoc/>
    protected override bool CanPass(ChatJoinRequest request) => request.InviteLink?.InviteLink == _inviteLink;
}
