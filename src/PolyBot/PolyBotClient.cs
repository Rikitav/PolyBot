using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PolyBot.BotFather;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PolyBot;

/// <summary>
/// Unified runtime runner binding the service collection, configuration and the Telegram
/// bot client to the generated routing pipeline. Two modes: <see cref="RunPollingAsync"/>
/// (long polling) and <see cref="RunTest"/> (token-free in-memory test harness).
/// </summary>
public sealed class PolyBotClient : IAsyncDisposable
{
    private readonly PolyBotOptions? _curatorOptions;
    private ServiceProvider? _provider;
    private TelegramBotClientOptions? _telegramOptions;
    private ITelegramBotClient? _botClient;

    /// <summary>
    /// Creates the runner with optional direct options (taking precedence over configuration).
    /// </summary>
    public PolyBotClient(PolyBotOptions? curatorOptions = null)
    {
        _curatorOptions = curatorOptions;
        Configuration = new ConfigurationManager();
        Services = new ServiceCollection();
    }

    /// <summary>
    /// The service collection the consumer registers the generated router and services into.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// The application configuration (an empty <see cref="ConfigurationManager"/> by default);
    /// options are read from the <c>"PolyBot"</c> section by fixed names.
    /// </summary>
    public ConfigurationManager Configuration { get; }

    /// <summary>
    /// The built bot client. Only available after <see cref="RunPollingAsync"/> or
    /// <see cref="RunTest"/> started; throws <see cref="InvalidOperationException"/> before that.
    /// </summary>
    public ITelegramBotClient BotClient
    {
        get
        {
            if (_botClient is null)
            {
                throw new InvalidOperationException(
                    "The bot client is built when polling or test mode starts; call RunPollingAsync or RunTest first.");
            }

            return _botClient;
        }
    }

    /// <summary>
    /// Starts the long-polling loop against the Telegram Bot API.
    /// </summary>
    public async Task RunPollingAsync(CancellationToken cancellationToken = default)
    {
        BuildProvider(requireBotToken: true, forceTestClient: false);
        IServiceProvider provider = _provider!;
        TelegramBotClientOptions botOptions = _telegramOptions!;

        IBotFatherSync? botFatherSync = provider.GetService<IBotFatherSync>();
        if (botFatherSync is not null)
        {
            await botFatherSync.SyncCommandsAsync(cancellationToken).ConfigureAwait(false);
        }

        PolyBotOptions options = provider.GetRequiredService<PolyBotOptions>();
        User me = await TelegramBotClientExtensions.GetMe(_botClient!, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(me.Username))
        {
            options.BotUsername = me.Username;
        }

        ReceiverOptions receiverOptions = new ReceiverOptions
        {
            Limit = options.PollingLimit,
            Offset = options.PollingOffset,
            AllowedUpdates = options.AllowedUpdates ?? provider.GetService<IAllowedUpdatesProvider>()?.AllowedUpdates,
            DropPendingUpdates = options.DropPendingUpdates,
        };

        IUpdateHandler handler = provider.GetRequiredService<IUpdateHandler>();
        HttpClient httpClient = provider.GetRequiredService<HttpClient>();

        await ReceiveUpdatesOptimizedAsync(httpClient, botOptions.BaseRequestUrl, _botClient!, handler, receiverOptions, cancellationToken).ConfigureAwait(false);
        //await TelegramBotClientExtensions.ReceiveAsync(_botClient!, handler, receiverOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the provider and initializes the test harness, returning the update mocker.
    /// </summary>
    public UpdateMocker RunTest(CancellationToken cancellationToken = default)
    {
        BuildProvider(requireBotToken: false, forceTestClient: true);
        IServiceProvider provider = _provider!;

        PolyBotOptions options = provider.GetRequiredService<PolyBotOptions>();
        options.AllowedUpdates ??= provider.GetService<IAllowedUpdatesProvider>()?.AllowedUpdates;

        IUpdateHandler handler = provider.GetRequiredService<IUpdateHandler>();
        return new UpdateMocker(provider, handler, (PolyTests)_botClient!);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync().ConfigureAwait(false);
            _provider = null;
        }
    }

    private static async Task ReceiveUpdatesOptimizedAsync(
        HttpClient httpClient,
        string baseRequestUrl,
        ITelegramBotClient botClient,
        IUpdateHandler updateHandler,
        ReceiverOptions receiverOptions,
        CancellationToken cancellationToken = default)
    {
        int limit = receiverOptions.Limit ?? 100;
        int offset = receiverOptions.Offset ?? 0;
        int timeoutSeconds = (int)botClient.Timeout.TotalSeconds;
        string endpointPrefix = $"{baseRequestUrl}/getUpdates";

        if (receiverOptions.DropPendingUpdates)
        {
            offset = await DropPendingUpdatesAsync(httpClient, baseRequestUrl, cancellationToken).ConfigureAwait(false);
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            // Build the URL using stack-allocated buffer or direct query parameters
            string requestUri = $"{endpointPrefix}?offset={offset}&limit={limit}&timeout={timeoutSeconds}";
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            try
            {
                // ResponseHeadersRead avoids buffering the entire HTTP body into memory
                using HttpResponseMessage response = await httpClient
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    // Fallback error parsing if rate limited or invalid
                    await HandleNonSuccessResponse(response, updateHandler, botClient, cancellationToken)
                        .ConfigureAwait(false);

                    continue;
                }

                // Stream and process updates one by one without allocating an Update[] array
                using Stream stream = await response.Content
                    .ReadAsStreamAsync()
                    .ConfigureAwait(false);

                await foreach (Update? update in DeserializeUpdatesStreamAsync(stream, cancellationToken).ConfigureAwait(false))
                {
                    if (update is null)
                        continue;

                    try
                    {
                        offset = update.Id + 1;
                        await updateHandler
                            .HandleUpdateAsync(botClient, update, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        await updateHandler
                            .HandleErrorAsync(botClient, new UpdateHandlingException(ex.Message, update, ex), HandleErrorSource.HandleUpdateError, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                await updateHandler
                    .HandleErrorAsync(botClient, exception, HandleErrorSource.PollingError, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static async IAsyncEnumerable<Update> DeserializeUpdatesStreamAsync(Stream utf8JsonStream, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Telegram API envelopes response in: {"ok":true,"result":[ ... ]}
        // Utf8JsonStreamReader traverses into "result" without creating JsonDocument DOM objects
        using var jsonDoc = await JsonDocument
            .ParseAsync(utf8JsonStream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        JsonElement root = jsonDoc.RootElement;
        if (root.TryGetProperty("result", out JsonElement resultElement) && resultElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in resultElement.EnumerateArray())
            {
                // Deserialize each individual element directly using Telegram.Bot serializer options
                Update? update = item.Deserialize<Update>(JsonBotAPI.Options);
                if (update is not null)
                    yield return update;
            }
        }
    }

    private static async Task<int> DropPendingUpdatesAsync(HttpClient httpClient, string baseRequestUrl, CancellationToken cancellationToken)
    {
        try
        {
            string url = $"{baseRequestUrl}/getUpdates?offset=-1&limit=1&timeout=0";
            using HttpResponseMessage response = await httpClient
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            using Stream stream = await response.Content
                .ReadAsStreamAsync()
                .ConfigureAwait(false);

            using JsonDocument doc = await JsonDocument
                .ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (doc.RootElement.TryGetProperty("result", out JsonElement result) && result.GetArrayLength() > 0)
            {
                int lastUpdateIndex = result.GetArrayLength() - 1;
                JsonElement lastUpdate = result[lastUpdateIndex];
                JsonElement lastUpdateId = lastUpdate.GetProperty("update_id");
                return lastUpdateId.GetInt32() + 1;
            }
        }
        catch (OperationCanceledException)
        {
            // ignore
        }

        return 0;
    }

    private static async Task HandleNonSuccessResponse(HttpResponseMessage response, IUpdateHandler handler, ITelegramBotClient client, CancellationToken ct)
    {
        if ((int)response.StatusCode == 429)
        {
            // Delay on 429 backoff
            await Task.Delay(1000, ct).ConfigureAwait(false);
            return;
        }

        HttpRequestException ex = new HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase}).");
        await handler
            .HandleErrorAsync(client, ex, HandleErrorSource.PollingError, ct)
            .ConfigureAwait(false);
    }

    private void BuildProvider(bool requireBotToken, bool forceTestClient)
    {
        if (_provider is not null)
        {
            return;
        }

        PolyBotOptions options = new PolyBotOptions();
        BindConfiguration(options);
        OverrideFromConstructor(options);

        Services.TryAddSingleton(options);
        Services.AddPolyBotDefaults();

        if (forceTestClient || options.BotToken is null)
        {
            if (requireBotToken)
            {
                throw new InvalidOperationException(
                    "PolyBotOptions.BotToken was not provided: set it via the PolyBotClient constructor, " +
                    "the \"PolyBot:BotToken\" configuration key (e.g. appsettings.json via Configuration.AddJsonFile), " +
                    "or use RunTest which needs no token.");
            }

            Services.AddSingleton<ITelegramBotClient, PolyTests>();
        }
        else
        {
            _telegramOptions = new TelegramBotClientOptions(options.BotToken, options.BaseUrl, options.UseTestEnvironment);
            Services
                .AddHttpClient("tgbot-client")
                .AddTypedClient<ITelegramBotClient>((HttpClient httpClient) => new TelegramBotClient(_telegramOptions, httpClient))
                .RemoveAllLoggers();
        }

        _provider = Services.BuildServiceProvider();
        _botClient = _provider.GetRequiredService<ITelegramBotClient>();
    }

    private void BindConfiguration(PolyBotOptions options)
    {
        IConfigurationSection section = Configuration.GetSection("PolyBot");
        options.BotUsername = section[nameof(PolyBotOptions.BotUsername)] ?? options.BotUsername;
        options.BotToken = section[nameof(PolyBotOptions.BotToken)] ?? options.BotToken;
        options.BaseUrl = section[nameof(PolyBotOptions.BaseUrl)] ?? options.BaseUrl;
        options.WebhookUrl = section[nameof(PolyBotOptions.WebhookUrl)] ?? options.WebhookUrl;
        options.WebhookSecretToken = section[nameof(PolyBotOptions.WebhookSecretToken)] ?? options.WebhookSecretToken;
        options.UseTestEnvironment = ReadBool(section, nameof(PolyBotOptions.UseTestEnvironment), options.UseTestEnvironment);
        options.DropPendingUpdates = ReadBool(section, nameof(PolyBotOptions.DropPendingUpdates), options.DropPendingUpdates);
        options.DeleteWebhookOnStop = ReadBool(section, nameof(PolyBotOptions.DeleteWebhookOnStop), options.DeleteWebhookOnStop);
        options.PollingLimit = ReadInt(section, nameof(PolyBotOptions.PollingLimit), options.PollingLimit);
        options.PollingOffset = ReadInt(section, nameof(PolyBotOptions.PollingOffset), options.PollingOffset);
        options.WebhookMaxConnections = ReadInt(section, nameof(PolyBotOptions.WebhookMaxConnections), options.WebhookMaxConnections) ?? 40;
        options.AllowedUpdates = ReadUpdateTypes(section[nameof(PolyBotOptions.AllowedUpdates)]) ?? options.AllowedUpdates;
    }

    private void OverrideFromConstructor(PolyBotOptions options)
    {
        if (_curatorOptions is null)
        {
            return;
        }

        options.BotUsername = _curatorOptions.BotUsername ?? options.BotUsername;
        options.BotToken = _curatorOptions.BotToken ?? options.BotToken;
        options.BaseUrl = _curatorOptions.BaseUrl ?? options.BaseUrl;
        options.WebhookUrl = _curatorOptions.WebhookUrl ?? options.WebhookUrl;
        options.WebhookSecretToken = _curatorOptions.WebhookSecretToken ?? options.WebhookSecretToken;
        options.AllowedUpdates = _curatorOptions.AllowedUpdates ?? options.AllowedUpdates;
        options.UseTestEnvironment |= _curatorOptions.UseTestEnvironment;
        options.DropPendingUpdates |= _curatorOptions.DropPendingUpdates;
        options.DeleteWebhookOnStop = _curatorOptions.DeleteWebhookOnStop;
        options.PollingLimit = _curatorOptions.PollingLimit ?? options.PollingLimit;
        options.PollingOffset = _curatorOptions.PollingOffset ?? options.PollingOffset;
        options.WebhookMaxConnections = _curatorOptions.WebhookMaxConnections;
    }

    private static bool ReadBool(IConfiguration configuration, string key, bool defaultValue)
    {
        string? value = configuration[key];
        return value is not null && bool.TryParse(value, out bool parsed) ? parsed : defaultValue;
    }

    private static int? ReadInt(IConfiguration configuration, string key, int? defaultValue)
    {
        string? value = configuration[key];
        return value is not null && int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : defaultValue;
    }

    private static UpdateType[]? ReadUpdateTypes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string[] names = value!.Split(',');
        List<UpdateType> result = new List<UpdateType>(names.Length);
        foreach (string name in names)
        {
            string trimmed = name.Trim();
            if (Enum.TryParse(trimmed, ignoreCase: true, out UpdateType updateType))
            {
                result.Add(updateType);
            }
        }

        return result.ToArray();
    }
}
