using Telegram.Bot.Types;

namespace PolyBot;

/// <summary>
/// Represents an exception that occured while handling an <see cref="Update"/>
/// </summary>
public class UpdateHandlingException : Exception
{
    /// <summary>
    /// The <see cref="Telegram.Bot.Types.Update"/> that was being handled when the exception occured
    /// </summary>
    public Update Update { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateHandlingException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="update">The <see cref="Update"/> that was being handled when the exception occured.</param>
    public UpdateHandlingException(string message, Update update)
        : base("Error occured during update handling : (" + message + ")") => Update = update;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateHandlingException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="update">The <see cref="Update"/> that was being handled when the exception occured.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public UpdateHandlingException(string message, Update update, Exception innerException)
        : base("Error occured during update handling : (" + message + ")", innerException) => Update = update;
}
