namespace PolyBot.Routing;

/// <summary>
/// Outcome of a handler invocation: determines whether routing continues to the next
/// handler for the same update or stops.
/// </summary>
public readonly struct Result : IEquatable<Result>
{
    /// <summary>
    /// Routing continues to the next handler.
    /// </summary>
    public static readonly Result Continue = new(true);

    /// <summary>
    /// Routing stops; no further handlers run.
    /// </summary>
    public static readonly Result StopRouting = new(false);

    /// <summary>
    /// Alias of <see cref="StopRouting"/>: the current handler fully handled the update.
    /// </summary>
    public static Result Handled() => StopRouting;

    private Result(bool continueRouting)
    {
        ContinueRouting = continueRouting;
    }

    /// <summary>
    /// Gets whether routing should continue after this handler.
    /// </summary>
    public bool ContinueRouting { get; }

    /// <inheritdoc />
    public bool Equals(Result other) => ContinueRouting == other.ContinueRouting;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => ContinueRouting.GetHashCode();

    /// <summary>
    /// Compares two <see cref="Result"/> values for equality.
    /// </summary>
    public static bool operator ==(Result left, Result right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="Result"/> values for inequality.
    /// </summary>
    public static bool operator !=(Result left, Result right) => !left.Equals(right);
}
