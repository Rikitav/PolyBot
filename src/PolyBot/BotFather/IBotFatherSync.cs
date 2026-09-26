using Telegram.Bot.Types;

namespace PolyBot.BotFather;

/// <summary>
/// Pushes the commands discovered from <c>[Command]</c>-decorated handlers to BotFather via
/// <c>SetMyCommands</c>. Implemented by the generated <c>PolyBot.PolyBotBotFatherSync</c>;
/// register it with the generated <c>PolyBotExtensions.AddPolyBotBotFatherSync</c>.
/// </summary>
public interface IBotFatherSync
{
    /// <summary>
    /// The discovered commands, in declaration order (hidden commands and commands without a
    /// <c>Description</c> excluded — BotFather rejects empty descriptions, so only described
    /// commands are synchronized).
    /// </summary>
    IReadOnlyList<BotCommand> DiscoveredCommands { get; }

    /// <summary>
    /// Sends one <c>SetMyCommands</c> call per distinct (scope, language) group.
    /// </summary>
    Task SyncCommandsAsync(CancellationToken ct = default);
}
