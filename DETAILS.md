# PolyBot technical details

This document contains the deep explanations for PolyBot's features. For a feature overview and quick-start examples, see [README.md](README.md).

## Table of contents

1. [Writing handlers](#writing-handlers)
2. [Parameter injection](#parameter-injection)
3. [Wiring it up](#wiring-it-up)
4. [Running the bot](#running-the-bot)
5. [Hosting and webhooks](#hosting-and-webhooks)
6. [Keyboards](#keyboards)
7. [Finite state machines](#finite-state-machines)
8. [Rate limiting](#rate-limiting)
9. [Exception handling](#exception-handling)
10. [BotFather command sync](#botfather-command-sync)
11. [Update filters](#update-filters)
12. [Update helpers](#update-helpers)
13. [Inline awaits](#inline-awaits)
14. [Callback-data patterns](#callback-data-patterns)
15. [Test harness](#test-harness)
16. [Testing client](#testing-client)
17. [Diagnostics](#diagnostics)
18. [Library vs runnable assemblies](#library-vs-runnable-assemblies)

## Writing handlers

A handler is a static or instance method returning `Task<Result>` / `ValueTask<Result>`. Plain `Task`/`ValueTask` is accepted and always continues routing. Each `UpdateType` gets its own generated `case` in the router.

Rules:

* Handlers may target any update type via the corresponding `[XxxHandler]` attribute from `PolyBot.Attributes`.
* **Priorities**: `Priority` (higher = earlier) orders handlers within one update type. If `Priority` is omitted, handlers keep their source order.
* Routing falls through to the next handler while `Result.ContinueRouting` is true; `Result.StopRouting` ends routing for that update.
* **Filters**: `[XxxFilter]` attributes apply the discovered `XxxFilter : IUpdateFilter` class to the handler; all filters must pass (AND, in attribute order) before the handler runs.
* **Commands**: `[Command(Aliases = [...])]` restricts a handler to messages whose first entity is a bot command matching an alias. The `@botname` suffix is respected and only matches the current bot's username.

## Parameter injection

Handler parameters are resolved in the following order:

| Parameter type | Source |
| --- | --- |
| `ITelegramBotClient` | the `botClient` argument of `HandleUpdateAsync` |
| `CancellationToken` | the `cancellationToken` argument |
| `Update` | the raw `update` argument |
| assignable from the update payload | the extracted payload (`Message`, ...) |
| `[Key("x")] T` (constant string/int key) | `GetRequiredKeyedService<T>("x")` |
| anything else | `GetRequiredService<T>()` |

## Wiring it up

```csharp
var services = new ServiceCollection();
services.AddSingleton(new PolyBotOptions() { BotToken = botToken }); // required configuration
services.AddPolyBotDefaults();  // registers ITelegramBotClient and essential PolyBot services
services.AddPolyBotRouter();    // generated: registers PolyBot.BotRouter as IUpdateHandler
```

`BotRouter` and the `PolyBotExtensions` static class (`AddPolyBotRouter`) are generated into the root `PolyBot` namespace by the source generator referenced from the `PolyBot.SourceGenerators` project. Handlers' containing types and services are resolved from the same `IServiceProvider` on every update; instance handlers are activated per invocation (`GetRequiredService<ContainingType>()`). Do not hand-write a type named `PolyBotExtensions` in the `PolyBot` namespace — it would collide with the generated one.

## Running the bot

```csharp
await using PolyBotClient client = new PolyBotClient();
client.Services.AddPolyBotRouter();  // generated registration

await client.RunPollingAsync();      // blocks, polling until cancelled
```

All configuration lives in `PolyBotOptions` — the single entry point covering the bot token and client (`BotToken`, `BaseUrl`, `UseTestEnvironment`), polling (`PollingLimit`, `PollingOffset`, `AllowedUpdates`, `DropPendingUpdates`), and webhooks (`WebhookUrl`, `WebhookSecretToken`, `WebhookMaxConnections`, `DeleteWebhookOnStop`). Values come from the `"PolyBot"` configuration section (standard `Microsoft.Extensions.Configuration` sources, e.g. `AddJsonFile`), then the constructor overrides per-value; the token is required for polling and not for testing. `RunPollingAsync` synchronizes BotFather commands first when the sync service is registered, then back-fills `PolyBotOptions.BotUsername` from `GetMe` — so `@botsuffix` command verification works without hard-coding the username.

## Hosting and webhooks

The generator inspects the consumer's references and emits integration only for what is referenced — the core package never forces hosting or ASP.NET Core dependencies.

### Generic Host

When `Microsoft.Extensions.Hosting.Abstractions` is referenced, a `PolyBotPollingHostedService` (`IHostedService`) is generated that deletes stale webhooks, then polls with `ReceiveAsync` in the background, honouring a cancellation token on shutdown.

Register it with:

```csharp
services.AddPolyBotHostedPolling(receiver => { receiver.DropPendingUpdates = true; });
```

### ASP.NET Core

When any `Microsoft.AspNetCore.*` package is referenced, `AddPolyBotWebhook` registers a `PolyBotWebhookHostedService` that calls `SetWebhook` on startup (secret token, allowed updates, max connections, drop-pending) and `DeleteWebhook` on shutdown when `PolyBotOptions.DeleteWebhookOnStop` is set, plus `MapPolyBotWebhook` for the endpoint. All settings come from the same `PolyBotOptions`.

The endpoint can be remapped after start:

```csharp
app.MapPolyBotWebhook("/bot");
```

The mapped endpoint verifies the `X-Telegram-Bot-Api-Secret-Token` header (missing → 400, wrong → 401), deserializes the `Update` directly from the request body stream with Telegram.Bot's source-generated serializer (malformed JSON → 400), hands it to the generated router and returns 200 OK. Services are resolved per-request from `HttpContext.RequestServices`.

## Keyboards

Fluent builders (`InlineKeyboardBuilder`, `ReplyKeyboardBuilder`) accumulate buttons linearly and then either `Build()` (one button per row) or `Adjust(rowSizes)` (rows of the given sizes, repeating the last size for the remaining buttons):

```csharp
InlineKeyboardMarkup keyboard = new InlineKeyboardBuilder()
    .WithCallbackButton("name1.1", "d1")
    .WithCallbackButton("name1.2", "d2")
    .WithCallbackButton("name2.1", "d3")
    .Adjust(2, 1); // row of 2, then a row of 1
```

`ReplyKeyboardBuilder.WithButton(text)` mirrors it and sets `ResizeKeyboard`.

Generated keyboards declare the layout with `[CallbackButton(text, callbackData)]` on a `partial` method (or, C# 13, a `partial` property) returning `InlineKeyboardMarkup` — the generator emits the implementation. Each attribute list forms one row, the attributes inside it the columns; `{parameter}` placeholders in the callback data are interpolated in the generated code:

```csharp
[CallbackButton("First", "first:{val}"), CallbackButton("Second", "second:{val}")]
[CallbackButton("Cancel", "cancel"), CallbackButton("Apply", "apply:{val}")]
private static partial InlineKeyboardMarkup ActionsKeyboard(int val);
```

The containing type(s) must be `partial`. `CUR017` warns about placeholders that match no parameter, `CUR018` (error) about a non-`InlineKeyboardMarkup` return type, `CUR019` about non-partial (or generic) members.

## Finite state machines

Handlers can be restricted to (or excluded from) a per-user/per-chat state:

```csharp
public enum RegistrationStep
{
    AwaitingAge,
    Completed
}

[MessageHandler]
[State<RegistrationStep>(RegistrationStep.AwaitingAge)]
public static async Task<Result> AskAge(IStateMachine fsm)
{
    await fsm.SetAsync(RegistrationStep.Completed);
    return Result.Handled();
}

[MessageHandler]
[NoState<RegistrationStep>]
public static async Task<Result> StartRegistration(IStateMachine fsm)
{
    await fsm.SetAsync(RegistrationStep.AwaitingAge);
    return Result.Handled();
}
```

Within one handler, attributes sharing the same `(TEnum, StateKey)` pair are OR-ed; different pairs are AND-ed. Handlers with neither attribute are fully stateless. `[NoState]` only fires when the update's identity was resolved and has no state — updates without a resolvable identity (e.g. plain `Poll` updates) match neither `[State]` nor `[NoState]`.

State keys are resolved from the update being routed (`StateKeyResolver`):

| `StateKey` | Resolved from |
| --- | --- |
| `UserId` | first available of `Message`/`EditedMessage`/`ChannelPost`/`EditedChannelPost` `.From.Id`, `CallbackQuery`/`InlineQuery`/`ChosenInlineResult`/`ShippingQuery`/`PreCheckoutQuery`/`MyChatMember`/`ChatMember`/`ChatJoinRequest` `.From.Id`, `PollAnswer.User.Id`, `MessageReaction.User.Id`, `BusinessConnection.User.Id` |
| `ChatId` | the same message/chat payloads' `.Chat.Id`, plus `CallbackQuery.Message.Chat.Id`, `PollAnswer.VoterChat.Id`, `ChatBoost.Chat.Id`, `BusinessConnection.UserChatId` |
| `UserInChat` | `"{chatId}_{userId}"` (both must resolve) |

Inject `IStateMachine` into handlers to read/transition the current update's state; inject `IStateStorage` for untyped access. For ordered state enums, `AdvanceAsync`/`RollbackAsync` extension methods walk one step forward/back through the enum's values (no state → first, last → state cleared back to no state) and return the resulting state. `AddPolyBot()` registers `MemoryStateStorage`, `StateMachine` and `UpdateContextAccessor` as singletons with try-add semantics — register your own `IStateStorage` **before** `AddPolyBotRouter()` to override the in-memory default (required for multi-instance bots; `MemoryStateStorage` also offers an optional default expiration via `new MemoryStateStorage(TimeSpan)`).

## Rate limiting

Declarative sliding-window rate limiting on any handler via `[Throttled]`:

```csharp
[MessageHandler]
[Command(Aliases = ["search"])]
[Throttled(Limit = 3, PeriodMilliseconds = 60_000, Scope = ThrottleScope.User, Action = ThrottleAction.Fallthrough)]
public static Task<Result> Search(Message msg, [Rest] string query) { ... }
```

The generator inlines the guard as a single static `ThrottleGate.TryEnter` call inside the handler's branch — after the match guards (command/pattern/state) pass, before filters resolve or handler services are injected — so non-matching updates never consume budget and throttled calls short-circuit with no allocations beyond the gate's own bookkeeping. `ThrottleScope` buckets by user, chat, user-in-chat pair or a single global bucket; unidentifiable updates share bucket 0. `ThrottleAction.Ignore` halts the routing branch entirely; `Fallthrough` skips the handler and continues. Gates are static per handler and thread-safe (a single lock over per-bucket ring buffers) across concurrent polling loops and parallel webhook requests. `CUR035` warns about invalid or non-constant configuration, clamped to the defaults.

## Exception handling

Decorate one method with `[ExceptionHandler]` and the generated router invokes it from `HandleErrorAsync` for every update-handling failure, instead of swallowing errors:

```csharp
[ExceptionHandler]
public static Task OnError(Exception exception, HandleErrorSource source, IEchoService log)
{
    log.Record($"{source}: {exception.Message}");
    return Task.CompletedTask;
}
```

Parameters resolve like handler parameters — `Exception`, `HandleErrorSource`, `ITelegramBotClient` and `CancellationToken` come from the error callback, anything else from DI (keyed with `[Key]`) — but there is no current update in the error path, so update-bound parameters are rejected (`CUR032`). The method must return `Task` or `ValueTask` (`CUR034`); declaring more than one `[ExceptionHandler]` is an error (`CUR033`). A throw from the handler itself is swallowed so the polling loop survives.

## BotFather command sync

`[Command]` carries optional BotFather metadata — `Description`, `LanguageCode`, `Scope` (`CommandScope.Default`, `AllPrivateChats`, `AllGroupChats`, `AllChatAdministrators`) and `IsHidden` — and the generator turns it into `PolyBot.PolyBotBotFatherSync`, an `IBotFatherSyncService` holding the discovered commands (`DiscoveredCommands`, hidden ones excluded) and a `SyncCommandsAsync` that sends one `SetMyCommands` call per distinct (scope, language) group. `AddPolyBotRouter()` registers it automatically, and `PolyBotClient.RunPollingAsync` syncs before polling starts.

Only `'/'`-prefixed commands are synchronized; `IsHidden = true` keeps a command routable but out of the menu (and skips the missing-description warning). `CUR020` (error) reports aliases that violate BotFather's `^[a-z0-9_]{1,32}$` format, `CUR021` a missing description, `CUR022` (error) a description over 256 characters, and `CUR023` duplicate names inside the same (scope, language) bucket.

## Update filters

`PolyBot.IUpdateFilter` is a synchronous gate over an update. The source generator emits a set of abstract `XxxFilter` bases (namespace `PolyBot`, one per Telegram.Bot payload DTO) whose `CanPass(Update)` extracts the payload first — first match wins across all `Update` properties of that DTO — and then defers to a payload-specific `protected abstract bool CanPass(TDto payload)`:

```csharp
public sealed class NonEmptyTextFilter : MessageFilter
{
    protected override bool CanPass(Message message) => !string.IsNullOrEmpty(message.Text);
}
```

For DTOs shared by several update types, a narrowing derived filter is generated per additional type (`Extract` limited to that one property). For Telegram.Bot 22.10 the generator produces: `MessageFilter` + `EditedMessageFilter`, `ChannelPostFilter`, `EditedChannelPostFilter`, `BusinessMessageFilter`, `EditedBusinessMessageFilter`, `GuestMessageFilter`; `MyChatMemberFilter` + `ChatMemberFilter`; and the single-type bases `InlineQueryFilter`, `ChosenInlineResultFilter`, `CallbackQueryFilter`, `ShippingQueryFilter`, `PreCheckoutQueryFilter`, `PollFilter`, `PollAnswerFilter`, `ChatJoinRequestFilter`, `MessageReactionFilter`, `MessageReactionCountFilter`, `ChatBoostFilter`, `RemovedChatBoostFilter`, `BusinessConnectionFilter`, `DeletedBusinessMessagesFilter`, `PurchasedPaidMediaFilter`, `ManagedBotFilter`, `SubscriptionFilter`, `StoppedMessageGenerationFilter`. Nothing in the router consumes `IUpdateFilter` yet — it is a building block for user- and library-level gating.

## Update helpers

`PolyBot.UpdateExtensions` provides first-match probes over `Update`'s many optional payloads (probe order follows Telegram.Bot 22.10's `Update` properties; updates without a matching payload yield `null`):

* `GetUserId()` / `GetChatId()` — first user/chat id across the full probe chain (messages, channel posts, callback/inline/shipping/pre-checkout queries, poll answers, chat-member updates, message reactions, chat boosts, business connections).
* `GetSender()` / `GetChat()` — the first `User`/`Chat` payload itself.
* `GetMessage()` — `Message ?? EditedMessage ?? ChannelPost ?? EditedChannelPost`.
* `GetText()` — the message's `Text`, falling back to its `Caption` (media captions are the message text in Telegram).

These are the same chains `StateKeyResolver` uses for FSM keys.

## Inline awaits

Handlers can suspend mid-conversation and resume on a later update, without blocking the polling loop. Inject `IUpdateAwaiter` and await a generated `WaitFor*` entry point:

```csharp
[MessageHandler]
[Command(Aliases = ["register"])]
public static async Task<Result> Register(Message msg, IUpdateAwaiter awaiter, CancellationToken ct)
{
    Message? age = await awaiter.WaitForMessageAsync()
        .TextMatches(@"my age is \d+")
        .WithNonEmptyTextFilter()
        .ByUserId(ct);
    // ...
    return Result.Handled();
}
```

The generated router turns every await-site into implicit branches inside its routing `switch`: for each update type of the awaited DTO's group (e.g. awaiting a message-shaped payload creates branches for `Message`, `EditedMessage`, `ChannelPost`, `EditedChannelPost`, `BusinessMessage`, `EditedBusinessMessage` and `GuestMessage`), a branch at the top of the case statically pre-filters the payload (non-null, the `TextMatches` regexes, one `CanPass` per chain filter — resolved DI-first, and any `Where(static …)` lambdas, spliced verbatim in chain order), then hands the payload to the awaiter engine's delivery hook. Matching allocates nothing: the registry is keyed by a numeric identity (the user/chat id pair, probed most-specific-first) and the registration's declarative signature (payload type, `TextMatches` patterns, filter types, Where flag — held in per-site static constant arrays, compared by reference and ordinal equality) is re-validated by comparison, so a branch of a *different* site on the same identity can never claim the await, and two differently-shaped awaits on the same identity may coexist. `TextMatches`/`With*` are generator markers consumed into the branch condition. `Where(static m => …)` predicates must be `static`, non-async, expression-bodied lambdas whose body references only the parameter (members rooted at it) and literals — the `static` modifier makes captures a compile error, and the conservative reference check keeps the spliced lambda binding in the generated file (no usings there); anything else is rejected with `CUR016` and not enforced anywhere. `CUR015` warns when a chain references an unknown filter. Cases are created even for update types with no user handlers, so a conversation can await a `ChannelPost` without any ChannelPost handler existing. If the handler completes synchronously, routing proceeds normally; if it suspends on a `WaitFor`, the workflow is detached (`RunContinuationsAsynchronously`), the update counts as handled, and the `Result` the workflow eventually returns does **not** affect routing. Non-matching updates fall through to normal routing, so other handlers keep working while a conversation is pending. Awaits are single-shot; a timeout (from `WaitForAsync(timeout)`) completes the await with `null` and removes the registration, and cancellation cancels the task and removes the registration.

The source generator emits (namespace `PolyBot.Attributes`): one `WaitFor{UpdateType}Async` extension per Telegram.Bot update type (payload typing follows the generated filter DTOs — `WaitForMessageAsync` awaits any message-shaped payload), and one `With{FilterName}` extension per discovered concrete filter (DTO-typed when the filter subclasses a generated base, generic otherwise). `CUR013` warns when a handler takes the awaiter without an await site — a suspended `WaitFor` would deadlock the sequential poller, and custom wrapper methods are not followed by design.

## Callback-data patterns

`[CallbackQueryHandler]` methods can declare a route template over `CallbackQuery.Data` with `[Pattern("template", Separator = ':')]` — segment-based matching with the same `[Arg]` ergonomics as command arguments. Literal segments (`cart:checkout`) match ordinally, `{name}` captures one segment into the `[Arg]` parameter of the same name (name inferred from the parameter, as with commands), `{name:int}` adds a type constraint checked against the parameter type, and a trailing `{*path}` wildcard captures the rest of the data (possibly empty) into a `string` parameter. Generated guards inline the typed extraction — `TryParse` with invariant culture for numerics, `Enum.TryParse` for enums, raw strings otherwise — so a failing capture simply falls through to the next handler. Non-matching data never throws.

```csharp
[CallbackQueryHandler]
[Pattern("item:{id}:{action}")]
public static Task<Result> OnItem(
    CallbackQuery query,
    [Arg] int id,
    [Arg] string action)
{
    // You can use extracted 'id' and 'action' here!
}

[CallbackQueryHandler]
[Pattern("logs:{*path}")]
public static Task<Result> OnLogs(
    CallbackQuery query,
    [Arg] string path)
{
    // You can use extracted 'path' here!
}
```

The matcher (`PolyBot.CallbackPatternMatcher`/`CallbackSegments`) is zero-allocation per update: bounds are recomputed by scanning instead of materializing segment arrays. `CUR024`/`CUR025` (errors) reject templates that can never fit the 64-byte `callback_data` limit — the literal skeleton alone, or the skeleton plus the minimum non-empty placeholder sizes. `CUR026`/`CUR027` (errors) cover placeholders without `[Arg]` parameters and `[Arg]` parameters the template does not capture; `CUR028` (error) requires an accessible `TryParse`; `CUR029` (error) applies `[Pattern]` to non-callback handlers; `CUR030` (error) reports constraint/type mismatches; `CUR031` (error) reports malformed templates.

## Test harness

`RunTest()` builds the provider the same way, swaps in an in-memory `PolyBotBotTestClient` (no token, no network) and returns an `UpdateMocker` that pushes synthetic updates straight into the generated router:

```csharp
PolyBotClient client = new PolyBotClient();
client.Services.AddPolyBotRouter();
UpdateMocker mock = client.RunTest();

await mock.Message("hello");         // plain text, auto ids
await mock.Command("start");         // command with entity, leading '/' optional
await mock.Callback("item:1:open");
await mock.InlineQuery("query");
await mock.Update(new Update { });   // anything else; id auto-assigned when 0
```

Every push returns the routed update (with the auto-assigned ids) for deterministic assertions, and `mock.BotClient.SentRequests` captures outgoing API calls the handlers made. `client.BotClient` is available after either mode starts and throws `InvalidOperationException` before that.

## Testing client

`PolyTests` is an in-memory `ITelegramBotClient` double for offline tests: inject updates with `EnqueueUpdate`/`EnqueueUpdates` — they are served to the polling loop with long-poll semantics (batched by `limit`, waiting for injections, honouring cancellation on shutdown) — and assert on `SentRequests`, which captures every outgoing request (`SetWebhookRequest`, `SendMessageRequest`, ...). `GetMe`, `GetUpdates`, `SetWebhook`, `DeleteWebhook`, `SetMyCommands`, `SendMessage` and `AnswerCallbackQuery` are answered with fabricated responses; anything else throws. Register it as the `ITelegramBotClient` and drive any hosting model — polling hosted service, webhook endpoint, or `PolyBotClient.RunPollingAsync` — with no network and no token:

```csharp
PolyBotTests testClient = new PolyBotTests();
services.AddSingleton<ITelegramBotClient>(testClient);
services.AddPolyBotHostedPolling();

// ...start the host, then:
testClient.EnqueueUpdate(update);
```

## Diagnostics

* `CUR001` (warning) — handler signature not supported (wrong return type); method is skipped.
* `CUR002` (warning) — `[Command]` on a handler whose payload is not a `Message`; the command filter is ignored.
* `CUR003` (warning) — `[Arg]`/`[Rest]`/`[Parse]` on a parameter of a method without `[Command]`; the parameter is treated as a normal dependency.
* `CUR004` (warning) — non-constant `[Command]` `Prefix`; falls back to `'/'`.
* `CUR005` (warning) — `[Arg]` parameter type is not the argument type, `Nullable<T>` or `object`; the parameter is treated as a normal dependency.
* `CUR006` (warning) — more than one `[Rest]` parameter on a handler; extras are treated as ordinary dependency parameters.
* `CUR007` (warning) — `[Rest]` on a parameter whose type is not `string`; the parameter is treated as a normal dependency.
* `CUR008` (warning) — `[Parse]` declared after a `[Rest]` parameter; the parameter is treated as a normal dependency.
* `CUR009` (warning) — `[Parse]` on a parameter whose type is not `string`; the parameter is treated as a normal dependency.
* `CUR010` (error) — `[State]`/`[NoState]` type argument is not an enum; the attribute is ignored.
* `CUR011` (warning) — `[State]` and `[NoState]` declared for the same enum type and `StateKey` on one handler; the conditions are AND-ed and the handler will never match.
* `CUR012` (warning) — a concrete `IUpdateFilter` class has generic type parameters; it cannot be auto-wrapped in a filter attribute and is skipped.
* `CUR013` (warning) — a handler takes `IUpdateAwaiter` but no `WaitFor*Async` call on it was found; the handler runs to completion inline, and a suspended `WaitFor` would deadlock the sequential poller (custom wrapper methods are not followed by design — call `WaitFor*` directly on the `IUpdateAwaiter` parameter).
* `CUR014` (warning) — `TextMatches` on an await whose target DTO is not a message shape can never match; no implicit await branch is generated for that site.
* `CUR015` (warning) — an await chain references a filter that could not be resolved; the filter check will not be emitted into the await branch.
* `CUR016` (warning) — a `Where` predicate in an await chain does not meet the static-lambda constraints (static, non-async, expression-bodied, parameter-only references); the predicate will not be emitted into the await branch.

## Library vs runnable assemblies

The generator adapts to the project type (`OutputType`): library projects emit filters, `With*` await extensions and generated keyboards only — the routing infrastructure (`BotRouter`, DI extensions, BotFather sync, hosting/webhook) is generated exclusively for runnable assemblies (Exe/WinExe), so filter libraries can be referenced by other PolyBot apps without any type collisions. Handlers declared in a library are reported with `CUR036` (they would never route). Filter references are resolved semantically across assemblies: a handler in the app can use `[MyFilter]` and `WithMyFilter()` defined in a referenced filter library — the generator pairs the compiled wrapper/extension with its filter type in the library assembly, no assembly-wide scanning.

Filters are plain classes implementing `PolyBot.IUpdateFilter` (`bool CanPass(Update update)`), typically subclassing one of the generated abstract DTO bases (see the [Update filters](#update-filters) section) whose `CanPass(Update)` extracts the payload first:

```csharp
public sealed class NonEmptyTextFilter : MessageFilter
{
    protected override bool CanPass(Message message) => !string.IsNullOrEmpty(message.Text);
}
```

The source generator discovers every concrete (non-abstract), non-generic `IUpdateFilter` implementation reachable from your compilation — your project and referenced assemblies, nested classes included — and emits a wrapper attribute per class into the `PolyBot.Attributes` namespace: `EchoBot.NonEmptyTextFilter` becomes `[NonEmptyTextFilter]` (`AllowMultiple = true`). Generic filter classes are reported as `CUR012` and skipped. Applying one or more filter attributes to a handler gates it: every filter must pass, in attribute order (AND semantics):

```csharp
[InlineQueryHandler]
[AllowAllFilter]
[SomeOtherFilter]
public static Task<Result> OnInline(InlineQuery query) { ... }
```

At runtime the generated router resolves each filter from the service collection first (`GetService<T>()`), falling back to `ActivatorUtilities.CreateInstance` — so filters with constructor dependencies should be registered in DI, while parameterless filters work unregistered. This replaces the former async `FilterAttribute`/`FilterContext` pipeline (both removed).
