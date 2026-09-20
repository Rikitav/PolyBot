# ConversationBot

A PolyBot sample that demonstrates multi-step conversations, callback-data patterns, rate limiting, and global exception handling.

## Run

```bash
dotnet run -- <BOT_TOKEN>
# or
TELEGRAM_BOT_TOKEN=<token> dotnet run
```

## Features shown

- `IUpdateAwaiter.WaitForMessageAsync()` for suspending a handler and resuming on the user's next message.
- Fluent await chain with `.TextMatches(...)` and a custom `MessageFilter` (`WithNonEmptyTextFilter`).
- `[Pattern("confirm:{userId}")]` / `[Pattern("cancel:{userId}")]` with typed `[Arg]` extraction.
- `[Throttled]` sliding-window rate limit on `/register`.
- `[ExceptionHandler]` global error hook.
- Generated inline keyboard with parameter interpolation in callback data.
