namespace PolyBot.Attributes;

/// <summary>
/// Specifies the service key used when resolving a handler parameter from the DI container.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class KeyAttribute : Attribute
{
    /// <param name="serviceKey">The key used to resolve the keyed service.</param>
    public KeyAttribute(object serviceKey)
    {
        ServiceKey = serviceKey;
    }

    /// <summary>
    /// The key used to resolve the keyed service.
    /// </summary>
    public object ServiceKey { get; }
}
