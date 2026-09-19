using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PolyBot;

/// <summary>
/// In-memory <see cref="ITelegramBotClient"/> test double: inject updates with
/// <see cref="EnqueueUpdate"/>, assert outgoing requests via <see cref="SentRequests"/>.
/// Only <c>GetMe</c>, <c>GetUpdates</c>, <c>SetWebhook</c>, <c>DeleteWebhook</c>,
/// <c>SetMyCommands</c>, <c>SendMessage</c> and <c>AnswerCallbackQuery</c> are answered;
/// anything else throws <see cref="NotSupportedException"/>.
/// </summary>
/// <remarks>Fully offline: no network, no token. Not thread-safe beyond what the framework needs.</remarks>
public sealed class PolyTests : ITelegramBotClient
{
    private readonly object _gate = new();
    private readonly Queue<Update> _updates = new();
    private readonly List<object> _sentRequests = new();
    private TaskCompletionSource<bool> _signal = CreateSignal();
    private int _nextMessageId;

    /// <summary>
    /// The user returned by <c>GetMe</c>.
    /// </summary>
    public User BotUser { get; set; } = new User
    {
        Id = 1,
        IsBot = true,
        FirstName = "PolyBotTestBot",
        Username = "poly_test_bot",
    };

    /// <summary>
    /// Every request the framework sent, in order.
    /// </summary>
    public IReadOnlyList<object> SentRequests
    {
        get
        {
            lock (_gate)
            {
                return _sentRequests.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public long BotId => BotUser.Id;

    /// <inheritdoc />
    public bool LocalBotServer => false;

    /// <inheritdoc />
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

    /// <inheritdoc />
    public IExceptionParser ExceptionsParser { get; set; } = new DefaultExceptionParser();

    /// <inheritdoc />
    public event AsyncEventHandler<ApiRequestEventArgs>? OnMakingApiRequest
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public event AsyncEventHandler<ApiResponseEventArgs>? OnApiResponseReceived
    {
        add { }
        remove { }
    }

    /// <summary>
    /// Queues an update for the next <c>GetUpdates</c> poll.
    /// </summary>
    public void EnqueueUpdate(Update update)
    {
        lock (_gate)
        {
            _updates.Enqueue(update);
            TaskCompletionSource<bool> signal = _signal;
            _signal = CreateSignal();
            signal.TrySetResult(true);
        }
    }

    /// <summary>
    /// Queues several updates at once, in order.
    /// </summary>
    public void EnqueueUpdates(IEnumerable<Update> updates)
    {
        foreach (Update update in updates)
        {
            EnqueueUpdate(update);
        }
    }

    /// <inheritdoc />
    public Task<TResponse> SendRequest<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _sentRequests.Add(request);
        }

        switch (request)
        {
            case GetUpdatesRequest getUpdates:
                {
                    return AwaitAndCast<TResponse>(DequeueUpdatesAsync(getUpdates, cancellationToken));
                }

            case GetMeRequest:
                {
                    return Task.FromResult((TResponse)(object)BotUser);
                }

            case SetWebhookRequest:
            case DeleteWebhookRequest:
            case SetMyCommandsRequest:
            case AnswerCallbackQueryRequest:
                {
                    return Task.FromResult((TResponse)(object)true);
                }

            case SendMessageRequest sendMessage:
                {
                    return Task.FromResult((TResponse)(object)BuildMessage(sendMessage));
                }

            default:
                {
                    throw new NotSupportedException(
                        $"PolyBotBotTestClient does not support '{request.GetType().Name}'. Handle it in SendRequest or avoid this API in the test path.");
                }
        }
    }

    /// <inheritdoc />
    public Task<bool> TestApi(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task DownloadFile(string filePath, Stream destination, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("PolyBotBotTestClient does not support file downloads.");
    }

    /// <inheritdoc />
    public Task DownloadFile(TGFile file, Stream destination, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("PolyBotBotTestClient does not support file downloads.");
    }

    private async Task<Update[]> DequeueUpdatesAsync(GetUpdatesRequest request, CancellationToken cancellationToken)
    {
        while (true)
        {
            TaskCompletionSource<bool> signal;
            lock (_gate)
            {
                if (_updates.Count > 0)
                {
                    int limit = request.Limit.GetValueOrDefault();
                    if (limit <= 0)
                    {
                        limit = 100;
                    }

                    int count = Math.Min(_updates.Count, limit);
                    Update[] batch = new Update[count];
                    for (int i = 0; i < count; i++)
                    {
                        batch[i] = _updates.Dequeue();
                    }

                    return batch;
                }

                signal = _signal;
            }

            Task completed = await Task.WhenAny(signal.Task, Task.Delay(global::System.Threading.Timeout.Infinite, cancellationToken)).ConfigureAwait(false);
            if (!ReferenceEquals(completed, signal.Task))
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new OperationCanceledException(cancellationToken);
            }
        }
    }

    private static async Task<TResponse> AwaitAndCast<TResponse>(Task<Update[]> task)
    {
        Update[] batch = await task.ConfigureAwait(false);
        return (TResponse)(object)batch;
    }

    private Message BuildMessage(SendMessageRequest request)
    {
        return new Message
        {
            Id = ++_nextMessageId,
            Text = request.Text,
            From = BotUser,
            Date = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Chat = new Chat
            {
                Id = request.ChatId.Identifier.GetValueOrDefault(),
                Type = ChatType.Private,
            },
        };
    }

    private static TaskCompletionSource<bool> CreateSignal()
    {
        return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
