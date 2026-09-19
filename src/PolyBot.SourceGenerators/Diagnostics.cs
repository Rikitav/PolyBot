using Microsoft.CodeAnalysis;

namespace PolyBot.SourceGenerators;

internal static class PolyBotDiagnostics
{
    private const string Category = "PolyBot";

    public static readonly DiagnosticDescriptor UnsupportedHandlerSignature = new(
        id: "CUR001",
        title: "Unsupported handler signature",
        messageFormat: "Handler '{0}' must return Task<PolyBot.Result> or ValueTask<PolyBot.Result> (plain Task/ValueTask is also accepted); the method was skipped",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CommandOnNonMessagePayload = new(
        id: "CUR002",
        title: "Command filter on non-message payload",
        messageFormat: "[Command] on handler '{0}' is ignored because the update payload is not a Message",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ArgOnNonCommandHandler = new(
        id: "CUR003",
        title: "Arg, Rest or Parse on non-command handler",
        messageFormat: "[Arg], [Rest] or [Parse] on a parameter of handler '{0}' is ignored because the method has no [Command] attribute; the parameter is treated as a normal dependency",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NonConstantCommandPrefix = new(
        id: "CUR004",
        title: "Non-constant command prefix",
        messageFormat: "The Prefix named argument on [Command] for handler '{0}' is not a constant character; falling back to '/'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor IncompatibleArgParameterType = new(
        id: "CUR005",
        title: "Incompatible argument parameter type",
        messageFormat: "Parameter '{0}' of handler '{1}' has [Arg] but its type is not the argument type, Nullable<T> or object; the parameter is treated as a normal dependency",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MultipleRestParameters = new(
        id: "CUR006",
        title: "Multiple Rest parameters",
        messageFormat: "Handler '{0}' has more than one [Rest] parameter; extra [Rest] parameters are treated as ordinary dependency parameters, so resolving a string from the service provider will fail at runtime",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor RestParameterNotString = new(
        id: "CUR007",
        title: "Rest parameter is not a string",
        messageFormat: "[Rest] on parameter '{0}' of handler '{1}' is ignored because the parameter type is not string; the parameter is treated as a normal dependency",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ParseAfterRest = new(
        id: "CUR008",
        title: "Parse after Rest",
        messageFormat: "[Parse] on parameter '{0}' of handler '{1}' is ignored because it is declared after a [Rest] parameter; the parameter is treated as a normal dependency, so resolving a string from the service provider will fail at runtime",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ParseParameterNotString = new(
        id: "CUR009",
        title: "Parse parameter is not a string",
        messageFormat: "[Parse] on parameter '{0}' of handler '{1}' is ignored because the parameter type is not string; the parameter is treated as a normal dependency",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor StateTypeArgumentMustBeEnum = new(
        id: "CUR010",
        title: "State type argument must be an enum",
        messageFormat: "The type argument of [State]/[NoState] on handler '{0}' must be an enum; the attribute is ignored",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MutuallyExclusiveStateConditions = new(
        id: "CUR011",
        title: "Mutually exclusive state conditions",
        messageFormat: "Handler '{0}' declares both [State] and [NoState] for the same enum type and StateKey; the conditions are AND-ed and the handler will never match",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor GenericFilterClass = new(
        id: "CUR012",
        title: "Generic filter class",
        messageFormat: "Filter class '{0}' has generic type parameters; it cannot be auto-wrapped in a filter attribute and is skipped",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AwaiterWithoutAwaitSite = new(
        id: "CUR013",
        title: "Update awaiter without an await site",
        messageFormat: "Handler '{0}' takes an IUpdateAwaiter parameter but no WaitFor*Async call on it was found; the handler will run to completion inline, and a suspended WaitFor would deadlock the sequential poller — call WaitFor* directly on the IUpdateAwaiter parameter (custom wrappers are not followed by design)",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TextMatchesOnNonMessageAwait = new(
        id: "CUR014",
        title: "TextMatches on a non-Message await target",
        messageFormat: "TextMatches can never match a non-Message await target ('{0}'); no implicit await branch is generated for it",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnknownFilterInAwaitChain = new(
        id: "CUR015",
        title: "Unknown filter in await chain",
        messageFormat: "Unknown filter '{0}' in an await chain; the filter check will not be emitted into the await branch",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedWherePredicate = new(
        id: "CUR016",
        title: "Unsupported Where predicate",
        messageFormat: "Where predicate '{0}' in an await chain is not supported ({1}); the predicate will not be emitted into the await branch",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnknownKeyboardPlaceholder = new(
        id: "CUR017",
        title: "Unknown keyboard placeholder",
        messageFormat: "Callback data placeholder '{0}' of generated keyboard '{1}' does not match any parameter; the generated implementation will not compile until it is removed or a matching parameter is added",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor WrongKeyboardReturnType = new(
        id: "CUR018",
        title: "Generated keyboard must return InlineKeyboardMarkup",
        messageFormat: "Member '{0}' is annotated with [CallbackButton] but returns '{1}'; the generator only implements members returning Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor KeyboardMemberNotPartial = new(
        id: "CUR019",
        title: "Generated keyboard member must be partial",
        messageFormat: "Member '{0}' is annotated with [CallbackButton] but is not partial; make the method (or C# 13 property) and its containing types partial so the generator can provide the implementation",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidBotFatherCommand = new(
        id: "CUR020",
        title: "Command alias is not a valid BotFather command",
        messageFormat: "Alias '{0}' of command handler '{1}' is not a valid BotFather command (must match ^[a-z0-9_]{{1,32}}$)",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingCommandDescription = new(
        id: "CUR021",
        title: "Command has no BotFather description",
        messageFormat: "Command '{0}' has no Description for BotFather synchronization; set Description or IsHidden = true on its [Command]",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CommandDescriptionTooLong = new(
        id: "CUR022",
        title: "Command description exceeds 256 characters",
        messageFormat: "The Description of command '{0}' is {1} characters; BotFather allows at most 256",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateBotFatherCommand = new(
        id: "CUR023",
        title: "Duplicate command in the same scope and language",
        messageFormat: "Command '{0}' is declared more than once for scope '{1}' and language '{2}'; only the first is synchronized",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ImpossibleCallbackPattern = new(
        id: "CUR024",
        title: "Callback pattern can never fit callback_data",
        messageFormat: "The literal skeleton of pattern '{0}' is {1} bytes; BotFather's callback_data limit is 64 bytes, so the pattern can never match",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CallbackPatternOverflow = new(
        id: "CUR025",
        title: "Callback pattern placeholders can never fit callback_data",
        messageFormat: "Pattern '{0}' needs at least {1} bytes including its non-empty placeholders, exceeding the 64-byte callback_data limit",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnboundCallbackPlaceholder = new(
        id: "CUR026",
        title: "Callback pattern placeholder has no [Arg] parameter",
        messageFormat: "Placeholder '{{{0}}}' of pattern '{1}' has no matching [Arg] parameter on the handler",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ArgMissingFromCallbackPattern = new(
        id: "CUR027",
        title: "[Arg] parameter is not captured by the callback pattern",
        messageFormat: "[Arg] parameter '{0}' of handler '{1}' is not captured by pattern '{2}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingTryParse = new(
        id: "CUR028",
        title: "[Arg] type has no accessible TryParse",
        messageFormat: "Type '{0}' of [Arg] parameter '{1}' has no accessible static TryParse(string, out {0}) or TryParse(ReadOnlySpan<char>, out {0}); the parameter binds to default in the generated code",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor PatternOnNonCallbackHandler = new(
        id: "CUR029",
        title: "[Pattern] requires a CallbackQuery handler",
        messageFormat: "Handler '{0}' declares [Pattern] but is not a [CallbackQueryHandler]; the pattern is ignored",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CallbackConstraintMismatch = new(
        id: "CUR030",
        title: "Callback pattern constraint does not match the parameter type",
        messageFormat: "Constraint '{{{0}:{1}}}' of pattern '{2}' does not match the type '{3}' of [Arg] parameter '{0}' (or the constraint is unknown)",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MalformedCallbackPattern = new(
        id: "CUR031",
        title: "Malformed callback pattern",
        messageFormat: "Pattern '{0}' is malformed ({1}); the pattern is ignored",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidExceptionHandlerParameter = new(
        id: "CUR032",
        title: "Exception handler parameter cannot be resolved in the error path",
        messageFormat: "Parameter '{0}' of the [ExceptionHandler] method is update-bound or keyed without a constant key; there is no current update in the error path, so the parameter is dropped",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MultipleExceptionHandlers = new(
        id: "CUR033",
        title: "Only one [ExceptionHandler] is allowed",
        messageFormat: "Method '{0}' is also decorated with [ExceptionHandler]; a bot has a single exception handler — the first declaration wins",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidExceptionHandlerSignature = new(
        id: "CUR034",
        title: "Exception handler must return Task or ValueTask",
        messageFormat: "Method '{0}' is decorated with [ExceptionHandler] but does not return Task or ValueTask; the attribute is ignored",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor HandlerInLibraryProject = new(
        id: "CUR036",
        title: "Handlers are not routed in library projects",
        messageFormat: "Handler '{0}' is declared in a library project: only runnable assemblies (OutputType Exe/WinExe) get a generated router, so this handler never runs — move it to the executable, libraries may only contribute filters and keyboards",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidThrottleConfiguration = new(
        id: "CUR035",
        title: "Invalid [Throttled] configuration",
        messageFormat: "[Throttled] on '{0}' has an invalid or non-constant {1}; the value is clamped to the default",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
