using Telegram.Bot.Types;

namespace PolyBot.State;

/// <summary>
/// Default <see cref="IUpdateContextAccessor"/>: an <see cref="System.Threading.AsyncLocal{T}"/>
/// slot assigned by the generated router before routing and cleared in a <c>finally</c> afterwards.
/// </summary>
public sealed class UpdateContextAccessor : IUpdateContextAccessor
{
    private static readonly AsyncLocal<Update?> _current = new();

    /// <inheritdoc />
    public Update? Update
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
