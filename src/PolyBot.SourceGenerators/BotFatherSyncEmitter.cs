using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PolyBot.SourceGenerators;

internal static class BotFatherSyncEmitter
{
    private const string BotCommandFqn = "global::Telegram.Bot.Types.BotCommand";

    private const string ScopeNamespace = "global::Telegram.Bot.Types";

    public static string Generate(IReadOnlyList<HandlerModel> handlers)
    {
        List<SyncCommand> commands = new();
        foreach (HandlerModel handler in handlers)
        {
            if (handler.Prefix != '/' || handler.Aliases.Count == 0 || handler.CommandHidden)
            {
                continue;
            }

            commands.Add(new SyncCommand(
                handler.Aliases[0],
                handler.CommandDescription ?? string.Empty,
                handler.CommandScope,
                handler.CommandLanguageCode));
        }

        List<CommandGroup> groups = GroupCommands(commands);

        ClassDeclarationSyntax syncClass = SyntaxFactory.ClassDeclaration("PolyBotBotFatherSync")
            .AddModifiers(SyntaxKind.PublicKeyword, SyntaxKind.SealedKeyword)
            .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("global::PolyBot.BotFather.IBotFatherSync")))
            .AddMembers(BuildClientField())
            .AddMembers(BuildCommandsArray(commands))
            .AddMembers(BuildGroupArrays(groups).ToArray())
            .AddMembers(BuildConstructor())
            .AddMembers(BuildDiscoveredCommandsProperty())
            .AddMembers(BuildSyncMethod(groups))
            .WithLeadingTrivia(
                SyntaxFactory.Comment("/// <summary>Generated <see cref=\"PolyBot.IBotFatherSync\"/> implementation from the discovered [Command] metadata.</summary>"),
                SyntaxFactory.LineFeed);

        return EmitterSyntax.RenderPolyBotFile("PolyBot", syncClass);
    }

    private static FieldDeclarationSyntax BuildClientField()
    {
        return SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName("global::Telegram.Bot.ITelegramBotClient"))
                    .AddVariables(SyntaxFactory.VariableDeclarator("_client")))
            .AddModifiers(SyntaxKind.PrivateKeyword, SyntaxKind.ReadOnlyKeyword);
    }

    private static FieldDeclarationSyntax BuildCommandsArray(List<SyncCommand> commands)
    {
        if (commands.Count == 0)
        {
            return EmitterSyntax.BuildStaticArrayField(BotCommandFqn, "Commands", "new " + BotCommandFqn + "[0]");
        }

        List<string> initializers = new();
        foreach (SyncCommand command in commands)
        {
            initializers.Add(
                "new " + BotCommandFqn + "(" + BotRouterEmitter.EscapeString(command.Name)
                + ", " + BotRouterEmitter.EscapeString(command.Description) + ")");
        }

        return EmitterSyntax.BuildStaticArrayField(BotCommandFqn, "Commands", "new " + BotCommandFqn + "[] { " + string.Join(", ", initializers) + " }");
    }

    private static IEnumerable<MemberDeclarationSyntax> BuildGroupArrays(List<CommandGroup> groups)
    {
        List<MemberDeclarationSyntax> fields = new();
        for (int i = 0; i < groups.Count; i++)
        {
            List<string> refs = new();
            foreach (int commandIndex in groups[i].CommandIndexes)
            {
                refs.Add($"Commands[{commandIndex}]");
            }

            fields.Add(EmitterSyntax.BuildStaticArrayField(BotCommandFqn, $"Group{i}", "new " + BotCommandFqn + "[] { " + string.Join(", ", refs) + " }"));
        }

        return fields;
    }

    private static ConstructorDeclarationSyntax BuildConstructor()
    {
        return SyntaxFactory.ConstructorDeclaration("PolyBotBotFatherSync")
            .AddModifiers(SyntaxKind.PublicKeyword)
            .AddParameterListParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("client"))
                    .WithType(SyntaxFactory.ParseTypeName("global::Telegram.Bot.ITelegramBotClient")))
            .WithBody(SyntaxFactory.Block(
                SyntaxFactory.ExpressionStatement(SyntaxFactory.ParseExpression("_client = client"))));
    }

    private static PropertyDeclarationSyntax BuildDiscoveredCommandsProperty()
    {
        return SyntaxFactory.PropertyDeclaration(
                type: SyntaxFactory.ParseTypeName("global::System.Collections.Generic.IReadOnlyList<" + BotCommandFqn + ">"),
                identifier: "DiscoveredCommands")
            .AddModifiers(SyntaxKind.PublicKeyword)
            .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.SingletonList(
                SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithBody(SyntaxFactory.Block(SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName("Commands")))))));
    }

    private static MethodDeclarationSyntax BuildSyncMethod(List<CommandGroup> groups)
    {
        List<StatementSyntax> statements = new();
        for (int i = 0; i < groups.Count; i++)
        {
            CommandGroup group = groups[i];
            string scopeCreation = group.Scope switch
            {
                "AllPrivateChats" => "new " + ScopeNamespace + ".BotCommandScopeAllPrivateChats()",
                "AllGroupChats" => "new " + ScopeNamespace + ".BotCommandScopeAllGroupChats()",
                "AllChatAdministrators" => "new " + ScopeNamespace + ".BotCommandScopeAllChatAdministrators()",
                _ => "new " + ScopeNamespace + ".BotCommandScopeDefault()",
            };
            string languageArgument = group.LanguageCode is null ? "null" : BotRouterEmitter.EscapeString(group.LanguageCode);

            statements.Add(SyntaxFactory.ExpressionStatement(SyntaxFactory.AwaitExpression(SyntaxFactory.ParseExpression(
                "global::Telegram.Bot.TelegramBotClientExtensions.SetMyCommands(_client, Group" + i + ", " + scopeCreation + ", " + languageArgument + ", ct).ConfigureAwait(false)"))));
        }

        return SyntaxFactory.MethodDeclaration(
                returnType: SyntaxFactory.ParseTypeName("global::System.Threading.Tasks.Task"),
                identifier: "SyncCommandsAsync")
            .AddModifiers(SyntaxKind.PublicKeyword, SyntaxKind.AsyncKeyword)
            .AddParameterListParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("ct"))
                    .WithType(SyntaxFactory.ParseTypeName("global::System.Threading.CancellationToken"))
                    .WithDefault(SyntaxFactory.EqualsValueClause(SyntaxFactory.ParseExpression("default"))))
            .WithBody(SyntaxFactory.Block(statements))
            .WithLeadingTrivia(
                SyntaxFactory.Comment("/// <summary>One <c>SetMyCommands</c> call per distinct (scope, language) group.</summary>"),
                SyntaxFactory.LineFeed);
    }

    private static List<CommandGroup> GroupCommands(List<SyncCommand> commands)
    {
        List<CommandGroup> groups = new();
        for (int i = 0; i < commands.Count; i++)
        {
            SyncCommand command = commands[i];
            CommandGroup? group = null;
            foreach (CommandGroup candidate in groups)
            {
                if (candidate.Scope == command.Scope && candidate.LanguageCode == command.LanguageCode)
                {
                    group = candidate;
                    break;
                }
            }

            if (group is null)
            {
                group = new CommandGroup(command.Scope, command.LanguageCode);
                groups.Add(group);
            }

            group.CommandIndexes.Add(i);
        }

        // Deterministic order: by scope declaration order, then language (default first).
        groups.Sort(static (left, right) =>
        {
            int byScope = Array.IndexOf(CommandScopeOrder, left.Scope).CompareTo(Array.IndexOf(CommandScopeOrder, right.Scope));
            if (byScope != 0)
            {
                return byScope;
            }

            return string.CompareOrdinal(left.LanguageCode, right.LanguageCode);
        });
        return groups;
    }

    private static readonly string[] CommandScopeOrder = { "Default", "AllPrivateChats", "AllGroupChats", "AllChatAdministrators" };

    private sealed class SyncCommand
    {
        public SyncCommand(string name, string description, string scope, string? languageCode)
        {
            Name = name;
            Description = description;
            Scope = scope;
            LanguageCode = languageCode;
        }

        public string Name { get; }

        public string Description { get; }

        public string Scope { get; }

        public string? LanguageCode { get; }
    }

    private sealed class CommandGroup
    {
        public CommandGroup(string scope, string? languageCode)
        {
            Scope = scope;
            LanguageCode = languageCode;
            CommandIndexes = new List<int>();
        }

        public string Scope { get; }

        public string? LanguageCode { get; }

        public List<int> CommandIndexes { get; }
    }
}
