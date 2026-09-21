using Telegram.Bot.Types.Enums;

namespace PolyBot.Attributes;

/// <summary>
/// Assembly-level override for the update types PolyBot requests from Telegram.
/// By default PolyBot infers this set from declared handlers at compile time.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class AllowedUpdatesAttribute : Attribute
{
    /// <summary>
    /// Additional update types to request even if no handler is declared for them.
    /// </summary>
    public UpdateType[]? Include { get; set; }

    /// <summary>
    /// Update types to exclude from the inferred set.
    /// </summary>
    public UpdateType[]? Exclude { get; set; }

    /// <summary>
    /// When <c>true</c>, request all update types (represented by an empty array,
    /// which Telegram interprets as "all updates except <see cref="UpdateType.ChatMember"/>").
    /// </summary>
    public bool IncludeAll { get; set; }
}
