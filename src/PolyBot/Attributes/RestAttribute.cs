namespace PolyBot.Attributes;

/// <summary>
/// Binds a <c>string</c> handler parameter of a <c>[Command]</c>-restricted handler to
/// everything that follows the positionally consumed argument tokens in the message text,
/// as a single trimmed string.
/// </summary>
/// <remarks>
/// At most one parameter per handler may carry <see cref="RestAttribute"/>. The remainder is
/// never <c>null</c>, and because it absorbs all trailing text, a handler with a rest parameter
/// never reports "found unwanted arguments".
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class RestAttribute : Attribute
{
}
