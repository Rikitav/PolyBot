using Telegram.Bot.Types.Enums;

namespace PolyBot;

/// <summary>
/// The single configuration entry point for a PolyBot bot. Populated from the
/// <c>"PolyBot"</c> configuration section (see <see cref="PolyBotClient.Configuration"/>)
/// and overridable per-value via the <see cref="PolyBotClient(PolyBotOptions?)"/> constructor.
/// </summary>
public sealed class PolyBotOptions
{
    /// <summary>The bot's username, used for <c>@botsuffix</c> command verification.
    /// Back-filled from <c>GetMe</c> by <see cref="PolyBotClient.RunPollingAsync"/> when polling starts.</summary>
    public string? BotUsername { get; set; }

    /// <summary>
    /// The bot token. Required for <see cref="PolyBotClient.RunPollingAsync"/>; not required in test mode.
    /// </summary>
    public string? BotToken { get; set; }

    /// <summary>
    /// Optional custom Bot API base URL (local Bot API server).
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Use the Bot API test environment.
    /// </summary>
    public bool UseTestEnvironment { get; set; }

    /// <summary>
    /// Maximum number of updates per <c>GetUpdates</c> batch (Telegram allows 1-100).
    /// </summary>
    public int? PollingLimit { get; set; }

    /// <summary>
    /// Initial update offset; leave unset to consume pending updates.
    /// </summary>
    public int? PollingOffset { get; set; }

    /// <summary>
    /// Update types to receive; <c>null</c> (the default) receives all.
    /// </summary>
    public UpdateType[]? AllowedUpdates { get; set; }

    /// <summary>
    /// Drop pending updates when polling starts or the webhook is set.
    /// </summary>
    public bool DropPendingUpdates { get; set; }

    /// <summary>
    /// The full HTTPS URL Telegram sends updates to (the address mapped with <c>MapPolyBotWebhook</c>).
    /// </summary>
    public string? WebhookUrl { get; set; }

    /// <summary>
    /// Secret token verified from the <c>X-Telegram-Bot-Api-Secret-Token</c> header.
    /// </summary>
    public string? WebhookSecretToken { get; set; }

    /// <summary>
    /// Maximum simultaneous HTTPS connections to the webhook, 1-100. Defaults to 40.
    /// </summary>
    public int WebhookMaxConnections { get; set; } = 40;

    /// <summary>
    /// When <c>true</c> (the default), <c>DeleteWebhook</c> is called on graceful shutdown.
    /// </summary>
    public bool DeleteWebhookOnStop { get; set; } = true;
}
