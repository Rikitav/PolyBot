# PolyBot - The most magicky Telegram.Bot framework

PolyBot is a generative, DI-friendly routing framework for [Telegram.Bot](https://github.com/TelegramBots/Telegram.Bot). Instead of hand-written `switch` statements over `Update.Type`, you write minimal-API-style handler methods decorated with attributes; a Roslyn source generator compiles them into a hard-coded, optimized `BotRouter` implementing `Telegram.Bot.Polling.IUpdateHandler`.

---

## Learn and Docs

Learn PolybBot on [Official documentation site](https://poly-bot.mintlify.site).

---

## Why PolyBot?

* PolyBot has no reflection after generation and no overhead infrastructure.
* `switch` over `UpdateType` with inlined handling guards.
* Builti-in throttle gates are `static readonly` with lock-protected ring buffers.
* Await branches compare against static constant arrays; matching allocates nothing.
* Filter resolution inside the handler branch, not for non-matching handlers.
* The routing code is as close to hand-optimized C# as a generator can produce.

---

## Quick start

```csharp
[MessageHandler, Command(Aliases = ["start"])]
public static async ValueTask<Result> StartHandler(Message msg, ITelegramBotClient client, CancellationToken ct)
{
    client.SendMessage(msg.Chat, "Hello, " + msg.From!.UserName + "!", cancellationToken: ct);
    return Result.StopRouting;
}

public static async void Main()
{
    await using PolyBotClient client = new PolyBotClient();
    client.Services.AddPolyBotRouter();
    await client.RunPollingAsync();
}
```

---

## Features

### Attribute routing

One handler attribute per `UpdateType`: `[MessageHandler]`, `[CallbackQueryHandler]`, `[InlineQueryHandler]`, etc. Handlers return `Task<Result>` / `ValueTask<Result>`; `Result.ContinueRouting` falls through, `Result.StopRouting` ends routing. `Priority` or source order arranges handlers within an update type.

### Dependency injection

Parameters resolve automatically: `ITelegramBotClient`, `CancellationToken`, `Update`, the update payload (`Message`, `CallbackQuery`, ...), `[Key("x")] T` keyed services, and anything else via `GetRequiredService<T>()`.

### Commands

`[Command(Aliases = ["start"])]` matches messages whose first entity is a `/` command. The `@botname` suffix is verified against the current bot's username. Optional metadata (`Description`, `LanguageCode`, `Scope`, `IsHidden`) is synchronized with BotFather automatically.

### Filters

`[XxxFilter]` attributes gate handlers with custom `IUpdateFilter` implementations. All filters must pass in attribute order. Generated bases such as `MessageFilter` let filters work directly with the extracted payload.

```csharp
public class PremiumOnlyFilter : MessageFilter
{
    protected override bool CanPass(Message message)
        => message.From?.IsPremium is true;
}
```

### Rich messages

```csharp
InputRichMessage rich = new RichMessageBuilder()
    .Paragraph(b => b.Plain("Hello, ").Bold("World!"))
    .Build();
```

### Keyboards

```csharp
InlineKeyboardMarkup keyboard = new InlineKeyboardBuilder()
    .WithCallbackButton("Open", "open")
    .WithCallbackButton("Close", "close")
    .Adjust(2);
```

Or generate keyboards from `[CallbackButton]` attributes on a `partial` method returning `InlineKeyboardMarkup`.

```csharp
[CallbackButton("First", "first:{val}"), CallbackButton("Second", "second:{val}")]
[CallbackButton("Cancel", "cancel"), CallbackButton("Apply", "apply:{val}")]
private static partial InlineKeyboardMarkup ActionsKeyboard(int val);
```

### Finite state machines

```csharp
[MessageHandler, State<RegistrationStep>(RegistrationStep.AwaitingAge)]
public static async Task<Result> AskAge(IStateMachine fsm)
{
    await fsm.SetAsync(RegistrationStep.Completed);
    return Result.Handled();
}
```

Inject `IStateMachine` to transition state; register your own `IStateStorage` before `AddPolyBotRouter()` for custom persistence.

### Inline awaits

Suspend a handler and resume on a later update without blocking the polling loop:

```csharp
[MessageHandler, Command(Aliases = ["register"])]
public static async Task<Result> Register(IUpdateAwaiter awaiter, CancellationToken ct)
{
    Message? age = await awaiter.WaitForMessageAsync()
        .TextMatches(@"my age is \d+")
        .ByUserId(ct);
        
    return age is null ? Result.Handled() : Result.ContinueRouting;
}
```

### Rate limiting

```csharp
[MessageHandler, Command(Aliases = ["search"])]
[Throttled(Limit = 3, PeriodMilliseconds = 60_000, Scope = ThrottleScope.User)]
public static Task<Result> Search(Message msg, [Rest] string query) { ... }
```

### Callback-data patterns

```csharp
[CallbackQueryHandler, Pattern("item:{id}:{action}")]
public static Task<Result> OnItem(CallbackQuery query, [Arg] int id, [Arg] string action)
{
    return Result.Handled();
}
```

### Exception handling

```csharp
[ExceptionHandler]
public static Task OnError(Exception exception, HandleErrorSource source, ILogger log)
{
    log.LogError("{Source}: {Message}", source, exception.Message);
    return Task.CompletedTask;
}
```

### Hosting and webhooks

Integration is emitted only when the matching package is referenced:

- **Generic host** — `services.AddPolyBotHostedPolling(...)`.
- **ASP.NET Core** — `services.AddPolyBotWebhook()` and `app.MapPolyBotWebhook("/bot")`.

### Testing

Test without network or tokens:

```csharp
PolyBotClient client = new PolyBotClient();
client.Services.AddPolyBotRouter();
UpdateMocker mock = client.RunTest();

await mock.Message("hello");
await mock.Callback("item:1:open");
```

`PolyTests` is also available as an in-memory `ITelegramBotClient` for driving the host or polling service manually.
