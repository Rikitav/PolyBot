namespace PolyBot.Commands;

/// <summary>
/// Thrown by <see cref="CommandHelper.ParseArgs"/> when the tokens following a command
/// cannot be bound to the handler's argument specifications.
/// </summary>
public sealed class CommandArgsParseException : Exception
{
    /// <summary>
    /// Creates the exception with an error message.
    /// </summary>
    public CommandArgsParseException(string message)
        : base(message)
    {
        CommandText = string.Empty;
    }

    /// <summary>
    /// Creates the exception with an error message.
    /// </summary>
    public CommandArgsParseException(string message, string? argumentName, string commandText)
        : base(message)
    {
        ArgumentName = argumentName;
        CommandText = commandText;
    }

    /// <summary>
    /// Creates the exception with an error message and an underlying parse error.
    /// </summary>
    public CommandArgsParseException(string message, string? argumentName, string commandText, Exception? innerException)
        : base(message, innerException)
    {
        ArgumentName = argumentName;
        CommandText = commandText;
    }

    /// <summary>
    /// The argument that failed to bind, or <c>null</c> when the failure is not tied to
    /// a single argument (e.g. unwanted arguments).
    /// </summary>
    public string? ArgumentName { get; }

    /// <summary>
    /// The full text of the message that carried the command.
    /// </summary>
    public string CommandText { get; }
}
