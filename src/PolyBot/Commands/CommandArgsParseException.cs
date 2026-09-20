using Telegram.Bot.Types;

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
    /// Creates the exception with an error message and the original
    /// <see cref="Telegram.Bot.Types.Message"/> DTO.
    /// </summary>
    public CommandArgsParseException(string message, Message sourceMessage)
        : base(message)
    {
        SourceMessage = sourceMessage;
        CommandText = sourceMessage.Text ?? string.Empty;
    }

    /// <summary>
    /// Creates the exception with an error message, argument name, and the original
    /// <see cref="Telegram.Bot.Types.Message"/> DTO.
    /// </summary>
    public CommandArgsParseException(string message, string? argumentName, Message sourceMessage)
        : base(message)
    {
        ArgumentName = argumentName;
        SourceMessage = sourceMessage;
        CommandText = sourceMessage.Text ?? string.Empty;
    }

    /// <summary>
    /// Creates the exception with an error message, argument name, the original
    /// <see cref="Telegram.Bot.Types.Message"/> DTO, and an underlying parse error.
    /// </summary>
    public CommandArgsParseException(string message, string? argumentName, Message sourceMessage, Exception? innerException)
        : base(message, innerException)
    {
        ArgumentName = argumentName;
        SourceMessage = sourceMessage;
        CommandText = sourceMessage.Text ?? string.Empty;
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

    /// <summary>
    /// The original <see cref="Telegram.Bot.Types.Message"/> DTO that carried the command.
    /// </summary>
    public Message? SourceMessage { get; }
}
