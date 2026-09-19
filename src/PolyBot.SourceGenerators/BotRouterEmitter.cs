using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PolyBot.SourceGenerators;

internal static class BotRouterEmitter
{
    public static string Generate(IReadOnlyList<HandlerModel> handlers, ExceptionHandlerModel? exceptionHandler)
    {
        bool hasCommands = handlers.Any(h => h.Aliases.Count > 0);
        List<AwaitSiteEntry> awaitSites = CollectAwaitSites(handlers);

        ClassDeclarationSyntax routerClass = SyntaxFactory.ClassDeclaration("BotRouter")
            .AddModifiers(SyntaxKind.PublicKeyword, SyntaxKind.PartialKeyword)
            .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("global::Telegram.Bot.Polling.IUpdateHandler")))
            .AddMembers(BuildServiceField());
        if (hasCommands)
        {
            routerClass = routerClass.AddMembers(BuildOptionsField());
        }

        List<MemberDeclarationSyntax> siteFields = BuildAwaitSiteConstantFields(awaitSites);
        if (siteFields.Count > 0)
        {
            routerClass = routerClass.AddMembers(siteFields.ToArray());
        }

        List<MemberDeclarationSyntax> throttleFields = new();
        HashSet<string> throttleFieldNames = new(StringComparer.Ordinal);
        routerClass = routerClass
            .AddMembers(BuildConstructor(hasCommands))
            .AddMembers(BuildHandleUpdateMethod(handlers, awaitSites, throttleFields, throttleFieldNames))
            .AddMembers(BuildHandleErrorMethod(exceptionHandler));
        if (throttleFields.Count > 0)
        {
            routerClass = routerClass.AddMembers(throttleFields.ToArray());
        }

        return EmitterSyntax.RenderPolyBotFile("PolyBot", routerClass);
    }

    /// <summary>
    /// Per-await-site static signature arrays so the delivery call passes the site's declarative signature without allocating per update.
    /// </summary>
    private static List<MemberDeclarationSyntax> BuildAwaitSiteConstantFields(List<AwaitSiteEntry> awaitSites)
    {
        List<MemberDeclarationSyntax> fields = new();
        foreach ((AwaitSiteModel site, int siteIndex) in awaitSites)
        {
            if (site.TextPatterns.Count > 0)
            {
                List<string> literals = new();
                foreach (string pattern in site.TextPatterns.Items)
                {
                    literals.Add(EscapeString(pattern));
                }

                fields.Add(EmitterSyntax.BuildStaticArrayField("string", $"__curator_awp_{siteIndex}", "new string[] { " + string.Join(", ", literals) + " }"));
            }

            if (site.Filters.Count > 0)
            {
                List<string> literals = new();
                foreach (string filter in site.Filters.Items)
                {
                    literals.Add($"typeof({filter})");
                }

                fields.Add(EmitterSyntax.BuildStaticArrayField("global::System.Type", $"__curator_awt_{siteIndex}", "new global::System.Type[] { " + string.Join(", ", literals) + " }"));
            }
        }

        return fields;
    }

    internal static string EscapeString(string value)
    {
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    internal static string EscapeChar(char value)
    {
        string escaped = value switch
        {
            '\0' => "\\0",
            '\\' => "\\\\",
            '\'' => "\\'",
            '\n' => "\\n",
            '\r' => "\\r",
            '\t' => "\\t",
            _ => value.ToString(),
        };
        return "'" + escaped + "'";
    }

    private static MemberDeclarationSyntax BuildServiceField()
    {
        return SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName("global::System.IServiceProvider"))
                    .AddVariables(SyntaxFactory.VariableDeclarator("_services")))
            .AddModifiers(SyntaxKind.PrivateKeyword, SyntaxKind.ReadOnlyKeyword);
    }

    private static MemberDeclarationSyntax BuildOptionsField()
    {
        return SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName("global::PolyBot.PolyBotOptions?"))
                    .AddVariables(SyntaxFactory.VariableDeclarator("@__curatorOptions")))
            .AddModifiers(SyntaxKind.PrivateKeyword, SyntaxKind.ReadOnlyKeyword);
    }

    private static ConstructorDeclarationSyntax BuildConstructor(bool registerOptions)
    {
        List<StatementSyntax> statements = new()
        {
            SyntaxFactory.ExpressionStatement(
                SyntaxFactory.AssignmentExpression(
                    kind: SyntaxKind.SimpleAssignmentExpression,
                    left: SyntaxFactory.IdentifierName("_services"),
                    right: SyntaxFactory.IdentifierName("services"))),
        };

        if (registerOptions)
        {
            statements.Add(SyntaxFactory.ExpressionStatement(
                SyntaxFactory.AssignmentExpression(
                    kind: SyntaxKind.SimpleAssignmentExpression,
                    left: SyntaxFactory.ParseExpression("this.@__curatorOptions"),
                    right: SyntaxFactory.ParseExpression("global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<global::PolyBot.PolyBotOptions>(services)"))));
        }

        return SyntaxFactory.ConstructorDeclaration("BotRouter")
            .AddModifiers(SyntaxKind.PublicKeyword)
            .AddParameterListParameters(EmitterSyntax.BuildParameter("services", "global::System.IServiceProvider"))
            .WithBody(SyntaxFactory.Block(statements));
    }

    private static MethodDeclarationSyntax BuildHandleUpdateMethod(IReadOnlyList<HandlerModel> handlers, List<AwaitSiteEntry> awaitSites, List<MemberDeclarationSyntax> throttleFields, HashSet<string> throttleFieldNames)
    {
        SwitchStatementSyntax switchStatement = SyntaxFactory.SwitchStatement(SyntaxFactory.ParseExpression("update.Type"))
            .WithSections(SyntaxFactory.List(BuildSwitchSections(handlers, awaitSites, throttleFields, throttleFieldNames)));

        bool usesStateContext = handlers.Any(UsesStateContext);
        bool usesAwaiter = handlers.Any(h => h.AwaitSites.Count > 0);

        List<StatementSyntax> bodyStatements = [];
        if (usesStateContext || usesAwaiter)
        {
            bodyStatements.Add(EmitterSyntax.BuildLocalDeclaration(
                "global::PolyBot.State.IUpdateContextAccessor",
                "__curator_ctx",
                EmitterSyntax.RequiredService("global::PolyBot.State.IUpdateContextAccessor")));
            bodyStatements.Add(SyntaxFactory.ExpressionStatement(SyntaxFactory.ParseExpression("__curator_ctx.Update = update")));
            if (handlers.Any(h => h.StateConditions.Count > 0))
            {
                bodyStatements.Add(EmitterSyntax.BuildLocalDeclaration(
                    "global::PolyBot.State.IStateStorage",
                    "__curator_storage",
                    EmitterSyntax.RequiredService("global::PolyBot.State.IStateStorage")));
            }

            if (usesAwaiter)
            {
                // Conversational awaits: implicit per-site branches inside the switch use
                // this engine to claim matching updates before handlers run.
                bodyStatements.Add(EmitterSyntax.BuildLocalDeclaration(
                    "global::PolyBot.Awaits.IUpdateAwaiter",
                    "__curator_awaiter",
                    EmitterSyntax.RequiredService("global::PolyBot.Awaits.IUpdateAwaiter")));
            }

            bodyStatements.Add(SyntaxFactory.TryStatement(
                block: SyntaxFactory.Block(switchStatement),
                catches: default,
                @finally: SyntaxFactory.FinallyClause(SyntaxFactory.Block(
                    SyntaxFactory.IfStatement(
                        condition: SyntaxFactory.ParseExpression("ReferenceEquals(__curator_ctx.Update, update)"),
                        statement: SyntaxFactory.Block(
                            SyntaxFactory.ExpressionStatement(SyntaxFactory.ParseExpression("__curator_ctx.Update = null"))))))));
        }
        else
        {
            bodyStatements.Add(switchStatement);
        }

        return SyntaxFactory.MethodDeclaration(
                returnType: SyntaxFactory.ParseTypeName("global::System.Threading.Tasks.Task"),
                identifier: "HandleUpdateAsync")
            .AddModifiers(SyntaxKind.PublicKeyword, SyntaxKind.AsyncKeyword)
            .AddParameterListParameters(
                EmitterSyntax.BuildParameter("botClient", "global::Telegram.Bot.ITelegramBotClient"),
                EmitterSyntax.BuildParameter("update", "global::Telegram.Bot.Types.Update"),
                EmitterSyntax.BuildParameter("cancellationToken", "global::System.Threading.CancellationToken"))
            .WithBody(SyntaxFactory.Block(bodyStatements));
    }

    private static bool UsesStateContext(HandlerModel handler)
    {
        if (handler.StateConditions.Count > 0)
        {
            return true;
        }

        foreach (ParameterModel parameter in handler.Parameters.Items)
        {
            if (parameter.Source is ParameterSource.StateMachine or ParameterSource.StateStorage)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Without a discovered <c>[ExceptionHandler]</c> errors are swallowed; a throw from the handler is swallowed too, so the polling loop survives.
    /// </summary>
    private static MethodDeclarationSyntax BuildHandleErrorMethod(ExceptionHandlerModel? exceptionHandler)
    {
        List<StatementSyntax> bodyStatements;
        if (exceptionHandler is null)
        {
            bodyStatements = new List<StatementSyntax>
            {
                SyntaxFactory.ReturnStatement(
                    SyntaxFactory.ParseExpression("global::System.Threading.Tasks.Task.CompletedTask")),
            };
        }
        else
        {
            List<StatementSyntax> invokeStatements = new();
            string target = exceptionHandler.IsStatic
                ? exceptionHandler.ContainingTypeFqn
                : AddInstanceResolution(invokeStatements, exceptionHandler.ContainingTypeFqn, "__curator_errorHandlerInstance");

            List<ArgumentSyntax> arguments = new();
            foreach (ParameterModel parameter in exceptionHandler.Parameters.Items)
            {
                arguments.Add(parameter.Source switch
                {
                    ParameterSource.BotClient => SyntaxFactory.Argument(SyntaxFactory.IdentifierName("botClient")),
                    ParameterSource.CancellationToken => SyntaxFactory.Argument(SyntaxFactory.IdentifierName("cancellationToken")),
                    ParameterSource.ErrorException => SyntaxFactory.Argument(SyntaxFactory.IdentifierName("exception")),
                    ParameterSource.ErrorSource => SyntaxFactory.Argument(SyntaxFactory.IdentifierName("source")),
                    ParameterSource.KeyedService => SyntaxFactory.Argument(SyntaxFactory.ParseExpression(
                        EmitterSyntax.RequiredKeyedService(parameter.TypeFqn, parameter.KeyLiteral!))),
                    _ => SyntaxFactory.Argument(SyntaxFactory.ParseExpression(
                        EmitterSyntax.RequiredService(parameter.TypeFqn))),
                });
            }

            invokeStatements.Add(SyntaxFactory.ExpressionStatement(SyntaxFactory.AwaitExpression(
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        kind: SyntaxKind.SimpleMemberAccessExpression,
                        expression: SyntaxFactory.ParseExpression(target),
                        name: SyntaxFactory.IdentifierName(exceptionHandler.MethodName)),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments))))));

            bodyStatements = new List<StatementSyntax>
            {
                SyntaxFactory.TryStatement(
                    block: SyntaxFactory.Block(invokeStatements),
                    catches: SyntaxFactory.SingletonList(SyntaxFactory.CatchClause()
                        .WithDeclaration(SyntaxFactory.CatchDeclaration(SyntaxFactory.ParseTypeName("global::System.Exception")))
                        .WithBlock(SyntaxFactory.Block())),
                    @finally: null),
            };
        }

        MethodDeclarationSyntax method = SyntaxFactory.MethodDeclaration(
                returnType: SyntaxFactory.ParseTypeName("global::System.Threading.Tasks.Task"),
                identifier: "HandleErrorAsync")
            .AddModifiers(SyntaxKind.PublicKeyword)
            .AddParameterListParameters(
                EmitterSyntax.BuildParameter("botClient", "global::Telegram.Bot.ITelegramBotClient"),
                EmitterSyntax.BuildParameter("exception", "global::System.Exception"),
                EmitterSyntax.BuildParameter("source", "global::Telegram.Bot.Polling.HandleErrorSource"),
                EmitterSyntax.BuildParameter("cancellationToken", "global::System.Threading.CancellationToken"))
            .WithBody(SyntaxFactory.Block(bodyStatements));

        if (exceptionHandler is not null)
        {
            method = method.AddModifiers(SyntaxKind.AsyncKeyword);
        }

        return method;
    }

    private static string AddInstanceResolution(List<StatementSyntax> statements, string typeFqn, string variableName)
    {
        statements.Add(EmitterSyntax.BuildLocalDeclaration(typeFqn, variableName, EmitterSyntax.RequiredService(typeFqn)));
        return variableName;
    }

    private static List<SwitchSectionSyntax> BuildSwitchSections(IReadOnlyList<HandlerModel> handlers, List<AwaitSiteEntry> awaitSites, List<MemberDeclarationSyntax> throttleFields, HashSet<string> throttleFieldNames)
    {
        List<SwitchSectionSyntax> sections = new();
        int caseIndex = 0;
        foreach (string memberName in EnumerateCaseMemberNames(handlers, awaitSites))
        {
            List<HandlerModel> caseHandlers = handlers
                .Where(h => h.UpdateTypeMemberName == memberName)
                .OrderByDescending(h => h.Priority)
                .ToList();

            List<AwaitSiteEntry> caseSites = awaitSites
                .Where(s => s.Site.Members.Items.Any(m => m.MemberName == memberName))
                .ToList();
            
            sections.Add(BuildSwitchSection(memberName, caseHandlers, caseSites, caseIndex, throttleFields, throttleFieldNames));
            caseIndex++;
        }

        return sections;
    }

    /// <summary>
    /// Await sites deduplicated across handlers, in first-discovery order.
    /// </summary>
    private static List<AwaitSiteEntry> CollectAwaitSites(IReadOnlyList<HandlerModel> handlers)
    {
        List<AwaitSiteEntry> sites = new();
        foreach (HandlerModel handler in handlers)
        {
            foreach (AwaitSiteModel site in handler.AwaitSites.Items)
            {
                if (!sites.Any(s => s.Site.Equals(site)))
                {
                    sites.Add(new AwaitSiteEntry(site, sites.Count));
                }
            }
        }

        return sites;
    }

    private static IEnumerable<string> EnumerateCaseMemberNames(
        IReadOnlyList<HandlerModel> handlers,
        List<AwaitSiteEntry> awaitSites)
    {
        HashSet<string> members = new(StringComparer.Ordinal);
        foreach (HandlerModel handler in handlers)
        {
            members.Add(handler.UpdateTypeMemberName);
        }

        foreach ((AwaitSiteModel site, _) in awaitSites)
        {
            foreach ((string memberName, _) in site.Members.Items)
            {
                members.Add(memberName);
            }
        }

        return members.OrderBy(m => m, StringComparer.Ordinal);
    }

    private static SwitchSectionSyntax BuildSwitchSection(
        string updateTypeMemberName,
        List<HandlerModel> caseHandlers,
        List<AwaitSiteEntry> caseSites,
        int caseIndex,
        List<MemberDeclarationSyntax> throttleFields,
        HashSet<string> throttleFieldNames)
    {
        List<StatementSyntax> statements = new();

        // Implicit await-site branches at the top of the case: static payload/regex/filter
        // pre-filters inline; the engine still verifies the pending registration.
        foreach ((AwaitSiteModel site, int siteIndex) in caseSites)
        {
            string? propertyName = null;
            foreach ((string memberName, string candidateProperty) in site.Members.Items)
            {
                if (memberName == updateTypeMemberName)
                {
                    propertyName = candidateProperty;
                    break;
                }
            }

            if (propertyName is not null)
            {
                statements.AddRange(BuildAwaitSiteBranch(site, siteIndex, propertyName, caseIndex));
            }
        }

        if (caseHandlers.Count > 0)
        {
            statements.AddRange(BuildCaseHandlerStatements(caseHandlers, caseIndex, throttleFields, throttleFieldNames));
        }

        statements.Add(SyntaxFactory.BreakStatement());

        return SyntaxFactory.SwitchSection()
            .AddLabels(SyntaxFactory.CaseSwitchLabel(
                SyntaxFactory.ParseExpression($"global::Telegram.Bot.Types.Enums.UpdateType.{updateTypeMemberName}")))
            .AddStatements(SyntaxFactory.Block(statements));
    }

    private static List<StatementSyntax> BuildAwaitSiteBranch(AwaitSiteModel site, int siteIndex, string propertyName, int caseIndex)
    {
        string payloadVar = $"__curator_aw_{siteIndex}_{caseIndex}";

        List<StatementSyntax> statements = new();
        List<string> filterVars = new();
        for (int i = 0; i < site.Filters.Count; i++)
        {
            string filterVar = $"__curator_awf_{siteIndex}_{i}";
            string filterType = site.Filters[i];
            statements.Add(EmitterSyntax.BuildLocalDeclaration(filterType, filterVar, EmitterSyntax.ResolveFilter(filterType)));
            filterVars.Add(filterVar);
        }

        List<ExpressionSyntax> conditions = new()
        {
            SyntaxFactory.ParseExpression($"update.{propertyName} is {{ }} {payloadVar}"),
        };

        int filterOrdinal = 0;
        foreach (AwaitConditionModel condition in site.Conditions.Items)
        {
            switch (condition.Kind)
            {
                case AwaitConditionKind.TextPattern:
                    {
                        conditions.Add(SyntaxFactory.ParseExpression(
                            $"global::System.Text.RegularExpressions.Regex.IsMatch({payloadVar}.Text ?? {payloadVar}.Caption ?? string.Empty, {EscapeString(condition.Value)})"));
                        break;
                    }

                case AwaitConditionKind.WhereLambda:
                    {
                        // The validated lambda is spliced verbatim (modifiers included); the
                        // Func cast re-infers the parameter types in the generated file.
                        conditions.Add(SyntaxFactory.ParseExpression(
                            $"((global::System.Func<{site.DtoTypeFqn}, bool>)({condition.Value}))({payloadVar})"));
                        break;
                    }

                case AwaitConditionKind.Filter:
                    {
                        conditions.Add(SyntaxFactory.ParseExpression(
                            $"__curator_awf_{siteIndex}_{filterOrdinal}.CanPass(update)"));
                        filterOrdinal++;
                        break;
                    }
            }
        }

        bool needsUser = site.KeyMode is AwaitKeyMode.UserId or AwaitKeyMode.UserInChat;
        bool needsChat = site.KeyMode is AwaitKeyMode.ChatId or AwaitKeyMode.UserInChat;

        List<StatementSyntax> body = new();
        if (needsUser)
        {
            body.Add(EmitterSyntax.BuildLocalDeclaration(
                "long?",
                $"__curator_awUser_{siteIndex}",
                "global::PolyBot.Routing.UpdateExtensions.GetUserId(update)"));
        }

        if (needsChat)
        {
            body.Add(EmitterSyntax.BuildLocalDeclaration(
                "long?",
                $"__curator_awChat_{siteIndex}",
                "global::PolyBot.Routing.UpdateExtensions.GetChatId(update)"));
        }

        string userArgument = needsUser ? $"__curator_awUser_{siteIndex}" : "null";
        string chatArgument = needsChat ? $"__curator_awChat_{siteIndex}" : "null";
        string patternsArgument = site.TextPatterns.Count > 0
            ? $"__curator_awp_{siteIndex}"
            : "global::System.Array.Empty<string>()";
        string filterTypesArgument = site.Filters.Count > 0
            ? $"__curator_awt_{siteIndex}"
            : "global::System.Array.Empty<global::System.Type>()";
        bool hasWhereCondition = site.Conditions.Items.Any(condition => condition.Kind == AwaitConditionKind.WhereLambda);
        body.Add(SyntaxFactory.IfStatement(
            condition: SyntaxFactory.ParseExpression(
                $"await __curator_awaiter.TryDeliverAsync({userArgument}, {chatArgument}, update, {payloadVar}, typeof({site.DtoTypeFqn}), {patternsArgument}, {filterTypesArgument}, {hasWhereCondition.ToString().ToLowerInvariant()}, cancellationToken)"),
            statement: SyntaxFactory.Block(SyntaxFactory.ReturnStatement())));

        statements.Add(SyntaxFactory.IfStatement(
            condition: ChainBinaryExpressions(SyntaxKind.LogicalAndExpression, conditions),
            statement: SyntaxFactory.Block(body)));

        return statements;
    }

    private static List<StatementSyntax> BuildCaseHandlerStatements(List<HandlerModel> caseHandlers, int caseIndex, List<MemberDeclarationSyntax> throttleFields, HashSet<string> throttleFieldNames)
    {
        string payloadVar = $"__curator_payload_{caseIndex}";
        string propertyName = caseHandlers[0].UpdatePropertyName;

        List<StatementSyntax> handlerStatements = new()
        {
            SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName(caseHandlers[0].PayloadTypeFqn))
                    .AddVariables(SyntaxFactory.VariableDeclarator(payloadVar)
                        .WithInitializer(SyntaxFactory.EqualsValueClause(
                            SyntaxFactory.PostfixUnaryExpression(
                                kind: SyntaxKind.SuppressNullableWarningExpression,
                                operand: SyntaxFactory.MemberAccessExpression(
                                    kind: SyntaxKind.SimpleMemberAccessExpression,
                                    expression: SyntaxFactory.IdentifierName("update"),
                                    name: SyntaxFactory.IdentifierName(propertyName))))))),
        };

        List<StateCasePair> casePairs = new();
        foreach (HandlerModel caseHandler in caseHandlers)
        {
            foreach (StateConditionModel condition in caseHandler.StateConditions.Items)
            {
                if (!casePairs.Any(p => p.EnumTypeFqn == condition.EnumTypeFqn && p.StateKeyName == condition.StateKeyName))
                {
                    casePairs.Add(new StateCasePair(condition.EnumTypeFqn, condition.StateKeyName));
                }
            }
        }

        for (int i = 0; i < casePairs.Count; i++)
        {
            string suffix = $"{caseIndex}_{i}";
            handlerStatements.Add(EmitterSyntax.BuildLocalDeclaration($"{casePairs[i].EnumTypeFqn}?", $"__curator_state_{suffix}", "null"));
            handlerStatements.Add(EmitterSyntax.BuildLocalDeclaration("bool", $"__curator_stateGot_{suffix}", "false"));
        }

        for (int i = 0; i < caseHandlers.Count; i++)
        {
            handlerStatements.AddRange(BuildHandlerStatements(caseHandlers[i], payloadVar, caseIndex, i, casePairs, throttleFields, throttleFieldNames));
        }

        return handlerStatements;
    }

    private static List<StatementSyntax> BuildHandlerStatements(
        HandlerModel handler,
        string payloadVar,
        int caseIndex,
        int handlerIndex,
        IReadOnlyList<StateCasePair> casePairs,
        List<MemberDeclarationSyntax> throttleFields,
        HashSet<string> throttleFieldNames)
    {
        string id = $"{caseIndex}_{handlerIndex}";

        List<StatementSyntax> statements = BuildGuardedHandlerStatements(handler, payloadVar, caseIndex, casePairs, id);
        if (handler.Throttle is null)
        {
            return statements;
        }

        ThrottleModel throttle = handler.Throttle;
        string gateVar = $"__curator_throttle_{id}";
        if (throttleFieldNames.Add(gateVar))
        {
            throttleFields.Add(SyntaxFactory.ParseMemberDeclaration(
                $"private static readonly global::PolyBot.Routing.ThrottleGate {gateVar} = new global::PolyBot.Routing.ThrottleGate({throttle.Limit}, {throttle.PeriodMilliseconds});")!);
        }

        // Guarded handlers (command/pattern/state) consume the budget only on a match —
        // the gate sits inside the guard, around the handler body. Guard-less handlers
        // (filter-only or plain) are gated at the top of their branch.
        bool hasMatchGuard = handler.Aliases.Count > 0 || handler.Pattern is not null || handler.StateConditions.Count > 0;
        if (hasMatchGuard)
        {
            return statements;
        }

        IfStatementSyntax guard = SyntaxFactory.IfStatement(
            condition: SyntaxFactory.ParseExpression($"{gateVar}.TryEnter({ThrottleKeyArguments(throttle)})"),
            statement: SyntaxFactory.Block(statements));
        if (throttle.ActionName == "Ignore")
        {
            guard = guard.WithElse(SyntaxFactory.ElseClause(SyntaxFactory.Block(SyntaxFactory.ReturnStatement())));
        }

        return new List<StatementSyntax> { guard };
    }

    private static string ThrottleKeyArguments(ThrottleModel throttle)
    {
        return throttle.ScopeName switch
        {
            "Global" => "0",
            "User" => "global::PolyBot.Routing.UpdateExtensions.GetUserId(update) ?? 0",
            "Chat" => "global::PolyBot.Routing.UpdateExtensions.GetChatId(update) ?? 0",
            _ => "global::PolyBot.Routing.UpdateExtensions.GetUserId(update) ?? 0, global::PolyBot.Routing.UpdateExtensions.GetChatId(update) ?? 0",
        };
    }

    /// <summary>
    /// The throttle gate consumes a budget slot only after the match guards passed; <see cref="ThrottleAction.Ignore"/> halts the routing branch, <see cref="ThrottleAction.Fallthrough"/> skips the handler.
    /// </summary>
    private static BlockSyntax BuildThrottledBody(HandlerModel handler, string payloadVar, string id)
    {
        BlockSyntax body = BuildHandlerBody(handler, payloadVar, id);
        if (handler.Throttle is not { } throttle)
        {
            return body;
        }

        string gateVar = $"__curator_throttle_{id}";
        IfStatementSyntax gate = SyntaxFactory.IfStatement(
            condition: SyntaxFactory.ParseExpression($"{gateVar}.TryEnter({ThrottleKeyArguments(throttle)})"),
            statement: body);
        if (throttle.ActionName == "Ignore")
        {
            gate = gate.WithElse(SyntaxFactory.ElseClause(SyntaxFactory.Block(SyntaxFactory.ReturnStatement())));
        }

        return SyntaxFactory.Block(gate);
    }

    private static List<StatementSyntax> BuildGuardedHandlerStatements(
        HandlerModel handler,
        string payloadVar,
        int caseIndex,
        IReadOnlyList<StateCasePair> casePairs,
        string id)
    {
        List<StatementSyntax> stateSetup = BuildStateEnsureBlocks(handler, caseIndex, casePairs);

        if (handler.Aliases.Count > 0)
        {
            List<ExpressionSyntax> guardParts = BuildCommandGuardParts(handler, payloadVar, id);
            if (handler.StateConditions.Count > 0)
            {
                guardParts.Add(BuildStateGuard(handler, caseIndex, casePairs));
            }

            List<StatementSyntax> statements = new(stateSetup);
            statements.Add(SyntaxFactory.IfStatement(
                condition: ChainBinaryExpressions(SyntaxKind.LogicalAndExpression, guardParts),
                statement: BuildThrottledBody(handler, payloadVar, id)));
            return statements;
        }

        if (handler.Pattern is not null)
        {
            List<ExpressionSyntax> guardParts = BuildPatternGuardParts(handler, payloadVar, id);
            if (handler.StateConditions.Count > 0)
            {
                guardParts.Add(BuildStateGuard(handler, caseIndex, casePairs));
            }

            List<StatementSyntax> statements = new(stateSetup);
            statements.Add(SyntaxFactory.IfStatement(
                condition: ChainBinaryExpressions(SyntaxKind.LogicalAndExpression, guardParts),
                statement: BuildThrottledBody(handler, payloadVar, id)));
            return statements;
        }

        if (handler.StateConditions.Count > 0)
        {
            List<StatementSyntax> statements = new(stateSetup);
            statements.Add(SyntaxFactory.IfStatement(
                condition: BuildStateGuard(handler, caseIndex, casePairs),
                statement: BuildThrottledBody(handler, payloadVar, id)));
            return statements;
        }

        if (handler.Filters.Count > 0)
        {
            // Filters need their own scope so locals never collide.
            return new List<StatementSyntax>
            {
                SyntaxFactory.Block(BuildHandlerBody(handler, payloadVar, id).Statements),
            };
        }

        return new List<StatementSyntax>(BuildHandlerBody(handler, payloadVar, id).Statements);
    }

    /// <summary>
    /// One ensure block per (enum, StateKey) pair; the got flag is set only when the key resolves, so <c>[NoState]</c> stays false for updates without a resolvable identity.
    /// </summary>
    private static List<StatementSyntax> BuildStateEnsureBlocks(
        HandlerModel handler,
        int caseIndex,
        IReadOnlyList<StateCasePair> casePairs)
    {
        List<StatementSyntax> statements = new();
        HashSet<int> emittedPairs = new();
        foreach (StateConditionModel condition in handler.StateConditions.Items)
        {
            int pairIndex = IndexOfCasePair(casePairs, condition);
            if (!emittedPairs.Add(pairIndex))
            {
                continue;
            }

            string suffix = $"{caseIndex}_{pairIndex}";
            statements.Add(SyntaxFactory.IfStatement(
                condition: SyntaxFactory.ParseExpression($"!__curator_stateGot_{suffix}"),
                statement: SyntaxFactory.Block(
                    SyntaxFactory.IfStatement(
                        condition: SyntaxFactory.ParseExpression(
                            $"global::PolyBot.State.StateKeyResolver.TryResolveKey(update, global::PolyBot.State.StateKey.{condition.StateKeyName}, out string __curator_stateKey_{suffix})"),
                        statement: SyntaxFactory.Block(
                            SyntaxFactory.ExpressionStatement(SyntaxFactory.ParseExpression($"__curator_stateGot_{suffix} = true")),
                            SyntaxFactory.ExpressionStatement(SyntaxFactory.ParseExpression(
                                $"__curator_state_{suffix} = await __curator_storage.GetStateAsync<{condition.EnumTypeFqn}>(__curator_stateKey_{suffix}, cancellationToken)")))))));
        }

        return statements;
    }

    private static List<ExpressionSyntax> BuildPatternGuardParts(HandlerModel handler, string payloadVar, string id)
    {
        PatternModel pattern = handler.Pattern!;
        string segmentsVar = $"__curator_seg_{id}";
        int minSegments = pattern.WildcardIndex >= 0 ? pattern.Segments.Count - 1 : pattern.Segments.Count;

        List<ExpressionSyntax> parts = new()
        {
            SyntaxFactory.ParseExpression($"{payloadVar}.Data is {{ }} __curator_data_{id}"),
            SyntaxFactory.ParseExpression(
                $"global::PolyBot.Routing.CallbackPatternMatcher.Match(__curator_data_{id}, {EscapeChar(pattern.Separator)}, {minSegments}, out global::PolyBot.Routing.CallbackSegments {segmentsVar})"),
        };

        List<PatternSegmentModel> segments = pattern.Segments.Items.ToList();
        for (int i = 0; i < segments.Count; i++)
        {
            PatternSegmentModel segment = segments[i];
            if (segment.IsLiteral)
            {
                parts.Add(SyntaxFactory.ParseExpression($"{segmentsVar}.EqualsOrdinal({i}, {EscapeString(segment.Text)})"));
                continue;
            }

            string argVar = $"__curator_arg_{segment.Text}_{id}";
            if (segment.IsWildcard)
            {
                parts.Add(SyntaxFactory.ParseExpression($"{segmentsVar}.JoinRest({i}) is {{ }} {argVar}"));
                continue;
            }

            if (segment.ParseKind == PatternParseKind.String)
            {
                parts.Add(SyntaxFactory.ParseExpression($"{segmentsVar}.Substring({i}) is {{ }} {argVar}"));
                continue;
            }

            string capVar = $"__curator_cap_{segment.Text}_{id}";
            parts.Add(SyntaxFactory.ParseExpression($"{segmentsVar}.Substring({i}) is {{ }} {capVar}"));
            if (segment.ParseKind == PatternParseKind.Enum)
            {
                parts.Add(SyntaxFactory.ParseExpression(
                    $"global::System.Enum.TryParse<{segment.SpecTypeFqn}>({capVar}, true, out {segment.SpecTypeFqn} {argVar})"));
            }
            else if (segment.ParseKind == PatternParseKind.TryParseInvariant)
            {
                parts.Add(SyntaxFactory.ParseExpression(
                    $"{segment.SpecTypeFqn}.TryParse({capVar}, global::System.Globalization.NumberStyles.Any, global::System.Globalization.CultureInfo.InvariantCulture, out {segment.SpecTypeFqn} {argVar})"));
            }
            else if (segment.ParseKind == PatternParseKind.TryParse)
            {
                parts.Add(SyntaxFactory.ParseExpression(
                    $"{segment.SpecTypeFqn}.TryParse({capVar}, out {segment.SpecTypeFqn} {argVar})"));
            }
        }

        return parts;
    }

    /// <summary>
    /// Same-pair conditions OR-ed, distinct pairs AND-ed.
    /// </summary>
    private static ExpressionSyntax BuildStateGuard(
        HandlerModel handler,
        int caseIndex,
        IReadOnlyList<StateCasePair> casePairs)
    {
        List<ExpressionSyntax> conditions = new();
        foreach (StateConditionModel condition in handler.StateConditions.Items)
        {
            string suffix = $"{caseIndex}_{IndexOfCasePair(casePairs, condition)}";
            if (condition.IsNoState)
            {
                conditions.Add(SyntaxFactory.ParseExpression(
                    $"(__curator_stateGot_{suffix} && !__curator_state_{suffix}.HasValue)"));
                continue;
            }

            List<ExpressionSyntax> comparands = new();
            foreach (string comparand in condition.Comparands.Items)
            {
                comparands.Add(SyntaxFactory.ParseExpression($"__curator_state_{suffix}.Value == {comparand}"));
            }

            ExpressionSyntax comparandExpression = comparands.Count == 1
                ? comparands[0]
                : SyntaxFactory.ParenthesizedExpression(ChainBinaryExpressions(SyntaxKind.LogicalOrExpression, comparands));
            conditions.Add(SyntaxFactory.ParseExpression(
                $"(__curator_state_{suffix}.HasValue && {comparandExpression})"));
        }

        return ChainBinaryExpressions(SyntaxKind.LogicalAndExpression, conditions);
    }

    private static int IndexOfCasePair(
        IReadOnlyList<StateCasePair> casePairs,
        StateConditionModel condition)
    {
        for (int i = 0; i < casePairs.Count; i++)
        {
            if (casePairs[i].EnumTypeFqn == condition.EnumTypeFqn && casePairs[i].StateKeyName == condition.StateKeyName)
            {
                return i;
            }
        }

        return -1;
    }

    private static BlockSyntax BuildHandlerBody(HandlerModel handler, string payloadVar, string id)
    {
        List<StatementSyntax> statements = new();
        List<StatementSyntax> argSetup = BuildArgSetupStatements(handler, payloadVar, id);

        string invocationTarget = handler.IsStatic
            ? handler.ContainingTypeFqn
            : AddInstanceResolution(statements, handler, id);

        InvocationExpressionSyntax invocation = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    kind: SyntaxKind.SimpleMemberAccessExpression,
                    expression: SyntaxFactory.ParseExpression(invocationTarget),
                    name: SyntaxFactory.IdentifierName(handler.MethodName)))
            .WithArgumentList(SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList(BuildArguments(handler, payloadVar, id))));

        if (handler.Filters.Count == 0)
        {
            statements.AddRange(argSetup);
            statements.AddRange(BuildInvocationStatements(handler, invocation, id));
            return SyntaxFactory.Block(statements);
        }

        // Filters: one DI-resolved instance per applied filter attribute, AND-ed in
        // attribute order. Resolution tries the service collection first and falls back to
        // ActivatorUtilities, so parameterless filters need no registration while filters
        // with constructor dependencies get them injected.
        List<string> filterVars = new();
        for (int i = 0; i < handler.Filters.Count; i++)
        {
            string filterVar = $"__curator_filter_{id}_{i}";
            string filterType = handler.Filters[i].TypeFqn;
            statements.Add(EmitterSyntax.BuildLocalDeclaration(filterType, filterVar, EmitterSyntax.ResolveFilter(filterType)));
            filterVars.Add(filterVar);
        }

        List<StatementSyntax> gated = new(argSetup.Concat(BuildInvocationStatements(handler, invocation, id)));
        for (int i = filterVars.Count - 1; i >= 0; i--)
        {
            gated = new List<StatementSyntax>
            {
                SyntaxFactory.IfStatement(
                    condition: SyntaxFactory.ParseExpression($"{filterVars[i]}.CanPass(update)"),
                    statement: SyntaxFactory.Block(gated)),
            };
        }

        statements.AddRange(gated);

        return SyntaxFactory.Block(statements);
    }

    private static List<StatementSyntax> BuildArgSetupStatements(HandlerModel handler, string payloadVar, string id)
    {
        List<StatementSyntax> statements = new();
        if (handler.Aliases.Count == 0)
        {
            return statements;
        }

        string cmdLenVar = $"__curator_cmdlen_{id}";
        if (handler.Args.Count == 0)
        {
            if (handler.TailConsumers.Count > 0)
            {
                // Tail values are read from the returned array, so it must be captured.
                statements.Add(SyntaxFactory.LocalDeclarationStatement(
                    SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName("object?[]"))
                        .AddVariables(SyntaxFactory.VariableDeclarator($"__curator_args_{id}")
                            .WithInitializer(SyntaxFactory.EqualsValueClause(BuildParseArgsInvocation(handler, payloadVar, cmdLenVar))))));
            }
            else if (handler.NoArgs)
            {
                statements.Add(SyntaxFactory.ExpressionStatement(SyntaxFactory.ParseExpression(
                    $"global::PolyBot.Commands.CommandHelper.ParseArgs({payloadVar}.Text!, {cmdLenVar}, global::System.Array.Empty<global::PolyBot.Commands.ArgSpec>(), true, global::System.Array.Empty<string?>())")));
            }

            return statements;
        }

        statements.Add(SyntaxFactory.LocalDeclarationStatement(
            SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName("object?[]"))
                .AddVariables(SyntaxFactory.VariableDeclarator($"__curator_args_{id}")
                    .WithInitializer(SyntaxFactory.EqualsValueClause(BuildParseArgsInvocation(handler, payloadVar, cmdLenVar))))));

        return statements;
    }

    private static ExpressionSyntax BuildParseArgsInvocation(HandlerModel handler, string payloadVar, string cmdLenVar)
    {
        List<ExpressionSyntax> specCreations = new();
        foreach (ArgModel arg in handler.Args.Items)
        {
            specCreations.Add(SyntaxFactory.ObjectCreationExpression(
                    type: SyntaxFactory.ParseTypeName("global::PolyBot.Commands.ArgSpec"),
                    argumentList: SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
                    {
                        SyntaxFactory.Argument(SyntaxFactory.TypeOfExpression(SyntaxFactory.ParseTypeName(arg.SpecTypeFqn))),
                        SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(kind: SyntaxKind.StringLiteralExpression, token: SyntaxFactory.Literal(arg.Name))),
                        SyntaxFactory.Argument(arg.IsOptional
                            ? SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression)
                            : SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression)),
                    })),
                    initializer: null));
        }

        ArrayCreationExpressionSyntax specsArray = SyntaxFactory.ArrayCreationExpression(
            type: SyntaxFactory.ArrayType(
                elementType: SyntaxFactory.ParseTypeName("global::PolyBot.Commands.ArgSpec"),
                rankSpecifiers: SyntaxFactory.SingletonList(SyntaxFactory.ArrayRankSpecifier())),
            initializer: SyntaxFactory.InitializerExpression(
                kind: SyntaxKind.ArrayInitializerExpression,
                expressions: SyntaxFactory.SeparatedList(specCreations)));

        return SyntaxFactory.InvocationExpression(
            expression: SyntaxFactory.ParseExpression("global::PolyBot.Commands.CommandHelper.ParseArgs"),
            argumentList: SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
            {
                SyntaxFactory.Argument(SyntaxFactory.ParseExpression($"{payloadVar}.Text!")),
                SyntaxFactory.Argument(SyntaxFactory.IdentifierName(cmdLenVar)),
                SyntaxFactory.Argument(specsArray),
                SyntaxFactory.Argument(BoolLiteral(handler.NoArgs)),
                SyntaxFactory.Argument(BuildTailPatternsExpression(handler)),
            })));
    }

    private static ExpressionSyntax BuildTailPatternsExpression(HandlerModel handler)
    {
        if (handler.TailConsumers.Count == 0)
        {
            return SyntaxFactory.ParseExpression("global::System.Array.Empty<string?>()");
        }

        List<ExpressionSyntax> elements = new();
        foreach (TailConsumerModel tail in handler.TailConsumers.Items)
        {
            elements.Add(tail.Pattern is null
                ? SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)
                : SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(tail.Pattern)));
        }

        return SyntaxFactory.ArrayCreationExpression(
            type: SyntaxFactory.ArrayType(
                elementType: SyntaxFactory.ParseTypeName("string?"),
                rankSpecifiers: SyntaxFactory.SingletonList(SyntaxFactory.ArrayRankSpecifier())),
            initializer: SyntaxFactory.InitializerExpression(
                kind: SyntaxKind.ArrayInitializerExpression,
                expressions: SyntaxFactory.SeparatedList(elements)));
    }

    private static ExpressionSyntax BoolLiteral(bool value)
    {
        return value
            ? SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression)
            : SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression);
    }

    private static string AddInstanceResolution(List<StatementSyntax> statements, HandlerModel handler, string id)
    {
        return AddInstanceResolution(statements, handler.ContainingTypeFqn, $"__curator_instance_{id}");
    }

    private static List<StatementSyntax> BuildInvocationStatements(HandlerModel handler, InvocationExpressionSyntax invocation, string id)
    {
        if (handler.AwaitSites.Count > 0)
        {
            return BuildWorkflowInvocationStatements(handler, invocation, id);
        }

        string resultVar = $"__curator_result_{id}";
        if (handler.ReturnKind == HandlerReturnKind.Plain)
        {
            return new List<StatementSyntax>
            {
                SyntaxFactory.ExpressionStatement(SyntaxFactory.AwaitExpression(invocation)),
            };
        }

        return new List<StatementSyntax>
        {
            SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName("global::PolyBot.Routing.Result"))
                    .AddVariables(SyntaxFactory.VariableDeclarator(resultVar)
                        .WithInitializer(SyntaxFactory.EqualsValueClause(SyntaxFactory.AwaitExpression(invocation))))),
            SyntaxFactory.IfStatement(
                condition: SyntaxFactory.ParseExpression($"!{resultVar}.ContinueRouting"),
                statement: SyntaxFactory.Block(SyntaxFactory.ReturnStatement())),
        };
    }

    /// <summary>
    /// Invokes a conversational handler without awaiting; when it suspends it is detached via <c>ObserveFireAndForget</c> and the update counts as handled.
    /// </summary>
    private static List<StatementSyntax> BuildWorkflowInvocationStatements(HandlerModel handler, InvocationExpressionSyntax invocation, string id)
    {
        string taskVar = $"__curator_wf_{id}";
        string taskExpression = invocation.ToString();
        if (handler.ReturnsValueTask)
        {
            taskExpression += ".AsTask()";
        }

        if (handler.ReturnKind == HandlerReturnKind.Plain)
        {
            return new List<StatementSyntax>
            {
                EmitterSyntax.BuildLocalDeclaration("global::System.Threading.Tasks.Task", taskVar, taskExpression),
                SyntaxFactory.IfStatement(
                    condition: SyntaxFactory.ParseExpression($"!{taskVar}.IsCompleted"),
                    statement: SyntaxFactory.Block(SyntaxFactory.ReturnStatement())),
                SyntaxFactory.ExpressionStatement(SyntaxFactory.AwaitExpression(SyntaxFactory.ParseExpression(taskVar))),
            };
        }

        string resultVar = $"__curator_wfResult_{id}";
        return new List<StatementSyntax>
        {
            EmitterSyntax.BuildLocalDeclaration("global::System.Threading.Tasks.Task<global::PolyBot.Routing.Result>", taskVar, taskExpression),
            SyntaxFactory.IfStatement(
                condition: SyntaxFactory.ParseExpression($"!{taskVar}.IsCompleted"),
                statement: SyntaxFactory.Block(
                    SyntaxFactory.ExpressionStatement(SyntaxFactory.ParseExpression(
                        $"global::PolyBot.Routing.WorkflowSupport.ObserveFireAndForget({taskVar})")),
                    SyntaxFactory.ReturnStatement())),
            EmitterSyntax.BuildLocalDeclaration("global::PolyBot.Routing.Result", resultVar, $"await {taskVar}"),
            SyntaxFactory.IfStatement(
                condition: SyntaxFactory.ParseExpression($"!{resultVar}.ContinueRouting"),
                statement: SyntaxFactory.Block(SyntaxFactory.ReturnStatement())),
        };
    }

    private static List<ExpressionSyntax> BuildCommandGuardParts(HandlerModel handler, string payloadVar, string id)
    {
        string cmdLenVar = $"__curator_cmdlen_{id}";

        List<ExpressionSyntax> checks = new();
        int aliasIndex = 0;
        foreach (string alias in handler.Aliases.Items)
        {
            string outArgument = aliasIndex == 0 ? $"out int {cmdLenVar}" : $"out {cmdLenVar}";
            checks.Add(SyntaxFactory.ParseExpression(
                $"global::PolyBot.Commands.CommandHelper.MatchCommand({payloadVar}, {EscapeString(alias)}, {EscapeChar(handler.Prefix)}, this.@__curatorOptions?.BotUsername, {outArgument})"));
            aliasIndex++;
        }

        ExpressionSyntax checksExpression = checks.Count == 1
            ? checks[0]
            : SyntaxFactory.ParenthesizedExpression(ChainBinaryExpressions(SyntaxKind.LogicalOrExpression, checks));

        return new List<ExpressionSyntax>
        {
            SyntaxFactory.ParseExpression($"{payloadVar}.Text is not null"),
            checksExpression,
        };
    }

    private static List<ArgumentSyntax> BuildArguments(HandlerModel handler, string payloadVar, string id)
    {
        List<ArgumentSyntax> arguments = new();
        foreach (ParameterModel parameter in handler.Parameters.Items)
        {
            arguments.Add(parameter.Source switch
            {
                ParameterSource.BotClient => SyntaxFactory.Argument(SyntaxFactory.IdentifierName("botClient")),
                ParameterSource.CancellationToken => SyntaxFactory.Argument(SyntaxFactory.IdentifierName("cancellationToken")),
                ParameterSource.Update => SyntaxFactory.Argument(SyntaxFactory.IdentifierName("update")),
                ParameterSource.Payload => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(payloadVar)),
                ParameterSource.Arg => SyntaxFactory.Argument(BuildBoundValueExpression(handler, parameter, id)),
                ParameterSource.Rest => SyntaxFactory.Argument(BuildBoundValueExpression(handler, parameter, id)),
                ParameterSource.Parse => SyntaxFactory.Argument(BuildBoundValueExpression(handler, parameter, id)),
                ParameterSource.KeyedService => SyntaxFactory.Argument(SyntaxFactory.ParseExpression(
                    EmitterSyntax.RequiredKeyedService(parameter.TypeFqn, parameter.KeyLiteral!))),
                ParameterSource.StateMachine => SyntaxFactory.Argument(SyntaxFactory.ParseExpression(
                    EmitterSyntax.RequiredService("global::PolyBot.State.IStateMachine"))),
                ParameterSource.StateStorage => SyntaxFactory.Argument(SyntaxFactory.ParseExpression(
                    EmitterSyntax.RequiredService("global::PolyBot.State.IStateStorage"))),
                ParameterSource.Awaiter => SyntaxFactory.Argument(SyntaxFactory.ParseExpression(
                    EmitterSyntax.RequiredService("global::PolyBot.Awaits.IUpdateAwaiter"))),
                _ => SyntaxFactory.Argument(SyntaxFactory.ParseExpression(
                    EmitterSyntax.RequiredService(parameter.TypeFqn))),
            });
        }

        return arguments;
    }

    /// <summary>
    /// Pattern captures whose type failed the TryParse check (CUR028) bind to <c>default</c> so the reported error is the only failure.
    /// </summary>
    private static ExpressionSyntax BuildBoundValueExpression(HandlerModel handler, ParameterModel parameter, string id)
    {
        if (handler.Pattern is not null)
        {
            ArgModel arg = handler.Args[parameter.ArgIndex];
            PatternParseKind parseKind = PatternParseKind.TryParse;
            foreach (PatternSegmentModel segment in handler.Pattern.Segments.Items)
            {
                if (!segment.IsLiteral && string.Equals(segment.Text, arg.Name, StringComparison.OrdinalIgnoreCase))
                {
                    parseKind = segment.ParseKind;
                    break;
                }
            }

            if (parseKind == PatternParseKind.Failed)
            {
                return SyntaxFactory.ParseExpression($"default({parameter.TypeFqn})!");
            }

            return SyntaxFactory.ParseExpression($"({parameter.TypeFqn})__curator_arg_{arg.Name}_{id}");
        }

        return SyntaxFactory.ParseExpression($"({parameter.TypeFqn})__curator_args_{id}[{parameter.ArgIndex}]!");
    }

    private static ExpressionSyntax ChainBinaryExpressions(SyntaxKind kind, IReadOnlyList<ExpressionSyntax> expressions)
    {
        ExpressionSyntax result = expressions[0];
        for (int i = 1; i < expressions.Count; i++)
        {
            result = SyntaxFactory.BinaryExpression(kind: kind, left: result, right: expressions[i]);
        }

        return result;
    }
}
