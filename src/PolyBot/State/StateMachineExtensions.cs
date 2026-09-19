namespace PolyBot.State;

/// <summary>
/// Convenience transitions over the ordered values of a state enum:
/// <see cref="AdvanceAsync{TEnum}"/> walks forward, <see cref="RollbackAsync{TEnum}"/> walks back.
/// </summary>
public static class StateMachineExtensions
{
    /// <summary>
    /// Moves one step forward in the enum's declaration order: no state → first value →
    /// next value; past the last value the stored key is cleared. Returns the resulting state.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No current update or unresolvable key (only when a write is required; reading a
    /// missing state never throws).
    /// </exception>
    public static async ValueTask<TEnum?> AdvanceAsync<TEnum>(this IStateMachine machine, StateKey keyType = StateKey.UserId, CancellationToken ct = default)
        where TEnum : struct, Enum
    {
        TEnum? current = await machine.GetAsync<TEnum>(keyType, ct).ConfigureAwait(false);
        TEnum[] values = GetValues<TEnum>();
        if (values.Length == 0)
        {
            return null;
        }

        int index = current.HasValue ? Array.IndexOf(values, current.Value) : -1;
        if (index == values.Length - 1)
        {
            await machine.ResetAsync<TEnum>(keyType, ct).ConfigureAwait(false);
            return null;
        }

        TEnum next = values[index + 1];
        await machine.SetAsync(next, keyType, ct).ConfigureAwait(false);
        return next;
    }

    /// <summary>
    /// Moves one step back in the enum's declaration order: any value → previous value;
    /// from the first value (or from no state — a no-op) the stored key is cleared.
    /// Returns the resulting state.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No current update or unresolvable key (only when a write is required; reading a
    /// missing state never throws).
    /// </exception>
    public static async ValueTask<TEnum?> RollbackAsync<TEnum>(this IStateMachine machine, StateKey keyType = StateKey.UserId, CancellationToken ct = default)
        where TEnum : struct, Enum
    {
        TEnum? current = await machine.GetAsync<TEnum>(keyType, ct).ConfigureAwait(false);
        if (!current.HasValue)
        {
            return null;
        }

        TEnum[] values = GetValues<TEnum>();
        int index = Array.IndexOf(values, current.Value);
        if (index <= 0)
        {
            await machine.ResetAsync<TEnum>(keyType, ct).ConfigureAwait(false);
            return null;
        }

        TEnum previous = values[index - 1];
        await machine.SetAsync(previous, keyType, ct).ConfigureAwait(false);
        return previous;
    }

    private static TEnum[] GetValues<TEnum>()
        where TEnum : struct, Enum
    {
        Array raw = Enum.GetValues(typeof(TEnum));
        TEnum[] values = new TEnum[raw.Length];
        raw.CopyTo(values, 0);
        return values;
    }
}
