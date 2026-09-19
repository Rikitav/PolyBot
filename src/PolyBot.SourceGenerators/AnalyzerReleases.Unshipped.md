; Unshipped analyzer changes
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
CUR003 | PolyBot | Warning | [Arg], [Rest] or [Parse] on a parameter of a method without [Command] is ignored; the parameter is treated as a normal dependency
CUR004 | PolyBot | Warning | Non-constant [Command] Prefix named argument falls back to '/'
CUR005 | PolyBot | Warning | [Arg] parameter type is not the argument type, Nullable<T> or object; the parameter is treated as a normal dependency
CUR006 | PolyBot | Warning | A handler with more than one [Rest] parameter binds only the first; extra [Rest] parameters are treated as ordinary dependency parameters
CUR007 | PolyBot | Warning | [Rest] on a parameter whose type is not string is ignored; the parameter is treated as a normal dependency
CUR008 | PolyBot | Warning | [Parse] declared after a [Rest] parameter is ignored; the parameter is treated as a normal dependency
CUR009 | PolyBot | Warning | [Parse] on a parameter whose type is not string is ignored; the parameter is treated as a normal dependency
CUR010 | PolyBot | Error | The type argument of [State]/[NoState] is not an enum; the attribute is ignored
CUR011 | PolyBot | Warning | [State] and [NoState] declared for the same enum type and StateKey on one handler are AND-ed and never match
CUR012 | PolyBot | Warning | A concrete IUpdateFilter class with generic type parameters cannot be auto-wrapped and is skipped
CUR013 | PolyBot | Warning | A handler taking IUpdateAwaiter without a recognized WaitFor*Async await site runs inline; a suspended WaitFor would deadlock the sequential poller
CUR014 | PolyBot | Warning | TextMatches can never match a non-Message await target; no implicit await branch is generated for it
CUR015 | PolyBot | Warning | Unknown filter in an await chain; the filter check will not be emitted into the await branch
CUR016 | PolyBot | Warning | A Where predicate in an await chain does not meet the static-lambda constraints; the predicate will not be emitted into the await branch
CUR017 | PolyBot | Warning | A callback data placeholder of a generated keyboard does not match any parameter
CUR018 | PolyBot | Error | A member annotated with [CallbackButton] does not return Telegram.Bot InlineKeyboardMarkup
CUR019 | PolyBot | Warning | A member annotated with [CallbackButton] is not partial (or its method is generic); no implementation can be generated
CUR020 | PolyBot | Error | A syncable command alias does not match BotFather's command format
CUR021 | PolyBot | Warning | A syncable command has no Description for BotFather synchronization
CUR022 | PolyBot | Error | A command Description exceeds BotFather's 256 character limit
CUR023 | PolyBot | Warning | Two commands collide in the same BotFather scope-and-language bucket; only the first is synchronized
CUR024 | PolyBot | Error | A callback pattern's literal skeleton exceeds the 64-byte callback_data limit and can never match
CUR025 | PolyBot | Error | A callback pattern including its minimum placeholder sizes exceeds the 64-byte callback_data limit
CUR026 | PolyBot | Error | A callback pattern placeholder has no matching [Arg] parameter
CUR027 | PolyBot | Error | An [Arg] parameter is not captured by the handler's callback pattern
CUR028 | PolyBot | Error | An [Arg] type in a callback pattern has no accessible static TryParse
CUR029 | PolyBot | Error | [Pattern] declared on a handler that is not a CallbackQuery handler
CUR030 | PolyBot | Error | A typed callback pattern constraint does not match the [Arg] parameter type or is unknown
CUR031 | PolyBot | Error | A callback pattern template is malformed and is ignored
CUR032 | PolyBot | Error | An [ExceptionHandler] parameter is update-bound or keyed without a constant key
CUR033 | PolyBot | Error | More than one method carries [ExceptionHandler]; a bot has a single exception handler
CUR034 | PolyBot | Error | An [ExceptionHandler] method does not return Task or ValueTask
CUR035 | PolyBot | Warning | A [Throttled] Limit/PeriodMilliseconds/Scope/Action is invalid or non-constant and is clamped to the default
CUR036 | PolyBot | Warning | A handler is declared in a library project; only runnable assemblies route handlers
