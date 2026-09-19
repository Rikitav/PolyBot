namespace PolyBot.Awaits;

/// <summary>
/// Numeric identity of a pending await: the (userId, chatId) pair it was registered for,
/// with 0 marking an id not part of the identity (Telegram ids are positive). Registration
/// keys lookups so no key strings are ever formatted.
/// </summary>
internal readonly struct AwaitIdentity : IEquatable<AwaitIdentity>
{
    public AwaitIdentity(long userId, long chatId)
    {
        UserId = userId;
        ChatId = chatId;
    }

    public long UserId { get; }

    public long ChatId { get; }

    public bool Equals(AwaitIdentity other)
    {
        return UserId == other.UserId && ChatId == other.ChatId;
    }

    public override bool Equals(object? obj)
    {
        return obj is AwaitIdentity other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (int)((UserId * 397) ^ ChatId);
        }
    }
}
