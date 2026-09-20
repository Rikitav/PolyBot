# CommandBot

A PolyBot sample that demonstrates command routing, argument parsing, custom prefixes, and generated inline keyboards.

## Run

```bash
dotnet run -- <BOT_TOKEN>
# or
TELEGRAM_BOT_TOKEN=<token> dotnet run
```

## Features shown

- `[Command]` with aliases and BotFather descriptions.
- `[Arg]` for typed command arguments (`/add 5 3`).
- Optional `[Arg]` (`/greet` vs `/greet Bob`).
- `[Rest]` for multi-word tails (`/say 2 hello world`).
- Custom prefix (`!help`).
- Generated `[CallbackButton]` keyboard with `partial` method.
- `[Pattern]`-based callback query routing (`help`, `about`).
- Catch-all `MessageHandler` fallback with low priority.
