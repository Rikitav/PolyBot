using PolyBot.Attributes;
using PolyBot.Tests.Fixtures;
using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace PolyBot.Tests;

public interface IKeyedService
{
    string Name { get; }
}

public sealed class KeyedService : IKeyedService
{
    public KeyedService(string name)
    {
        Name = name;
    }

    public string Name { get; }
}

public interface IWelcomeService
{
    string Name { get; }
}

public sealed class WelcomeService : IWelcomeService
{
    public string Name => "welcome-impl";
}

public sealed class AllowAllFilter : MessageFilter
{
    protected override bool CanPass(Message message)
    {
        return true;
    }
}

public sealed class DenyAllFilter : MessageFilter
{
    protected override bool CanPass(Message message)
    {
        return false;
    }
}

public interface IGateService
{
    bool Open { get; }
}

public sealed class GateService : IGateService
{
    public bool Open => true;
}

// Consumed-flag lets the first call pass and vetoes later ones; IGateService only comes from DI.
public sealed class DiGateFilter : MessageFilter
{
    private readonly IGateService _gate;
    private bool _consumed;

    public DiGateFilter(IGateService gate)
    {
        _gate = gate;
    }

    protected override bool CanPass(Message message)
    {
        if (!_gate.Open || _consumed)
        {
            return false;
        }

        _consumed = true;
        return true;
    }
}

public sealed class NonEmptyTextFilter : MessageFilter
{
    protected override bool CanPass(Message message)
    {
        return !string.IsNullOrEmpty(message.Text);
    }
}

public enum RegistrationStep
{
    AwaitingAge,
    Completed,
}

public sealed partial class TestHandlers
{
    [CallbackButton("First", "first:{val}"), CallbackButton("Second", "second:{val}")]
    [CallbackButton("Cancel", "cancel"), CallbackButton("Apply", "apply:{val}")]
    public static partial InlineKeyboardMarkup TestKeyboard(int val);

    [CallbackButton("Yes", "yes"), CallbackButton("No", "no")]
    public static partial InlineKeyboardMarkup ConfirmKeyboard { get; }

    [MessageHandler(Priority = -1)]
    [Command(Aliases = ["start"], Description = "Start the bot")]
    public static async Task<Result> StartHandler(
        Message msg,
        ITelegramBotClient bot,
        [Key("lol")] IKeyedService keyed,
        IWelcomeService welcome,
        CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, $"start:{keyed.Name}:{welcome.Name}", cancellationToken: ct);
        return Result.StopRouting;
    }

    [MessageHandler(Priority = -2)]
    [Command(Aliases = ["start"], Description = "Запустить бота", LanguageCode = "ru")]
    public static async Task<Result> StartRuHandler(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, "start:ru", cancellationToken: ct);
        return Result.Handled();
    }

    [MessageHandler(Priority = -100)]
    public static async Task<Result> EchoHandler(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, $"echo:{msg.Text}", cancellationToken: ct);
        return Result.Continue;
    }

    [MessageHandler(Priority = -1)]
    [Command(Aliases = ["add"], Description = "Add two numbers")]
    public static async Task<Result> AddHandler(
        Message msg,
        ITelegramBotClient bot,
        [Arg(Name = "lhs")] int x,
        [Arg] int y,
        CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, $"{x + y}", cancellationToken: ct);
        return Result.Handled();
    }

    [MessageHandler(Priority = -1)]
    [Command(Aliases = ["greet"], Description = "Greet someone")]
    public static async Task<Result> GreetHandler(
        Message msg,
        ITelegramBotClient bot,
        [Arg(IsOptional = true)] string? name,
        CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, $"greet:{name ?? "<null>"}", cancellationToken: ct);
        return Result.Handled();
    }

    [MessageHandler(Priority = -1)]
    [Command(Aliases = ["say"], Description = "Repeat a phrase")]
    public static async Task<Result> SayHandler(
        Message msg,
        ITelegramBotClient bot,
        [Arg] int times,
        [Rest] string phrase,
        CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, $"say:{times}:{phrase}", cancellationToken: ct);
        return Result.Handled();
    }

    [MessageHandler(Priority = -1)]
    [Command(Aliases = ["note"], Description = "Save a note")]
    public static async Task<Result> NoteHandler(Message msg, ITelegramBotClient bot, [Rest] string body, CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, $"note:{body}", cancellationToken: ct);
        return Result.Handled();
    }

    [MessageHandler(Priority = -1)]
    [Command(Aliases = ["parse"], Description = "Parse demo")]
    public static async Task<Result> ParseHandler(
        Message msg,
        ITelegramBotClient bot,
        [Arg] int id,
        [Parse("[0-9]{2}")] string digits,
        [Rest] string rest,
        CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, $"parse:{id}:{digits}:{rest}", cancellationToken: ct);
        return Result.Handled();
    }

    [MessageHandler(Priority = -1)]
    [Command(Aliases = ["ping"], NoArgs = true, Description = "Ping")]
    public static async Task<Result> PingHandler(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, "pong", cancellationToken: ct);
        return Result.Handled();
    }

    [MessageHandler(Priority = -1)]
    [Command(Aliases = ["hi"], Prefix = '!', Description = "Say hi")]
    public static async Task<Result> HiHandler(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, "hi there", cancellationToken: ct);
        return Result.Handled();
    }

    [MessageHandler(Priority = -1)]
    [Command(Aliases = ["time"], Prefix = ' ', Description = "Tell the time")]
    public static async Task<Result> TimeHandler(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, "time now", cancellationToken: ct);
        return Result.Handled();
    }

    // Positional constructor argument: [Command("pos", "positional")] ≡ Aliases = ["pos", "positional"].
    [MessageHandler(Priority = -1)]
    [Command("pos", "positional", Description = "Positional alias demo")]
    public static async Task<Result> PositionalAliasHandler(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, "positional", cancellationToken: ct);
        return Result.Handled();
    }

    // No Description on purpose: the command routes, but BotFather sync must skip it
    // (empty descriptions are rejected by the Bot API). Intentional CUR021 warning.
    [MessageHandler(Priority = -1)]
    [Command(Aliases = ["nodesc"])]
    public static async Task<Result> NoDescriptionHandler(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, "nodesc", cancellationToken: ct);
        return Result.Handled();
    }

    [MessageHandler(Priority = -1)]
    [Command(Aliases = ["fsm"], Description = "FSM demo")]
    [NoState<RegistrationStep>]
    public static async Task<Result> NoStateHandler(Message msg, IStateMachine fsm, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, "nostate", cancellationToken: ct);
        await fsm.SetAsync(RegistrationStep.AwaitingAge);
        return Result.Handled();
    }

    [MessageHandler(Priority = -2)]
    [State<RegistrationStep>(RegistrationStep.AwaitingAge)]
    public static async Task<Result> AwaitingAgeHandler(Message msg, IStateMachine fsm, ITelegramBotClient bot, CancellationToken ct)
    {
        RegistrationStep? current = await fsm.GetAsync<RegistrationStep>();
        await bot.SendMessage(msg.Chat.Id, $"state:{current}", cancellationToken: ct);
        await fsm.SetAsync(RegistrationStep.Completed);
        return Result.Handled();
    }

    [MessageHandler(Priority = -1)]
    [Command(Aliases = ["allow"], Description = "Allow demo")]
    [AllowAllFilter]
    public static async Task<Result> AllowHandler(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, "allowed", cancellationToken: ct);
        return Result.Handled();
    }

    [MessageHandler(Priority = -1)]
    [Command(Aliases = ["deny"], Description = "Deny demo")]
    [DenyAllFilter]
    public static async Task<Result> DenyHandler(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, "denied", cancellationToken: ct);
        return Result.Handled();
    }

    [MessageHandler(Priority = -1)]
    [Command(Aliases = ["gate"], Description = "Gate demo")]
    [DiGateFilter]
    public static async Task<Result> GateHandler(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, "gate:open", cancellationToken: ct);
        return Result.Handled();
    }

    [MessageHandler(Priority = -2)]
    [Command(Aliases = ["send"], Description = "Send the demo keyboard")]
    public static async Task<Result> SendHandler(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, "menu", replyMarkup: TestKeyboard(5), cancellationToken: ct);
        return Result.Handled();
    }

    [MessageHandler(Priority = -5)]
    [Command(Aliases = ["register"], Description = "Register")]
    public static async Task<Result> RegisterHandler(Message msg, IUpdateAwaiter awaiter, ITelegramBotClient bot, CancellationToken ct)
    {
        Message? next = await awaiter.WaitForMessageAsync()
            .TextMatches(@"my age is \d+")
            .WithNonEmptyTextFilter()
            .ByUserId(ct);

        if (next is not null)
        {
            await bot.SendMessage(msg.Chat.Id, $"register:{next.Text}", cancellationToken: ct);
        }

        return Result.Handled();
    }

    [MessageHandler(Priority = -5)]
    [Command(Aliases = ["fast"], Description = "Fast await demo")]
    public static async Task<Result> FastHandler(Message msg, IUpdateAwaiter awaiter, ITelegramBotClient bot, CancellationToken ct)
    {
        Message? next = await awaiter.WaitForMessageAsync(TimeSpan.FromMilliseconds(80)).ByUserId(ct);
        await bot.SendMessage(msg.Chat.Id, "fast:" + (next is null ? "timeout" : "got"), cancellationToken: ct);
        return Result.Handled();
    }

    [MessageHandler(Priority = -5)]
    [Command(Aliases = ["room"], Description = "Room demo")]
    public static async Task<Result> RoomHandler(Message msg, IUpdateAwaiter awaiter, ITelegramBotClient bot, CancellationToken ct)
    {
        Message? next = await awaiter.WaitForMessageAsync()
            .Where(static m => (m.Text ?? m.Caption)!.Length > 5)
            .ByChatId(ct);

        if (next is not null)
        {
            await bot.SendMessage(msg.Chat.Id, $"room:{next.Text}", cancellationToken: ct);
        }

        return Result.Handled();
    }

    [MessageHandler(Priority = -50)]
    [Command(Aliases = ["boom"], IsHidden = true)]
    public static Task<Result> BoomHandler(Message msg)
    {
        throw new InvalidOperationException("boom");
    }

    [MessageHandler(Priority = -1)]
    [Command(Aliases = ["secret"], IsHidden = true)]
    public static async Task<Result> SecretHandler(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, "secret", cancellationToken: ct);
        return Result.Handled();
    }

    [CallbackQueryHandler(Priority = -100)]
    public static async Task<Result> OnCallback(CallbackQuery query, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.AnswerCallbackQuery(query.Id, cancellationToken: ct);
        return Result.Handled();
    }

    [CallbackQueryHandler(Priority = -1)]
    [Pattern("item:{id}:{action}")]
    public static async Task<Result> OnItemAction(CallbackQuery query, [Arg] int id, [Arg] string action, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(query.Message!.Chat.Id, $"item:{id}:{action}", cancellationToken: ct);
        return Result.Handled();
    }

    [CallbackQueryHandler(Priority = -1)]
    [Pattern("logs:{*path}")]
    public static async Task<Result> OnLogsPath(CallbackQuery query, [Arg] string path, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(query.Message!.Chat.Id, $"logs:{path}", cancellationToken: ct);
        return Result.Handled();
    }

    [CallbackQueryHandler(Priority = -1)]
    [Pattern("pay:{amount:decimal}")]
    public static async Task<Result> OnPay(CallbackQuery query, [Arg] decimal amount, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(query.Message!.Chat.Id, string.Create(CultureInfo.InvariantCulture, $"pay:{amount}"), cancellationToken: ct);
        return Result.Handled();
    }

    [MessageHandler(Priority = -4)]
    [Command(Aliases = ["window"], IsHidden = true)]
    [Throttled(Limit = 1, PeriodMilliseconds = 250, Scope = ThrottleScope.User, Action = ThrottleAction.Fallthrough)]
    public static async Task<Result> WindowHandler(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, "window", cancellationToken: ct);
        return Result.Handled();
    }

    [MessageHandler(Priority = -4)]
    [Command(Aliases = ["burst"], IsHidden = true)]
    [Throttled(Limit = 5, PeriodMilliseconds = 60000, Scope = ThrottleScope.Global, Action = ThrottleAction.Fallthrough)]
    public static async Task<Result> BurstHandler(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, "burst", cancellationToken: ct);
        return Result.Handled();
    }

    [MessageHandler(Priority = -4)]
    [Command(Aliases = ["halted"], IsHidden = true)]
    [Throttled(Limit = 1, PeriodMilliseconds = 60000, Scope = ThrottleScope.User, Action = ThrottleAction.Ignore)]
    public static async Task<Result> HaltedHandler(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, "halted", cancellationToken: ct);
        return Result.Handled();
    }

    [MessageHandler(Priority = -6)]
    [FixtureFilter]
    public static async Task<Result> OnFixtureFiltered(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, "fixture-pass", cancellationToken: ct);
        return Result.Handled();
    }

    [MessageHandler(Priority = -6)]
    [Command(Aliases = ["fixtureawait"], IsHidden = true)]
    public static async Task<Result> OnFixtureAwait(Message msg, IUpdateAwaiter awaiter, ITelegramBotClient bot, CancellationToken ct)
    {
        Message? next = await awaiter.WaitForMessageAsync()
            .WithFixtureFilter()
            .ByUserId(ct);
        await bot.SendMessage(msg.Chat.Id, $"fixture-await:{next?.Text}", cancellationToken: ct);
        return Result.Handled();
    }

    // Migrated PolyBot.Filters: parameterized attribute (positional args).
    [MessageHandler(Priority = -7)]
    [TextEqualsFilter("filtertrigger")]
    public static async Task<Result> OnFilterTrigger(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, "filter-trigger", cancellationToken: ct);
        return Result.Handled();
    }

    // Migrated PolyBot.Filters: parameterized attribute with a named optional argument.
    [MessageHandler(Priority = -7)]
    [TextStartsWithFilter("pre", Comparison = StringComparison.Ordinal)]
    public static async Task<Result> OnOrdinalPrefix(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, "ordinal-prefix", cancellationToken: ct);
        return Result.Handled();
    }

    // Migrated PolyBot.Filters: parameterless attribute.
    [MessageHandler(Priority = -7)]
    [FromBotFilter]
    public static async Task<Result> OnFromBot(Message msg, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(msg.Chat.Id, "from-bot", cancellationToken: ct);
        return Result.Handled();
    }

    // Migrated PolyBot.Filters: With* await extension with constructor arguments.
    [MessageHandler(Priority = -7)]
    [Command(Aliases = ["filterawait"], IsHidden = true)]
    public static async Task<Result> OnFilterAwait(Message msg, IUpdateAwaiter awaiter, ITelegramBotClient bot, CancellationToken ct)
    {
        Message? next = await awaiter.WaitForMessageAsync()
            .WithTextEqualsFilter("awaittrigger")
            .ByUserId(ct);
        await bot.SendMessage(msg.Chat.Id, $"filter-await:{next?.Text}", cancellationToken: ct);
        return Result.Handled();
    }

    [ExceptionHandler]
    public static async Task OnError(Exception exception, HandleErrorSource source, ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendMessage(424242, $"error:{exception.GetType().Name}", cancellationToken: ct);
    }
}
