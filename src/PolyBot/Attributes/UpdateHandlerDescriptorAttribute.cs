using Telegram.Bot.Types.Enums;

namespace PolyBot.Attributes;

/// <summary>
/// Metadata stamped by the source generator onto each generated handler attribute, recording
/// which update type it handles and the corresponding property on <see cref="Telegram.Bot.Types.Update"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class UpdateHandlerDescriptorAttribute : Attribute
{
    /// <param name="updateType">The update type handled.</param>
    /// <param name="updatePropertyName">The name of the matching property on <c>Update</c>.</param>
    public UpdateHandlerDescriptorAttribute(UpdateType updateType, string updatePropertyName)
    {
        UpdateType = updateType;
        UpdatePropertyName = updatePropertyName;
    }

    /// <summary>
    /// The update type handled by the attributed handler attribute.
    /// </summary>
    public UpdateType UpdateType { get; }

    /// <summary>
    /// The name of the matching property on <see cref="Telegram.Bot.Types.Update"/>.
    /// </summary>
    public string UpdatePropertyName { get; }
}
