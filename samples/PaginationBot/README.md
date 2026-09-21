# PaginationBot

A PolyBot sample that demonstrates paginated inline keyboards combined with inline awaits.

## Run

```bash
dotnet run -- <BOT_TOKEN>
# or
TELEGRAM_BOT_TOKEN=<token> dotnet run
```

## Features shown

- `[Command]`-driven entry point (`/items`).
- Fluent `InlineKeyboardBuilder` with dynamic page buttons and navigation controls.
- `IUpdateAwaiter.WaitForCallbackQueryAsync()` to suspend the handler between page clicks.
- Await keyed by user id so each user gets their own paginator state.
- `EditMessageText` to update the existing message on each navigation click.
- Timeout handling with a bounded await.
