using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PolyBot.SourceGenerators;

/// <summary>
/// Discovers handler methods; source-generated handler attributes may still be error symbols
/// on the first pass, in which case the update type falls back to the naming convention.
/// </summary>
internal static class HandlerDiscovery
{
    private const string HandlerAttributeSuffix = "HandlerAttribute";

    public static HandlerItem Discover(GeneratorSyntaxContext context, EquatableArray<FilterClassModel> filterClasses)
    {
        MethodDeclarationSyntax methodSyntax = (MethodDeclarationSyntax)context.Node;
        SemanticModel semanticModel = context.SemanticModel;
        if (semanticModel.GetDeclaredSymbol(methodSyntax) is not IMethodSymbol methodSymbol)
        {
            return HandlerItem.Skip;
        }

        Compilation compilation = semanticModel.Compilation;
        INamedTypeSymbol? handlerBase = compilation.GetTypeByMetadataName("PolyBot.Attributes.HandlerAttribute");
        INamedTypeSymbol? commandAttribute = compilation.GetTypeByMetadataName("PolyBot.Attributes.CommandAttribute");
        INamedTypeSymbol? argAttributeBase = compilation.GetTypeByMetadataName("PolyBot.Attributes.ArgAttribute");
        INamedTypeSymbol? restAttribute = compilation.GetTypeByMetadataName("PolyBot.Attributes.RestAttribute");
        INamedTypeSymbol? parseAttribute = compilation.GetTypeByMetadataName("PolyBot.Attributes.ParseAttribute");
        INamedTypeSymbol? stateAttributeBase = compilation.GetTypeByMetadataName("PolyBot.Attributes.StateAttribute`1");
        INamedTypeSymbol? noStateAttributeBase = compilation.GetTypeByMetadataName("PolyBot.Attributes.NoStateAttribute`1");
        INamedTypeSymbol? stateKeyEnum = compilation.GetTypeByMetadataName("PolyBot.State.StateKey");
        INamedTypeSymbol? stateMachineType = compilation.GetTypeByMetadataName("PolyBot.State.IStateMachine");
        INamedTypeSymbol? stateStorageType = compilation.GetTypeByMetadataName("PolyBot.State.IStateStorage");
        INamedTypeSymbol? updateAwaiterType = compilation.GetTypeByMetadataName("PolyBot.Awaits.IUpdateAwaiter");
        INamedTypeSymbol? patternAttribute = compilation.GetTypeByMetadataName("PolyBot.Attributes.PatternAttribute");
        INamedTypeSymbol? callbackQueryType = compilation.GetTypeByMetadataName("Telegram.Bot.Types.CallbackQuery");
        INamedTypeSymbol? updateFilterType = compilation.GetTypeByMetadataName("PolyBot.Routing.IUpdateFilter");
        INamedTypeSymbol? throttleAttribute = compilation.GetTypeByMetadataName("PolyBot.Attributes.ThrottledAttribute");
        INamedTypeSymbol? nullableType = compilation.GetTypeByMetadataName("System.Nullable`1");
        INamedTypeSymbol? keyAttribute = compilation.GetTypeByMetadataName("PolyBot.Attributes.KeyAttribute");
        INamedTypeSymbol? descriptorAttribute = compilation.GetTypeByMetadataName("PolyBot.Attributes.UpdateHandlerDescriptorAttribute");
        INamedTypeSymbol? updateHandlerAttribute = compilation.GetTypeByMetadataName("PolyBot.Attributes.UpdateHandlerAttribute");
        INamedTypeSymbol? updateTypeEnum = compilation.GetTypeByMetadataName("Telegram.Bot.Types.Enums.UpdateType");
        INamedTypeSymbol? updateClass = compilation.GetTypeByMetadataName("Telegram.Bot.Types.Update");
        INamedTypeSymbol? botClientType = compilation.GetTypeByMetadataName("Telegram.Bot.ITelegramBotClient");
        INamedTypeSymbol? cancellationTokenType = compilation.GetTypeByMetadataName("System.Threading.CancellationToken");
        INamedTypeSymbol? messageType = compilation.GetTypeByMetadataName("Telegram.Bot.Types.Message");
        INamedTypeSymbol? taskType = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
        INamedTypeSymbol? taskOfTType = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
        INamedTypeSymbol? valueTaskType = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1");
        INamedTypeSymbol? valueTaskNonGenericType = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask");

        if (handlerBase is null || updateTypeEnum is null || updateClass is null)
        {
            return HandlerItem.Skip;
        }

        AttributeData? handlerAttributeData = null;
        string? handlerAttributeClassName = null;
        int handlerAttributeCount = 0;
        foreach (AttributeData attributeData in methodSymbol.GetAttributes())
        {
            INamedTypeSymbol? attributeClass = attributeData.AttributeClass;
            if (attributeClass is null)
            {
                continue;
            }

            if (IsHandlerAttribute(attributeClass, handlerBase, updateTypeEnum))
            {
                handlerAttributeCount++;
                handlerAttributeData ??= attributeData;
                handlerAttributeClassName ??= attributeClass.Name;
            }
        }

        if (handlerAttributeData is null || handlerAttributeClassName is null)
        {
            return HandlerItem.Skip;
        }

        bool isRawUpdateHandler = updateHandlerAttribute is not null &&
            handlerAttributeData.AttributeClass is { } matchedHandlerAttribute &&
            SymbolEqualityComparer.Default.Equals(matchedHandlerAttribute, updateHandlerAttribute);

        Location? handlerAttributeLocation = handlerAttributeData.ApplicationSyntaxReference?.GetSyntax().GetLocation();
        if (isRawUpdateHandler && handlerAttributeCount > 1)
        {
            Diagnostic conflictDiagnostic = Diagnostic.Create(
                PolyBotDiagnostics.MultipleHandlerAttributes,
                handlerAttributeLocation ?? methodSymbol.Locations.FirstOrDefault(),
                methodSymbol.Name);
            return HandlerItem.ForModel(null, new List<Diagnostic> { conflictDiagnostic });
        }

        string updateTypeMemberName;
        string updatePropertyName;
        List<string> updateTypeMemberNames;
        if (isRawUpdateHandler)
        {
            // [UpdateHandler] is always a bound symbol (hand-written in the PolyBot assembly),
            // so Types is read from the bound attribute data; empty means a universal catch-all.
            updateTypeMemberNames = ReadUpdateHandlerTypes(handlerAttributeData, updateTypeEnum);
            if (updateTypeMemberNames.Count == 0)
            {
                updateTypeMemberName = string.Empty;
                updatePropertyName = string.Empty;
            }
            else
            {
                updateTypeMemberName = updateTypeMemberNames[0];
                updatePropertyName = UpdateShape.FindUpdatePropertyName(updateClass, updateTypeMemberName);
            }
        }
        else
        {
            if (!TryResolveUpdateType(handlerAttributeData, descriptorAttribute, updateTypeEnum, updateClass, handlerAttributeClassName, out updateTypeMemberName, out updatePropertyName))
            {
                return HandlerItem.Skip;
            }

            updateTypeMemberNames = new List<string> { updateTypeMemberName };
        }

        ITypeSymbol? payloadType = isRawUpdateHandler
            ? updateClass
            : FindUpdateProperty(updateClass, updatePropertyName)?.Type;

        HandlerReturnKind? returnKind = ClassifyReturnType(methodSymbol.ReturnType, taskType, taskOfTType, valueTaskType, valueTaskNonGenericType, handlerBase.ContainingAssembly);
        List<Diagnostic> diagnostics = new();
        if (returnKind is null)
        {
            Location? location = methodSymbol.Locations.FirstOrDefault();
            Diagnostic diagnostic = Diagnostic.Create(
                PolyBotDiagnostics.UnsupportedHandlerSignature,
                location,
                methodSymbol.Name);
            return HandlerItem.ForModel(null, new List<Diagnostic> { diagnostic });
        }

        // If any parameter or return type failed to bind, skip silently; the user will see the binding error.
        if (methodSymbol.ReturnType.TypeKind == TypeKind.Error ||
            methodSymbol.Parameters.Any(p => p.Type.TypeKind == TypeKind.Error))
        {
            return HandlerItem.Skip;
        }

        // Priority: named argument on the attribute, with a syntactic fallback for
        // source-generated attribute classes that are still error symbols.
        int priority = GetNamedArgument(handlerAttributeData, "Priority") is { Kind: TypedConstantKind.Primitive, Value: int p }
            ? p
            : GetPriorityFromSyntax(semanticModel, methodSyntax, handlerAttributeClassName);

        List<string> aliases = new();
        AttributeData? commandData = null;
        if (commandAttribute is not null)
        {
            foreach (AttributeData attributeData in methodSymbol.GetAttributes())
            {
                if (attributeData.AttributeClass is null ||
                    !InheritsFrom(attributeData.AttributeClass, commandAttribute))
                {
                    continue;
                }

                commandData ??= attributeData;
                aliases.AddRange(ReadStringArray(attributeData, "Aliases"));
            }
        }

        char prefix = '/';
        bool noArgs = false;
        string? commandDescription = null;
        string? commandLanguageCode = null;
        string commandScope = "Default";
        bool commandHidden = false;
        if (commandData is not null)
        {
            if (GetNamedArgument(commandData, "NoArgs") is { Kind: TypedConstantKind.Primitive, Value: bool noArgsValue })
            {
                noArgs = noArgsValue;
            }

            prefix = ReadCommandPrefix(commandData, methodSymbol, diagnostics);

            if (GetNamedArgument(commandData, "Description") is { Kind: TypedConstantKind.Primitive, Value: string descriptionValue })
            {
                commandDescription = descriptionValue;
            }

            if (GetNamedArgument(commandData, "LanguageCode") is { Kind: TypedConstantKind.Primitive, Value: string languageValue })
            {
                commandLanguageCode = languageValue;
            }

            if (GetNamedArgument(commandData, "Scope") is { Kind: TypedConstantKind.Enum, Value: int scopeOrdinal } &&
                scopeOrdinal >= 0 && scopeOrdinal < CommandScopeNames.Length)
            {
                commandScope = CommandScopeNames[scopeOrdinal];
            }

            if (GetNamedArgument(commandData, "IsHidden") is { Kind: TypedConstantKind.Primitive, Value: bool hiddenValue })
            {
                commandHidden = hiddenValue;
            }
        }

        AttributeData? patternData = patternAttribute is not null
            ? methodSymbol.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass is not null && InheritsFrom(a.AttributeClass, patternAttribute))
            : null;

        bool isMessagePayload = messageType is not null && payloadType is not null &&
            SymbolEqualityComparer.Default.Equals(payloadType, messageType);

        Diagnostic? extraDiagnostic = null;
        if (aliases.Count > 0 && !isMessagePayload)
        {
            extraDiagnostic = Diagnostic.Create(
                PolyBotDiagnostics.CommandOnNonMessagePayload,
                methodSymbol.Locations.FirstOrDefault(),
                methodSymbol.Name);
            aliases.Clear();
        }

        if (commandData is not null && prefix == '/' && aliases.Count > 0)
        {
            ValidateBotFatherMetadata(methodSymbol, aliases, commandDescription, commandHidden, diagnostics);
        }

        // [Arg]/[Parse]/[Rest] on parameters; only command-restricted handlers (aliases
        // present) can bind them, otherwise the parameter stays a normal dependency
        // (CUR003 when [Command] is missing entirely).
        List<ArgModel> args = new();
        List<TailConsumerModel> tailConsumers = new();
        if (argAttributeBase is not null || restAttribute is not null || parseAttribute is not null)
        {
            foreach (IParameterSymbol parameter in methodSymbol.Parameters)
            {
                AttributeData? restData = restAttribute is not null
                    ? parameter.GetAttributes().FirstOrDefault(a =>
                        a.AttributeClass is not null && InheritsFrom(a.AttributeClass, restAttribute))
                    : null;
                if (restData is not null)
                {
                    if (commandData is null)
                    {
                        diagnostics.Add(Diagnostic.Create(
                            PolyBotDiagnostics.ArgOnNonCommandHandler,
                            parameter.Locations.FirstOrDefault(),
                            methodSymbol.Name));
                        continue;
                    }

                    if (aliases.Count == 0)
                    {
                        continue; // [Command] without aliases: the handler is not command-guarded.
                    }

                    if (tailConsumers.Any(c => c.Pattern is null))
                    {
                        diagnostics.Add(Diagnostic.Create(
                            PolyBotDiagnostics.MultipleRestParameters,
                            parameter.Locations.FirstOrDefault(),
                            methodSymbol.Name));
                        continue;
                    }

                    if (parameter.Type.SpecialType != SpecialType.System_String)
                    {
                        diagnostics.Add(Diagnostic.Create(
                            PolyBotDiagnostics.RestParameterNotString,
                            parameter.Locations.FirstOrDefault(),
                            parameter.Name,
                            methodSymbol.Name));
                        continue;
                    }

                    tailConsumers.Add(new TailConsumerModel
                    {
                        ParameterIndex = parameter.Ordinal,
                        Pattern = null,
                    });
                    continue;
                }

                AttributeData? parseData = parseAttribute is not null
                    ? parameter.GetAttributes().FirstOrDefault(a =>
                        a.AttributeClass is not null && InheritsFrom(a.AttributeClass, parseAttribute))
                    : null;
                if (parseData is not null)
                {
                    if (commandData is null)
                    {
                        diagnostics.Add(Diagnostic.Create(
                            PolyBotDiagnostics.ArgOnNonCommandHandler,
                            parameter.Locations.FirstOrDefault(),
                            methodSymbol.Name));
                        continue;
                    }

                    if (aliases.Count == 0)
                    {
                        continue; // [Command] without aliases: the handler is not command-guarded.
                    }

                    if (tailConsumers.Any(c => c.Pattern is null))
                    {
                        diagnostics.Add(Diagnostic.Create(
                            PolyBotDiagnostics.ParseAfterRest,
                            parameter.Locations.FirstOrDefault(),
                            parameter.Name,
                            methodSymbol.Name));
                        continue;
                    }

                    if (parameter.Type.SpecialType != SpecialType.System_String)
                    {
                        diagnostics.Add(Diagnostic.Create(
                            PolyBotDiagnostics.ParseParameterNotString,
                            parameter.Locations.FirstOrDefault(),
                            parameter.Name,
                            methodSymbol.Name));
                        continue;
                    }

                    if (parseData.ConstructorArguments.Length != 1 ||
                        parseData.ConstructorArguments[0].Value is not string pattern)
                    {
                        continue; // Missing constant pattern; nothing to scan with.
                    }

                    tailConsumers.Add(new TailConsumerModel
                    {
                        ParameterIndex = parameter.Ordinal,
                        Pattern = pattern,
                    });
                    continue;
                }

                AttributeData? argData = argAttributeBase is not null
                    ? parameter.GetAttributes().FirstOrDefault(a =>
                        a.AttributeClass is not null && InheritsFrom(a.AttributeClass, argAttributeBase))
                    : null;
                if (argData is null)
                {
                    continue;
                }

                if (commandData is null && patternData is null)
                {
                    diagnostics.Add(Diagnostic.Create(
                        PolyBotDiagnostics.ArgOnNonCommandHandler,
                        parameter.Locations.FirstOrDefault(),
                        methodSymbol.Name));
                    continue;
                }

                if (aliases.Count == 0 && patternData is null)
                {
                    continue; // [Command] without aliases: the handler is not command-guarded.
                }

                ITypeSymbol specType = UnwrapNullable(parameter.Type, nullableType);
                if (!IsCompatibleArgType(parameter.Type, specType, nullableType))
                {
                    diagnostics.Add(Diagnostic.Create(
                        PolyBotDiagnostics.IncompatibleArgParameterType,
                        parameter.Locations.FirstOrDefault(),
                        parameter.Name,
                        methodSymbol.Name));
                    continue;
                }

                string? explicitName = argData.ConstructorArguments.Length == 1 &&
                                       argData.ConstructorArguments[0].Value is string ctorName
                    ? ctorName
                    : null;
                if (explicitName is null &&
                    GetNamedArgument(argData, "Name") is { Kind: TypedConstantKind.Primitive, Value: string namedName })
                {
                    explicitName = namedName;
                }

                string argName = string.IsNullOrEmpty(explicitName) ? parameter.Name : explicitName!;

                bool isOptional = GetNamedArgument(argData, "IsOptional") is { Kind: TypedConstantKind.Primitive, Value: true };
                args.Add(new ArgModel
                {
                    SpecTypeFqn = specType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    ParamTypeFqn = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    Name = argName,
                    IsOptional = isOptional,
                    ParameterIndex = parameter.Ordinal,
                });
            }
        }

        // Filter wrapper attributes: own-assembly wrappers are error symbols in the same
        // pass (matched by name stem below); wrappers from referenced filter libraries
        // are compiled types, resolved semantically by pairing the wrapper with the
        // concrete filter type in its assembly (no assembly-wide scanning).
        List<FilterModel> filters = new();
        foreach (AttributeData attributeData in methodSymbol.GetAttributes())
        {
            INamedTypeSymbol? attributeClass = attributeData.AttributeClass;
            if (attributeClass is null)
            {
                continue;
            }

            string stem = attributeClass.Name;
            if (stem.EndsWith("Attribute", StringComparison.Ordinal))
            {
                stem = stem.Substring(0, stem.Length - "Attribute".Length);
            }

            FilterModel? match = null;
            foreach (FilterClassModel filterClass in filterClasses.Items)
            {
                if (filterClass.ShortName == stem)
                {
                    match = BuildFilterModel(filterClass.TypeFqn, filterClass.Ctors, attributeData, semanticModel, diagnostics);
                    break;
                }
            }

            if (match is null && !attributeClass.IsImplicitlyDeclared &&
                attributeClass.TypeKind != TypeKind.Error &&
                !SymbolEqualityComparer.Default.Equals(attributeClass.ContainingAssembly, methodSymbol.ContainingAssembly))
            {
                INamedTypeSymbol? libraryFilter = FindFilterInAssembly(attributeClass.ContainingAssembly, stem, updateFilterType);
                if (libraryFilter is not null)
                {
                    match = BuildFilterModel(
                        libraryFilter.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        FilterCtorInfo.ReadMirroredCtors(libraryFilter),
                        attributeData,
                        semanticModel,
                        diagnostics);
                }
            }

            if (match is not null)
            {
                filters.Add(match);
            }
        }

        // [State]/[NoState] conditions: same (enum, StateKey) pair folds into one condition
        // (comparands OR-ed); distinct pairs become separate AND-ed conditions.
        List<StateConditionModel> stateConditions = new();
        if (stateAttributeBase is not null || noStateAttributeBase is not null)
        {
            foreach (AttributeData attributeData in methodSymbol.GetAttributes())
            {
                INamedTypeSymbol? attributeClass = attributeData.AttributeClass;
                if (attributeClass is null || attributeClass.TypeKind == TypeKind.Error)
                {
                    continue;
                }

                bool isState = stateAttributeBase is not null &&
                               SymbolEqualityComparer.Default.Equals(attributeClass.OriginalDefinition, stateAttributeBase);
                bool isNoState = !isState && noStateAttributeBase is not null &&
                                 SymbolEqualityComparer.Default.Equals(attributeClass.OriginalDefinition, noStateAttributeBase);
                if (!isState && !isNoState)
                {
                    continue;
                }

                if (attributeClass.TypeArguments.Length != 1 ||
                    attributeClass.TypeArguments[0] is not INamedTypeSymbol { TypeKind: Enum } enumType)
                {
                    diagnostics.Add(Diagnostic.Create(
                        PolyBotDiagnostics.StateTypeArgumentMustBeEnum,
                        methodSymbol.Locations.FirstOrDefault(),
                        methodSymbol.Name));
                    continue;
                }

                string enumTypeFqn = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                string enumTypeDisplayName = enumType.ToDisplayString(
                    SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));

                string stateKeyName = "UserId";
                int keyArgumentIndex = isState ? 1 : 0;
                if (stateKeyEnum is not null &&
                    attributeData.ConstructorArguments.Length > keyArgumentIndex &&
                    attributeData.ConstructorArguments[keyArgumentIndex].Value is int keyValue)
                {
                    stateKeyName = EnumValueToMemberName(stateKeyEnum, keyValue) ?? "UserId";
                }

                string? comparand = null;
                if (isState && attributeData.ConstructorArguments.Length >= 1)
                {
                    object? stateValue = attributeData.ConstructorArguments[0].Value;
                    if (stateValue is not null)
                    {
                        string? memberName = EnumValueToMemberName(enumType, stateValue);
                        if (memberName is not null)
                        {
                            comparand = enumTypeFqn + "." + memberName;
                        }
                    }
                }

                int sameIndex = IndexOfStateCondition(stateConditions, enumTypeFqn, stateKeyName, isNoState);
                if (sameIndex >= 0)
                {
                    if (comparand is not null)
                    {
                        StateConditionModel existing = stateConditions[sameIndex];
                        string[] merged = existing.Comparands.Items.Concat(new[] { comparand }).ToArray();
                        stateConditions[sameIndex] = new StateConditionModel
                        {
                            EnumTypeFqn = existing.EnumTypeFqn,
                            EnumTypeDisplayName = existing.EnumTypeDisplayName,
                            StateKeyName = existing.StateKeyName,
                            IsNoState = existing.IsNoState,
                            Comparands = new EquatableArray<string>(merged),
                        };
                    }

                    continue;
                }

                if (IndexOfStateCondition(stateConditions, enumTypeFqn, stateKeyName, !isNoState) >= 0)
                {
                    diagnostics.Add(Diagnostic.Create(
                        PolyBotDiagnostics.MutuallyExclusiveStateConditions,
                        methodSymbol.Locations.FirstOrDefault(),
                        methodSymbol.Name));
                }

                stateConditions.Add(new StateConditionModel
                {
                    EnumTypeFqn = enumTypeFqn,
                    EnumTypeDisplayName = enumTypeDisplayName,
                    StateKeyName = stateKeyName,
                    IsNoState = isNoState,
                    Comparands = new EquatableArray<string>(comparand is null ? [] : new[] { comparand }),
                });
            }
        }

        List<ParameterModel> parameters = new();
        bool acceptsRawUpdate = false;
        foreach (IParameterSymbol parameter in methodSymbol.Parameters)
        {
            ParameterSource source = ParameterSource.Service;
            string? keyLiteral = null;
            int argIndex = IndexOfArg(args, parameter.Ordinal);
            int tailIndex = IndexOfTailConsumer(tailConsumers, parameter.Ordinal);

            if (botClientType is not null && SymbolEqualityComparer.Default.Equals(parameter.Type, botClientType))
            {
                source = ParameterSource.BotClient;
            }
            else if (cancellationTokenType is not null && SymbolEqualityComparer.Default.Equals(parameter.Type, cancellationTokenType))
            {
                source = ParameterSource.CancellationToken;
            }
            else if (SymbolEqualityComparer.Default.Equals(parameter.Type, updateClass))
            {
                source = ParameterSource.Update;
                acceptsRawUpdate = true;
            }
            else if (payloadType is not null && ((Microsoft.CodeAnalysis.CSharp.CSharpCompilation)compilation).ClassifyConversion(payloadType, parameter.Type).IsImplicit)
            {
                source = ParameterSource.Payload;
            }
            else if (argIndex >= 0)
            {
                source = ParameterSource.Arg;
            }
            else if (tailIndex >= 0)
            {
                source = tailConsumers[tailIndex].Pattern is null ? ParameterSource.Rest : ParameterSource.Parse;
            }
            else if (stateMachineType is not null && SymbolEqualityComparer.Default.Equals(parameter.Type, stateMachineType))
            {
                source = ParameterSource.StateMachine;
            }
            else if (stateStorageType is not null && SymbolEqualityComparer.Default.Equals(parameter.Type, stateStorageType))
            {
                source = ParameterSource.StateStorage;
            }
            else if (updateAwaiterType is not null && SymbolEqualityComparer.Default.Equals(parameter.Type, updateAwaiterType))
            {
                source = ParameterSource.Awaiter;
            }
            else if (keyAttribute is not null)
            {
                AttributeData? keyData = parameter.GetAttributes().FirstOrDefault(a =>
                    a.AttributeClass is not null && InheritsFrom(a.AttributeClass, keyAttribute));
                keyLiteral = TryGetKeyLiteral(keyData);
                if (keyLiteral is not null)
                {
                    source = ParameterSource.KeyedService;
                }
            }

            parameters.Add(new ParameterModel
            {
                TypeFqn = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                Source = source,
                KeyLiteral = keyLiteral,
                ArgIndex = argIndex >= 0 ? argIndex : tailIndex >= 0 ? args.Count + tailIndex : -1,
            });
        }

        if (isRawUpdateHandler && !parameters.Any(p => p.Source == ParameterSource.Update))
        {
            Diagnostic diagnostic = Diagnostic.Create(
                PolyBotDiagnostics.UpdateHandlerRequiresUpdateParameter,
                handlerAttributeLocation ?? methodSymbol.Locations.FirstOrDefault(),
                methodSymbol.Name);
            return HandlerItem.ForModel(null, new List<Diagnostic> { diagnostic });
        }

        // Inline-await extraction: await chains rooted at an IUpdateAwaiter parameter that
        // call WaitForAsync/WaitForXxxAsync become implicit branches; custom wrapper
        // methods are intentionally not followed (CUR013 warns about them).
        List<UpdateShape.FilterGroup> awaitGroups = updateAwaiterType is not null && updateClass is not null && updateTypeEnum is not null
            ? UpdateShape.BuildGroups(updateClass, updateTypeEnum)
            : new List<UpdateShape.FilterGroup>();
        List<AwaitSiteModel> awaitSites = ExtractAwaitSites(methodSyntax, semanticModel, updateAwaiterType, awaitGroups, filterClasses, diagnostics);
        bool takesAwaiter = false;
        foreach (ParameterModel parameterModel in parameters)
        {
            if (parameterModel.Source == ParameterSource.Awaiter)
            {
                takesAwaiter = true;
                break;
            }
        }

        if (takesAwaiter && awaitSites.Count == 0)
        {
            diagnostics.Add(Diagnostic.Create(
                PolyBotDiagnostics.AwaiterWithoutAwaitSite,
                methodSymbol.Locations.FirstOrDefault(),
                methodSymbol.Name));
        }

        bool returnsValueTask =
            SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType.OriginalDefinition, valueTaskType) ||
            SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType.OriginalDefinition, valueTaskNonGenericType);

        PatternModel? routePattern = null;
        if (patternData is not null)
        {
            routePattern = BuildPattern(patternData, methodSymbol, payloadType, callbackQueryType, args, diagnostics);
        }

        ThrottleModel? throttle = null;
        if (throttleAttribute is not null)
        {
            AttributeData? throttleData = methodSymbol.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass is not null && SymbolEqualityComparer.Default.Equals(a.AttributeClass, throttleAttribute));
            if (throttleData is not null)
            {
                throttle = ReadThrottle(throttleData, methodSymbol, diagnostics);
            }
        }

        if (isRawUpdateHandler && (commandData is not null || routePattern is not null || stateConditions.Count > 0))
        {
            Diagnostic diagnostic = Diagnostic.Create(
                PolyBotDiagnostics.UnsupportedUpdateHandlerGuard,
                handlerAttributeLocation ?? methodSymbol.Locations.FirstOrDefault(),
                methodSymbol.Name);
            return HandlerItem.ForModel(null, new List<Diagnostic> { diagnostic });
        }

        HandlerModel model = new()
        {
            ContainingTypeFqn = methodSymbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            MethodName = methodSymbol.Name,
            IsStatic = methodSymbol.IsStatic,
            UpdateTypeMemberName = updateTypeMemberName,
            UpdateTypeMemberNames = new EquatableArray<string>(updateTypeMemberNames.ToArray()),
            IsRawUpdateHandler = isRawUpdateHandler,
            UpdatePropertyName = updatePropertyName,
            PayloadTypeFqn = payloadType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "object",
            Priority = priority,
            Aliases = new EquatableArray<string>(aliases.ToArray()),
            Prefix = prefix,
            NoArgs = noArgs,
            CommandDescription = commandDescription,
            CommandLanguageCode = commandLanguageCode,
            CommandScope = commandScope,
            CommandHidden = commandHidden,
            Args = new EquatableArray<ArgModel>(args.ToArray()),
            TailConsumers = new EquatableArray<TailConsumerModel>(tailConsumers.ToArray()),
            StateConditions = new EquatableArray<StateConditionModel>(stateConditions.ToArray()),
            Filters = new EquatableArray<FilterModel>(filters.ToArray()),
            Parameters = new EquatableArray<ParameterModel>(parameters.ToArray()),
            ReturnKind = returnKind.Value,
            ReturnsValueTask = returnsValueTask,
            AwaitSites = new EquatableArray<AwaitSiteModel>(awaitSites.ToArray()),
            Pattern = routePattern,
            Throttle = throttle,
            AcceptsRawUpdate = acceptsRawUpdate,
            HandlerAttributeLocation = handlerAttributeLocation,
        };

        if (extraDiagnostic is not null)
        {
            diagnostics.Insert(0, extraDiagnostic);
        }

        return HandlerItem.ForModel(model, diagnostics);
    }

    private static int IndexOfArg(List<ArgModel> args, int parameterIndex)
    {
        for (int i = 0; i < args.Count; i++)
        {
            if (args[i].ParameterIndex == parameterIndex)
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOfTailConsumer(List<TailConsumerModel> tailConsumers, int parameterIndex)
    {
        for (int i = 0; i < tailConsumers.Count; i++)
        {
            if (tailConsumers[i].ParameterIndex == parameterIndex)
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOfStateCondition(
        List<StateConditionModel> stateConditions,
        string enumTypeFqn,
        string stateKeyName,
        bool isNoState)
    {
        for (int i = 0; i < stateConditions.Count; i++)
        {
            StateConditionModel condition = stateConditions[i];
            if (condition.EnumTypeFqn == enumTypeFqn &&
                condition.StateKeyName == stateKeyName &&
                condition.IsNoState == isNoState)
            {
                return i;
            }
        }

        return -1;
    }

    private const string MessageDtoFqn = "global::Telegram.Bot.Types.Message";

    private static List<AwaitSiteModel> ExtractAwaitSites(
        MethodDeclarationSyntax methodSyntax,
        SemanticModel semanticModel,
        INamedTypeSymbol? awaiterType,
        List<UpdateShape.FilterGroup> groups,
        EquatableArray<FilterClassModel> filterClasses,
        List<Diagnostic> diagnostics)
    {
        List<AwaitSiteModel> sites = new();
        if (awaiterType is null)
        {
            return sites;
        }

        foreach (AwaitExpressionSyntax awaitExpression in methodSyntax.DescendantNodes().OfType<AwaitExpressionSyntax>())
        {
            AwaitSiteModel? site = ExtractAwaitSite(awaitExpression.Expression, semanticModel, awaiterType, groups, filterClasses, diagnostics);
            if (site is not null && !sites.Contains(site))
            {
                sites.Add(site);
            }
        }

        return sites;
    }

    private static AwaitSiteModel? ExtractAwaitSite(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        INamedTypeSymbol awaiterType,
        List<UpdateShape.FilterGroup> groups,
        EquatableArray<FilterClassModel> filterClasses,
        List<Diagnostic> diagnostics)
    {
        string? memberName = null;
        string? explicitDtoFqn = null;
        List<string> patterns = new();
        List<string> filterFqns = new();
        List<AwaitConditionModel> conditions = new();
        AwaitKeyMode? keyMode = null;
        bool sawWaitFor = false;

        ExpressionSyntax current = expression;
        while (true)
        {
            if (current is InvocationExpressionSyntax invocation)
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                {
                    return null;
                }

                string name = memberAccess.Name.Identifier.ValueText;
                if (IsWaitForMethodName(name))
                {
                    sawWaitFor = true;
                    if (name == "WaitForAsync")
                    {
                        if (memberAccess.Name is GenericNameSyntax { TypeArgumentList.Arguments.Count: 1 } genericName &&
                            semanticModel.GetTypeInfo(genericName.TypeArgumentList.Arguments[0]).Type is ITypeSymbol typeArgument)
                        {
                            explicitDtoFqn = typeArgument.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        }
                    }
                    else
                    {
                        memberName = name.Substring("WaitFor".Length, name.Length - "WaitFor".Length - "Async".Length);
                    }

                    current = memberAccess.Expression;
                    continue;
                }

                if (name == "TextMatches")
                {
                    if (invocation.ArgumentList.Arguments.Count == 1 &&
                        semanticModel.GetConstantValue(invocation.ArgumentList.Arguments[0].Expression) is { HasValue: true, Value: string pattern })
                    {
                        patterns.Add(pattern);
                        conditions.Add(new AwaitConditionModel { Kind = AwaitConditionKind.TextPattern, Value = pattern });
                    }

                    current = memberAccess.Expression;
                    continue;
                }

                if (name == "Where")
                {
                    if (invocation.ArgumentList.Arguments.Count == 1 &&
                        invocation.ArgumentList.Arguments[0].Expression is LambdaExpressionSyntax lambda &&
                        TryValidateWhereLambda(lambda, semanticModel, diagnostics))
                    {
                        conditions.Add(new AwaitConditionModel { Kind = AwaitConditionKind.WhereLambda, Value = lambda.ToString() });
                    }

                    current = memberAccess.Expression;
                    continue;
                }

                if (name == "WithFilter")
                {
                    // Generic form WithFilter<TFilter>(): resolve the type argument. The
                    // instance overload (a filter variable) is not statically analyzable
                    // and is silently not enforced (documented in the builder remarks).
                    if (memberAccess.Name is GenericNameSyntax { TypeArgumentList.Arguments.Count: 1 } genericFilter &&
                        semanticModel.GetTypeInfo(genericFilter.TypeArgumentList.Arguments[0]).Type is ITypeSymbol filterType)
                    {
                        string filterFqn = filterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        filterFqns.Add(filterFqn);
                        conditions.Add(new AwaitConditionModel { Kind = AwaitConditionKind.Filter, Value = filterFqn });
                    }

                    current = memberAccess.Expression;
                    continue;
                }

                if (name.StartsWith("With", StringComparison.Ordinal))
                {
                    // With* marker: own-assembly extensions are error symbols in the same
                    // pass (stem match below); referenced libraries' extensions are
                    // compiled — resolve the filter semantically (WithFilter<TFilter>()
                    // via the type argument, With{Name}() by pairing with the filter
                    // type in the extension's assembly).
                    FilterClassModel? match = null;
                    string stem = name.Substring("With".Length);
                    foreach (FilterClassModel filterClass in filterClasses.Items)
                    {
                        if (filterClass.ShortName == stem)
                        {
                            match = filterClass;
                            break;
                        }
                    }

                    INamedTypeSymbol? libraryFilter = null;
                    IMethodSymbol? boundWithMethod = null;
                    if (match is null &&
                        semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol withMethod &&
                        !SymbolEqualityComparer.Default.Equals(withMethod.ContainingAssembly, semanticModel.Compilation.Assembly))
                    {
                        INamedTypeSymbol? updateFilterType = semanticModel.Compilation.GetTypeByMetadataName("PolyBot.Routing.IUpdateFilter");
                        if (withMethod is { IsGenericMethod: true, Name: "WithFilter" } &&
                            withMethod.TypeArguments.Length == 1 &&
                            withMethod.TypeArguments[0] is INamedTypeSymbol genericFilter)
                        {
                            libraryFilter = genericFilter;
                        }
                        else
                        {
                            boundWithMethod = withMethod;
                            libraryFilter = FindFilterInAssembly(withMethod.ContainingAssembly, stem, updateFilterType);
                        }
                    }

                    string? filterFqn = match?.TypeFqn ??
                        libraryFilter?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    if (filterFqn is null)
                    {
                        diagnostics.Add(Diagnostic.Create(
                            PolyBotDiagnostics.UnknownFilterInAwaitChain,
                            invocation.GetLocation(),
                            stem));
                    }
                    else
                    {
                        EquatableArray<FilterCtorModel> ctors = match is not null
                            ? match.Ctors
                            : FilterCtorInfo.ReadMirroredCtors(libraryFilter!);
                        string? args = CaptureWithFilterArgs(filterFqn, ctors, boundWithMethod, invocation, semanticModel, diagnostics);
                        if (args is not null || invocation.ArgumentList.Arguments.Count == 0)
                        {
                            string composite = args is null ? filterFqn : filterFqn + "|" + args;
                            filterFqns.Add(composite);
                            conditions.Add(new AwaitConditionModel { Kind = AwaitConditionKind.Filter, Value = composite });
                        }
                    }

                    current = memberAccess.Expression;
                    continue;
                }

                if (keyMode is null)
                {
                    keyMode = name switch
                    {
                        "ByUserId" => AwaitKeyMode.UserId,
                        "ByChatId" => AwaitKeyMode.ChatId,
                        "ByUserInChat" => AwaitKeyMode.UserInChat,
                        _ => null,
                    };
                }

                current = memberAccess.Expression;
                continue;
            }

            if (current is MemberAccessExpressionSyntax access)
            {
                current = access.Expression;
                continue;
            }

            if (current is IdentifierNameSyntax identifier)
            {
                bool rooted = sawWaitFor &&
                    semanticModel.GetSymbolInfo(identifier).Symbol is IParameterSymbol parameter &&
                    SymbolEqualityComparer.Default.Equals(parameter.Type, awaiterType);
                if (!rooted)
                {
                    return null;
                }

                break;
            }

            return null;
        }

        // The chain walk visits the outermost call first; the builder records markers in
        // source order (innermost first), so reverse to keep both sides consistent for
        // the engine's order-sensitive filter-type check and the branch's source order.
        patterns.Reverse();
        filterFqns.Reverse();
        conditions.Reverse();

        UpdateShape.FilterGroup? group = ResolveAwaitGroup(memberName, explicitDtoFqn, groups);
        if (group is null || keyMode is null)
        {
            return null;
        }

        string dtoFqn = group.DtoType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (patterns.Count > 0 && dtoFqn != MessageDtoFqn)
        {
            diagnostics.Add(Diagnostic.Create(
                PolyBotDiagnostics.TextMatchesOnNonMessageAwait,
                expression.GetLocation(),
                memberName ?? dtoFqn));
            return null;
        }

        List<UpdateTypeMapping> members = new();
        foreach ((string mappingMember, string mappingProperty) in group.Mappings)
        {
            members.Add(new UpdateTypeMapping(mappingMember, mappingProperty));
        }

        return new AwaitSiteModel
        {
            DtoTypeFqn = dtoFqn,
            TextPatterns = new EquatableArray<string>(patterns.ToArray()),
            Filters = new EquatableArray<string>(filterFqns.ToArray()),
            Conditions = new EquatableArray<AwaitConditionModel>(conditions.ToArray()),
            KeyMode = keyMode.Value,
            Members = new EquatableArray<UpdateTypeMapping>(members.ToArray()),
        };
    }

    /// <summary>
    /// Finds a concrete <see cref="PolyBot.Routing.IUpdateFilter"/> implementation named
    /// <paramref name="shortName"/> in <paramref name="assembly"/> — the pairing used to
    /// resolve wrapper attributes and <c>With*</c> extensions that come from referenced
    /// filter libraries without scanning referenced assemblies up front.
    /// </summary>
    private static INamedTypeSymbol? FindFilterInAssembly(IAssemblySymbol assembly, string shortName, INamedTypeSymbol? filterInterface)
    {
        if (filterInterface is null)
        {
            return null;
        }

        return FindFilterInContainer(assembly.GlobalNamespace, shortName, filterInterface);
    }

    private static INamedTypeSymbol? FindFilterInContainer(INamespaceOrTypeSymbol container, string shortName, INamedTypeSymbol filterInterface)
    {
        foreach (ISymbol member in container.GetMembers())
        {
            if (member is INamespaceSymbol namespaceSymbol)
            {
                INamedTypeSymbol? found = FindFilterInContainer(namespaceSymbol, shortName, filterInterface);
                if (found is not null)
                {
                    return found;
                }

                continue;
            }

            if (member is INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false, Name: not null } typeSymbol &&
                string.Equals(typeSymbol.Name, shortName, StringComparison.Ordinal) &&
                typeSymbol.AllInterfaces.Any(interfaceSymbol => SymbolEqualityComparer.Default.Equals(interfaceSymbol, filterInterface)))
            {
                return typeSymbol;
            }
        }

        return null;
    }

    private static bool TryValidateWhereLambda(LambdaExpressionSyntax lambda, SemanticModel semanticModel, List<Diagnostic> diagnostics)
    {
        string reason;
        if (!lambda.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            reason = "the lambda must be static";
        }
        else if (lambda.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword))
        {
            reason = "the lambda must not be async";
        }
        else if (lambda.ExpressionBody is null)
        {
            reason = "the lambda must be expression-bodied";
        }
        else
        {
            ParameterSyntax? parameterSyntax = null;
            int parameterCount = 0;
            if (lambda is SimpleLambdaExpressionSyntax simpleLambda)
            {
                parameterSyntax = simpleLambda.Parameter;
                parameterCount = 1;
            }
            else if (lambda is ParenthesizedLambdaExpressionSyntax parenthesizedLambda)
            {
                parameterCount = parenthesizedLambda.ParameterList.Parameters.Count;
                parameterSyntax = parameterCount == 1 ? parenthesizedLambda.ParameterList.Parameters[0] : null;
            }

            if (parameterCount != 1 || parameterSyntax is null)
            {
                reason = "the lambda must have exactly one parameter";
            }
            else
            {
                IParameterSymbol? parameter = semanticModel.GetDeclaredSymbol(parameterSyntax);
                if (parameter is not null && WhereBodyReferencesOnlyParameter(lambda.ExpressionBody, parameter, semanticModel))
                {
                    return true;
                }

                reason = "the body must reference only the parameter (members rooted at it) and literals";
            }
        }

        diagnostics.Add(Diagnostic.Create(
            PolyBotDiagnostics.UnsupportedWherePredicate,
            lambda.GetLocation(),
            lambda.ToString(),
            reason));
        return false;
    }

    private static bool WhereBodyReferencesOnlyParameter(ExpressionSyntax body, IParameterSymbol parameter, SemanticModel semanticModel)
    {
        foreach (SyntaxNode node in body.DescendantNodesAndSelf())
        {
            if (node is not IdentifierNameSyntax and not GenericNameSyntax)
            {
                continue;
            }

            if (!IsParameterRootedName(node, parameter, semanticModel))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsParameterRootedName(SyntaxNode nameNode, IParameterSymbol parameter, SemanticModel semanticModel)
    {
        // The name part of a member access rooted at the parameter (m.Text, m.Text.Length).
        if (nameNode.Parent is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name == nameNode &&
            IsRootedAtParameter(memberAccess.Expression, parameter, semanticModel))
        {
            // Extension methods do not bind in the generated file (no usings): reject
            // them even though syntactically they look like member accesses.
            if (semanticModel.GetSymbolInfo(nameNode).Symbol is IMethodSymbol { ReducedFrom: not null })
            {
                return false;
            }

            return true;
        }

        // A plain reference to the parameter itself (anything else — types, statics,
        // unresolvable symbols — is rejected).
        return SymbolEqualityComparer.Default.Equals(semanticModel.GetSymbolInfo(nameNode).Symbol, parameter);
    }

    private static bool IsRootedAtParameter(ExpressionSyntax expression, IParameterSymbol parameter, SemanticModel semanticModel)
    {
        ExpressionSyntax current = expression;
        while (true)
        {
            if (current is IdentifierNameSyntax identifier)
            {
                return SymbolEqualityComparer.Default.Equals(semanticModel.GetSymbolInfo(identifier).Symbol, parameter);
            }

            if (current is MemberAccessExpressionSyntax memberAccess)
            {
                current = memberAccess.Expression;
                continue;
            }

            if (current is InvocationExpressionSyntax invocation)
            {
                // m.Foo().Bar — continue through the invocation's member access.
                if (invocation.Expression is MemberAccessExpressionSyntax invocationAccess)
                {
                    current = invocationAccess;
                    continue;
                }

                return false;
            }

            if (current is ConditionalAccessExpressionSyntax conditional)
            {
                current = conditional.Expression;
                continue;
            }

            if (current is ParenthesizedExpressionSyntax parenthesized)
            {
                current = parenthesized.Expression;
                continue;
            }

            if (current is BinaryExpressionSyntax binary)
            {
                // e.g. m.Text ?? m.Caption: both sides must be parameter-rooted.
                return IsRootedAtParameter(binary.Left, parameter, semanticModel) &&
                       IsRootedAtParameter(binary.Right, parameter, semanticModel);
            }

            if (current is PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } suppressNullable)
            {
                current = suppressNullable.Operand;
                continue;
            }

            return false;
        }
    }

    private static UpdateShape.FilterGroup? ResolveAwaitGroup(
        string? memberName,
        string? explicitDtoFqn,
        List<UpdateShape.FilterGroup> groups)
    {
        foreach (UpdateShape.FilterGroup candidate in groups)
        {
            if (explicitDtoFqn is not null)
            {
                if (candidate.DtoType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == explicitDtoFqn)
                {
                    return candidate;
                }
            }
            else if (memberName is not null)
            {
                foreach ((string candidateMember, _) in candidate.Mappings)
                {
                    if (candidateMember == memberName)
                    {
                        return candidate;
                    }
                }
            }
        }

        return null;
    }

    private static bool IsWaitForMethodName(string name)
    {
        return name == "WaitForAsync" ||
            (name.Length > "WaitForAsync".Length &&
             name.StartsWith("WaitFor", StringComparison.Ordinal) &&
             name.EndsWith("Async", StringComparison.Ordinal));
    }

    private static ITypeSymbol UnwrapNullable(ITypeSymbol parameterType, INamedTypeSymbol? nullableType)
    {
        if (nullableType is not null &&
            parameterType is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } nullableParameter &&
            SymbolEqualityComparer.Default.Equals(nullableParameter.OriginalDefinition, nullableType))
        {
            return nullableParameter.TypeArguments[0];
        }

        return parameterType;
    }

    private static char ReadCommandPrefix(AttributeData commandData, IMethodSymbol methodSymbol, List<Diagnostic> diagnostics)
    {
        TypedConstant? prefixArgument = GetNamedArgument(commandData, "Prefix");
        if (prefixArgument is null)
        {
            return '/';
        }

        object? prefixValue = prefixArgument.Value.Value;
        if (prefixValue is char charValue)
        {
            return charValue;
        }

        if (prefixValue is string stringValue && stringValue.Length > 0)
        {
            return stringValue[0];
        }

        diagnostics.Add(Diagnostic.Create(
            PolyBotDiagnostics.NonConstantCommandPrefix,
            methodSymbol.Locations.FirstOrDefault(),
            methodSymbol.Name));
        return '/';
    }

    private static readonly string[] CommandScopeNames = { "Default", "AllPrivateChats", "AllGroupChats", "AllChatAdministrators" };

    private static readonly string[] ThrottleScopeNames = { "User", "Chat", "UserInChat", "Global" };

    private static readonly string[] ThrottleActionNames = { "Ignore", "Fallthrough" };

    private static ThrottleModel ReadThrottle(AttributeData throttleData, IMethodSymbol methodSymbol, List<Diagnostic> diagnostics)
    {
        Location location = methodSymbol.Locations.FirstOrDefault() ?? Location.None;

        int limit = ReadThrottleInt(throttleData, "Limit", 1, methodSymbol, diagnostics);
        int period = ReadThrottleInt(throttleData, "PeriodMilliseconds", 1000, methodSymbol, diagnostics);
        string scope = ReadThrottleEnum(throttleData, "Scope", ThrottleScopeNames, "User", methodSymbol, diagnostics);
        string action = ReadThrottleEnum(throttleData, "Action", ThrottleActionNames, "Ignore", methodSymbol, diagnostics);

        return new ThrottleModel
        {
            Limit = limit,
            PeriodMilliseconds = period,
            ScopeName = scope,
            ActionName = action,
        };
    }

    private static int ReadThrottleInt(AttributeData throttleData, string name, int defaultValue, IMethodSymbol methodSymbol, List<Diagnostic> diagnostics)
    {
        TypedConstant? argument = GetNamedArgument(throttleData, name);
        if (argument is null)
        {
            return defaultValue;
        }

        if (argument is { Kind: TypedConstantKind.Primitive, Value: int value } && value >= 1)
        {
            return value;
        }

        diagnostics.Add(Diagnostic.Create(
            PolyBotDiagnostics.InvalidThrottleConfiguration,
            methodSymbol.Locations.FirstOrDefault(),
            methodSymbol.Name,
            name));
        return defaultValue;
    }

    private static string ReadThrottleEnum(AttributeData throttleData, string name, string[] names, string defaultValue, IMethodSymbol methodSymbol, List<Diagnostic> diagnostics)
    {
        TypedConstant? argument = GetNamedArgument(throttleData, name);
        if (argument is null)
        {
            return defaultValue;
        }

        if (argument is { Kind: TypedConstantKind.Enum, Value: int ordinal } &&
            ordinal >= 0 && ordinal < names.Length)
        {
            return names[ordinal];
        }

        diagnostics.Add(Diagnostic.Create(
            PolyBotDiagnostics.InvalidThrottleConfiguration,
            methodSymbol.Locations.FirstOrDefault(),
            methodSymbol.Name,
            name));
        return defaultValue;
    }

    private static PatternModel? BuildPattern(
        AttributeData patternData,
        IMethodSymbol methodSymbol,
        ITypeSymbol? payloadType,
        INamedTypeSymbol? callbackQueryType,
        List<ArgModel> args,
        List<Diagnostic> diagnostics)
    {
        Location location = methodSymbol.Locations.FirstOrDefault() ?? Location.None;
        if (callbackQueryType is null ||
            payloadType is null ||
            !SymbolEqualityComparer.Default.Equals(payloadType, callbackQueryType))
        {
            diagnostics.Add(Diagnostic.Create(
                PolyBotDiagnostics.PatternOnNonCallbackHandler,
                location,
                methodSymbol.Name));
            return null;
        }

        if (patternData.ConstructorArguments.Length != 1 ||
            patternData.ConstructorArguments[0].Value is not string template ||
            template.Length == 0)
        {
            diagnostics.Add(Diagnostic.Create(
                PolyBotDiagnostics.MalformedCallbackPattern,
                location,
                "<empty>",
                "the template must be a non-empty constant string"));
            return null;
        }

        char separator = GetNamedArgument(patternData, "Separator") is { Kind: TypedConstantKind.Primitive, Value: char sep }
            ? sep
            : ':';

        if (!TryParseTemplate(template, separator, location, diagnostics, out List<PatternSegmentModel> segments))
        {
            return null;
        }

        int wildcardIndex = -1;
        int skeletonBytes = 0;
        int minCaptureBytes = 0;
        for (int i = 0; i < segments.Count; i++)
        {
            PatternSegmentModel segment = segments[i];
            if (segment.IsLiteral)
            {
                skeletonBytes += System.Text.Encoding.UTF8.GetByteCount(segment.Text);
                if (i > 0)
                {
                    skeletonBytes += 1;
                }

                continue;
            }

            if (i > 0)
            {
                skeletonBytes += 1;
            }

            if (segment.IsWildcard)
            {
                if (i != segments.Count - 1)
                {
                    diagnostics.Add(Diagnostic.Create(
                        PolyBotDiagnostics.MalformedCallbackPattern,
                        location,
                        template,
                        "a wildcard {*name} must be the last segment"));
                    return null;
                }

                wildcardIndex = i;
                continue;
            }

            minCaptureBytes += 1;
        }

        if (skeletonBytes > CallbackMaxBytes)
        {
            diagnostics.Add(Diagnostic.Create(
                PolyBotDiagnostics.ImpossibleCallbackPattern,
                location,
                template,
                skeletonBytes));
            return null;
        }

        if (skeletonBytes + minCaptureBytes > CallbackMaxBytes)
        {
            diagnostics.Add(Diagnostic.Create(
                PolyBotDiagnostics.CallbackPatternOverflow,
                location,
                template,
                skeletonBytes + minCaptureBytes));
            return null;
        }

        if (!BindPatternCaptures(patternData, template, segments, args, methodSymbol, location, diagnostics))
        {
            return null;
        }

        return new PatternModel
        {
            Separator = separator,
            Segments = new EquatableArray<PatternSegmentModel>(segments.ToArray()),
            WildcardIndex = wildcardIndex,
        };
    }

    private const int CallbackMaxBytes = 64;

    private static bool TryParseTemplate(
        string template,
        char separator,
        Location location,
        List<Diagnostic> diagnostics,
        out List<PatternSegmentModel> segments)
    {
        segments = new List<PatternSegmentModel>();
        List<string> parts = SplitTemplate(template, separator);
        for (int i = 0; i < parts.Count; i++)
        {
            string part = parts[i];
            if (part.Length == 0)
            {
                diagnostics.Add(Diagnostic.Create(
                    PolyBotDiagnostics.MalformedCallbackPattern,
                    location,
                    template,
                    "empty segments are not allowed"));
                return false;
            }
            if (part[0] == '{')
            {
                if (part.Length < 3 || part[part.Length - 1] != '}')
                {
                    diagnostics.Add(Diagnostic.Create(
                        PolyBotDiagnostics.MalformedCallbackPattern,
                        location,
                        template,
                        $"segment '{part}' has unbalanced braces"));
                    return false;
                }

                string inner = part.Substring(1, part.Length - 2);
                if (inner[0] == '*')
                {
                    string name = inner.Substring(1);
                    if (name.Length == 0)
                    {
                        diagnostics.Add(Diagnostic.Create(
                            PolyBotDiagnostics.MalformedCallbackPattern,
                            location,
                            template,
                            "a wildcard needs a name"));
                        return false;
                    }

                    segments.Add(new PatternSegmentModel
                    {
                        IsLiteral = false,
                        IsWildcard = true,
                        Text = name,
                        Constraint = null,
                        SpecTypeFqn = null,
                        ParseKind = PatternParseKind.String,
                    });
                }
                else
                {
                    int colon = inner.IndexOf(':');
                    string name = colon < 0 ? inner : inner.Substring(0, colon);
                    string? constraint = colon < 0 ? null : inner.Substring(colon + 1);
                    if (name.Length == 0 || (constraint is not null && constraint.Length == 0))
                    {
                        diagnostics.Add(Diagnostic.Create(
                            PolyBotDiagnostics.MalformedCallbackPattern,
                            location,
                            template,
                            $"segment '{part}' has an empty placeholder or constraint"));
                        return false;
                    }

                    segments.Add(new PatternSegmentModel
                    {
                        IsLiteral = false,
                        IsWildcard = false,
                        Text = name,
                        Constraint = constraint,
                        SpecTypeFqn = null,
                        ParseKind = PatternParseKind.String,
                    });
                }
            }
            else
            {
                if (part.IndexOf('}') >= 0)
                {
                    diagnostics.Add(Diagnostic.Create(
                        PolyBotDiagnostics.MalformedCallbackPattern,
                        location,
                        template,
                        $"literal segment '{part}' contains a brace"));
                    return false;
                }

                segments.Add(new PatternSegmentModel
                {
                    IsLiteral = true,
                    IsWildcard = false,
                    Text = part,
                    Constraint = null,
                    SpecTypeFqn = null,
                    ParseKind = PatternParseKind.String,
                });
            }
        }

        return true;
    }

    /// <summary>
    /// Splits on separators outside <c>{...}</c> groups.
    /// </summary>
    private static List<string> SplitTemplate(string template, char separator)
    {
        List<string> parts = new();
        int start = 0;
        int depth = 0;
        for (int i = 0; i < template.Length; i++)
        {
            char character = template[i];
            if (character == '{')
            {
                depth++;
            }
            else if (character == '}')
            {
                depth = depth > 0 ? depth - 1 : 0;
            }
            else if (character == separator && depth == 0)
            {
                parts.Add(template.Substring(start, i - start));
                start = i + 1;
            }
        }

        parts.Add(template.Substring(start));
        return parts;
    }

    private static bool BindPatternCaptures(
        AttributeData patternData,
        string template,
        List<PatternSegmentModel> segments,
        List<ArgModel> args,
        IMethodSymbol methodSymbol,
        Location location,
        List<Diagnostic> diagnostics)
    {
        bool valid = true;
        HashSet<string> captured = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < segments.Count; i++)
        {
            PatternSegmentModel segment = segments[i];
            if (segment.IsLiteral)
            {
                continue;
            }

            ArgModel? match = null;
            foreach (ArgModel arg in args)
            {
                if (string.Equals(arg.Name, segment.Text, StringComparison.OrdinalIgnoreCase))
                {
                    match = arg;
                    break;
                }
            }

            if (match is null)
            {
                diagnostics.Add(Diagnostic.Create(
                    PolyBotDiagnostics.UnboundCallbackPlaceholder,
                    location,
                    segment.Text,
                    template));
                valid = false;
                continue;
            }

            captured.Add(segment.Text);

            ITypeSymbol parameterType = methodSymbol.Parameters[match.ParameterIndex].Type;
            PatternParseKind parseKind = ResolveParseKind(match, segment, parameterType, template, location, diagnostics);
            segments[i] = new PatternSegmentModel
            {
                IsLiteral = false,
                IsWildcard = segment.IsWildcard,
                Text = segment.Text,
                Constraint = segment.Constraint,
                SpecTypeFqn = match.SpecTypeFqn,
                ParseKind = parseKind,
            };
        }

        foreach (ArgModel arg in args)
        {
            if (!captured.Contains(arg.Name))
            {
                diagnostics.Add(Diagnostic.Create(
                    PolyBotDiagnostics.ArgMissingFromCallbackPattern,
                    location,
                    arg.Name,
                    methodSymbol.Name,
                    template));
                valid = false;
            }
        }

        return valid;
    }

    private static string? ConstraintAlias(ITypeSymbol type)
    {
        return type.SpecialType switch
        {
            SpecialType.System_Int32 => "int",
            SpecialType.System_Int64 => "long",
            SpecialType.System_Int16 => "short",
            SpecialType.System_Byte => "byte",
            SpecialType.System_Boolean => "bool",
            SpecialType.System_Double => "double",
            SpecialType.System_Single => "float",
            SpecialType.System_Decimal => "decimal",
            SpecialType.System_String => "string",
            _ => type.ToDisplayString() switch
            {
                "System.Guid" => "guid",
                "System.DateTime" => "datetime",
                _ => null,
            },
        };
    }

    private static PatternParseKind ResolveParseKind(
        ArgModel arg,
        PatternSegmentModel segment,
        ITypeSymbol parameterType,
        string template,
        Location location,
        List<Diagnostic> diagnostics)
    {
        ITypeSymbol specType = parameterType;
        if (parameterType is INamedTypeSymbol { IsGenericType: true } named &&
            named.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T &&
            named.TypeArguments.Length == 1)
        {
            specType = named.TypeArguments[0];
        }

        if (segment.Constraint is not null)
        {
            string? actualAlias = ConstraintAlias(specType);
            if (actualAlias is null || !string.Equals(actualAlias, segment.Constraint, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(Diagnostic.Create(
                    PolyBotDiagnostics.CallbackConstraintMismatch,
                    location,
                    segment.Text,
                    segment.Constraint,
                    template,
                    arg.SpecTypeFqn));
                return PatternParseKind.Failed;
            }
        }

        if (segment.IsWildcard)
        {
            if (specType.SpecialType != SpecialType.System_String)
            {
                diagnostics.Add(Diagnostic.Create(
                    PolyBotDiagnostics.CallbackConstraintMismatch,
                    location,
                    segment.Text,
                    "string",
                    template,
                    arg.SpecTypeFqn));
                return PatternParseKind.Failed;
            }

            return PatternParseKind.String;
        }

        if (specType.SpecialType == SpecialType.System_String)
        {
            return PatternParseKind.String;
        }

        if (specType.TypeKind == TypeKind.Enum)
        {
            return PatternParseKind.Enum;
        }

        if (IsNumeric(specType.SpecialType))
        {
            // BCL numerics guarantee the (string, NumberStyles, IFormatProvider, out T)
            // overload; capture parsing is culture-invariant so data means the same in
            // every locale.
            return PatternParseKind.TryParseInvariant;
        }

        if (HasAccessibleTryParse(specType))
        {
            return PatternParseKind.TryParse;
        }

        diagnostics.Add(Diagnostic.Create(
            PolyBotDiagnostics.MissingTryParse,
            location,
            arg.SpecTypeFqn,
            arg.Name));
        return PatternParseKind.Failed;
    }

    private static bool IsNumeric(SpecialType specialType)
    {
        return specialType is SpecialType.System_Byte or SpecialType.System_SByte or
            SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or
            SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or
            SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal;
    }

    private static bool HasAccessibleTryParse(ITypeSymbol type)
    {
        foreach (ISymbol member in type.GetMembers("TryParse"))
        {
            if (member is not IMethodSymbol { IsStatic: true, Parameters.Length: 2 } method ||
                method.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal) ||
                method.Parameters[1].RefKind != RefKind.Out ||
                !SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, type))
            {
                continue;
            }

            ITypeSymbol first = method.Parameters[0].Type;
            if (first.SpecialType == SpecialType.System_String)
            {
                return true;
            }

            if (first is INamedTypeSymbol { Name: "ReadOnlySpan", ContainingNamespace: not null } span &&
                span.ContainingNamespace.ToDisplayString() == "System")
            {
                return true;
            }
        }

        return false;
    }

    private static void ValidateBotFatherMetadata(IMethodSymbol methodSymbol, List<string> aliases, string? description, bool isHidden, List<Diagnostic> diagnostics)
    {
        foreach (string alias in aliases)
        {
            if (!IsValidBotFatherAlias(alias))
            {
                diagnostics.Add(Diagnostic.Create(
                    PolyBotDiagnostics.InvalidBotFatherCommand,
                    methodSymbol.Locations.FirstOrDefault(),
                    alias,
                    methodSymbol.Name));
            }
        }

        if (isHidden)
        {
            return;
        }

        if (description is null || description.Length == 0)
        {
            diagnostics.Add(Diagnostic.Create(
                PolyBotDiagnostics.MissingCommandDescription,
                methodSymbol.Locations.FirstOrDefault(),
                aliases[0]));
        }
        else if (description.Length > 256)
        {
            diagnostics.Add(Diagnostic.Create(
                PolyBotDiagnostics.CommandDescriptionTooLong,
                methodSymbol.Locations.FirstOrDefault(),
                aliases[0],
                description.Length));
        }
    }

    private static bool IsValidBotFatherAlias(string alias)
    {
        if (alias.Length is < 1 or > 32)
        {
            return false;
        }

        foreach (char character in alias)
        {
            bool ok = character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_';
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsCompatibleArgType(ITypeSymbol parameterType, ITypeSymbol specType, INamedTypeSymbol? nullableType)
    {
        if (SymbolEqualityComparer.Default.Equals(parameterType, specType))
        {
            return true;
        }

        if (parameterType.SpecialType == SpecialType.System_Object)
        {
            return true;
        }

        return nullableType is not null &&
               parameterType is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } nullableParameter &&
               SymbolEqualityComparer.Default.Equals(nullableParameter.OriginalDefinition, nullableType) &&
               SymbolEqualityComparer.Default.Equals(nullableParameter.TypeArguments[0], specType);
    }

    private static bool IsHandlerAttribute(INamedTypeSymbol attributeClass, INamedTypeSymbol handlerBase, INamedTypeSymbol updateTypeEnum)
    {
        // The stem is the attribute name without the "Attribute" suffix. Note that error-type
        // symbols keep the source-spelled name ("MessageHandler"), while bound symbols carry
        // the real class name ("MessageHandlerAttribute").
        string stem;
        if (attributeClass.Name.EndsWith(HandlerAttributeSuffix, StringComparison.Ordinal))
        {
            stem = attributeClass.Name.Substring(0, attributeClass.Name.Length - HandlerAttributeSuffix.Length);
        }
        else if (attributeClass.Name.EndsWith("Handler", StringComparison.Ordinal))
        {
            stem = attributeClass.Name.Substring(0, attributeClass.Name.Length - "Handler".Length);
        }
        else
        {
            return false;
        }

        if (attributeClass.TypeKind != TypeKind.Error)
        {
            return InheritsFrom(attributeClass, handlerBase) &&
                   !SymbolEqualityComparer.Default.Equals(attributeClass, handlerBase);
        }

        // Error symbol: the attribute class is produced by this generator in the same
        // generation pass. Accept it when the stem matches an UpdateType member name.
        if (stem.Length == 0)
        {
            return false;
        }

        INamespaceSymbol? ns = attributeClass.ContainingNamespace;
        if (ns is not null && !ns.IsGlobalNamespace &&
            ns.ToDisplayString() is { Length: > 0 } nsName &&
            nsName != "PolyBot.Attributes" && nsName != "PolyBot")
        {
            return false;
        }

        return FindEnumMember(updateTypeEnum, stem) is not null;
    }

    private static bool TryResolveUpdateType(
        AttributeData handlerAttributeData,
        INamedTypeSymbol? descriptorAttribute,
        INamedTypeSymbol updateTypeEnum,
        INamedTypeSymbol updateClass,
        string handlerAttributeClassName,
        out string updateTypeMemberName,
        out string updatePropertyName)
    {
        // Preferred: read the UpdateHandlerDescriptorAttribute stamped on the attribute class.
        if (descriptorAttribute is not null && handlerAttributeData.AttributeClass is { } attributeClass)
        {
            foreach (AttributeData classAttribute in attributeClass.GetAttributes())
            {
                if (classAttribute.AttributeClass is null ||
                    !SymbolEqualityComparer.Default.Equals(classAttribute.AttributeClass, descriptorAttribute))
                {
                    continue;
                }

                if (classAttribute.ConstructorArguments.Length >= 2 &&
                    classAttribute.ConstructorArguments[0].Type is { } enumType &&
                    SymbolEqualityComparer.Default.Equals(enumType, updateTypeEnum) &&
                    classAttribute.ConstructorArguments[1].Value is string propertyName)
                {
                    string? memberName = EnumValueToMemberName(updateTypeEnum, classAttribute.ConstructorArguments[0].Value);
                    if (memberName is not null)
                    {
                        updateTypeMemberName = memberName;
                        updatePropertyName = propertyName;
                        return true;
                    }
                }
            }
        }

        // Fallback: naming convention, e.g. "MessageHandler(Attribute)" -> UpdateType.Message.
        string name = handlerAttributeClassName;
        string stem = name.EndsWith(HandlerAttributeSuffix, StringComparison.Ordinal)
            ? name.Substring(0, name.Length - HandlerAttributeSuffix.Length)
            : name.EndsWith("Handler", StringComparison.Ordinal)
                ? name.Substring(0, name.Length - "Handler".Length)
                : name;
        IFieldSymbol? member = FindEnumMember(updateTypeEnum, stem);
        if (member is null)
        {
            updateTypeMemberName = string.Empty;
            updatePropertyName = string.Empty;
            return false;
        }

        updateTypeMemberName = member.Name;
        updatePropertyName = UpdateShape.FindUpdatePropertyName(updateClass, member.Name);
        return true;
    }

    private static HandlerReturnKind? ClassifyReturnType(
        ITypeSymbol returnType,
        INamedTypeSymbol? taskType,
        INamedTypeSymbol? taskOfTType,
        INamedTypeSymbol? valueTaskType,
        INamedTypeSymbol? valueTaskNonGenericType,
        IAssemblySymbol curatorAssembly)
    {
        INamedTypeSymbol? resultType = curatorAssembly.GetTypeByMetadataName("PolyBot.Routing.Result");
        if (resultType is null)
        {
            return null;
        }

        if (returnType is INamedTypeSymbol named)
        {
            if (taskOfTType is not null && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, taskOfTType) &&
                named.TypeArguments.Length == 1 && SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], resultType))
            {
                return HandlerReturnKind.Result;
            }

            if (valueTaskType is not null && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, valueTaskType) &&
                named.TypeArguments.Length == 1 && SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], resultType))
            {
                return HandlerReturnKind.Result;
            }

            if ((taskType is not null && SymbolEqualityComparer.Default.Equals(named, taskType)) ||
                (valueTaskNonGenericType is not null && SymbolEqualityComparer.Default.Equals(named, valueTaskNonGenericType)))
            {
                return HandlerReturnKind.Plain;
            }
        }

        return null;
    }

    private static TypedConstant? GetNamedArgument(AttributeData attributeData, string name)
    {
        foreach (KeyValuePair<string, TypedConstant> argument in attributeData.NamedArguments)
        {
            if (argument.Key == name)
            {
                return argument.Value;
            }
        }

        return null;
    }

    private static int GetPriorityFromSyntax(SemanticModel semanticModel, MethodDeclarationSyntax methodSyntax, string handlerAttributeClassName)
    {
        string shortName = handlerAttributeClassName.TrimSuffix("Attribute");
        foreach (AttributeListSyntax attributeList in methodSyntax.AttributeLists)
        {
            foreach (AttributeSyntax attribute in attributeList.Attributes)
            {
                string lastSegment = attribute.Name.ToString().Split('.').Last();
                if (!string.Equals(lastSegment, handlerAttributeClassName, StringComparison.Ordinal) &&
                    !string.Equals(lastSegment, shortName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (attribute.ArgumentList is null)
                {
                    continue;
                }

                foreach (AttributeArgumentSyntax argument in attribute.ArgumentList.Arguments)
                {
                    if (argument.NameEquals is { } nameEquals &&
                        nameEquals.Name.Identifier.ValueText == "Priority" &&
                        semanticModel.GetConstantValue(argument.Expression) is { HasValue: true, Value: int priority })
                    {
                        return priority;
                    }
                }
            }
        }

        return 0;
    }

    private static IEnumerable<string> ReadStringArray(AttributeData attributeData, string propertyName)
    {
        TypedConstant? value = GetNamedArgument(attributeData, propertyName);
        if (value is { Kind: TypedConstantKind.Array } array)
        {
            foreach (TypedConstant element in array.Values)
            {
                if (element.Value is string s)
                {
                    yield return s;
                }
            }
        }
    }

    private static string? TryGetKeyLiteral(AttributeData? keyData)
    {
        if (keyData?.ConstructorArguments.Length != 1)
        {
            return null;
        }

        TypedConstant constant = keyData.ConstructorArguments[0];
        if (constant.Value is string s)
        {
            return BotRouterEmitter.EscapeString(s);
        }

        if (constant.Value is int i)
        {
            return i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return null;
    }

    private static IPropertySymbol? FindUpdateProperty(INamedTypeSymbol updateClass, string propertyName)
    {
        return updateClass.GetMembers(propertyName).OfType<IPropertySymbol>().FirstOrDefault();
    }

    /// <summary>
    /// Builds the filter model for a handler attribute usage, capturing the constructor
    /// arguments (as emitted literals) when the usage passes any. Returns <c>null</c> when a
    /// diagnostic was reported; the filter is then omitted from the handler.
    /// </summary>
    private static FilterModel? BuildFilterModel(
        string filterTypeFqn,
        EquatableArray<FilterCtorModel> ctors,
        AttributeData attributeData,
        SemanticModel semanticModel,
        List<Diagnostic> diagnostics)
    {
        string? ctorArgs = null;
        if (attributeData.ApplicationSyntaxReference?.GetSyntax() is AttributeSyntax { ArgumentList.Arguments.Count: > 0 } attributeSyntax)
        {
            ctorArgs = CaptureFilterCtorArgs(filterTypeFqn, ctors, attributeData, attributeSyntax, semanticModel, diagnostics);
            if (ctorArgs is null)
            {
                return null;
            }
        }

        return new FilterModel { TypeFqn = filterTypeFqn, CtorArgs = ctorArgs };
    }

    /// <summary>
    /// Captures the attribute's constructor arguments as an emitted <c>name: literal</c> list.
    /// Bound (library) usages identify the mirrored ctor via the compiler-selected wrapper ctor;
    /// own-assembly usages (error-symbol wrappers) select it structurally, preferring the
    /// candidate with the fewest parameters (optional-filling betterness rule).
    /// </summary>
    private static string? CaptureFilterCtorArgs(
        string filterTypeFqn,
        EquatableArray<FilterCtorModel> ctors,
        AttributeData attributeData,
        AttributeSyntax attributeSyntax,
        SemanticModel semanticModel,
        List<Diagnostic> diagnostics)
    {
        string shortName = filterTypeFqn.Substring(filterTypeFqn.LastIndexOf('.') + 1);
        SeparatedSyntaxList<AttributeArgumentSyntax> arguments = attributeSyntax.ArgumentList?.Arguments ?? default;

        FilterCtorModel? selected = null;
        if (attributeData.AttributeConstructor is { } boundCtor)
        {
            string[] boundNames = boundCtor.Parameters.Select(static p => p.Name).ToArray();
            foreach (FilterCtorModel candidate in ctors.Items)
            {
                if (candidate.Params.Items.Select(static p => p.Name).SequenceEqual(boundNames))
                {
                    selected = candidate;
                    break;
                }
            }
        }
        else
        {
            foreach (FilterCtorModel candidate in ctors.Items.OrderBy(static c => c.Params.Count))
            {
                if (TryMapFilterArguments(candidate, arguments, out _))
                {
                    selected = candidate;
                    break;
                }
            }
        }

        if (selected is null ||
            !TryMapFilterArguments(selected, arguments, out List<(FilterParamModel Param, ExpressionSyntax Expression)> mapped))
        {
            diagnostics.Add(Diagnostic.Create(
                PolyBotDiagnostics.NoMatchingFilterConstructor,
                attributeSyntax.GetLocation(),
                shortName));
            return null;
        }

        return EmitFilterArgList(mapped, semanticModel, shortName, diagnostics);
    }

    /// <summary>
    /// Captures a <c>With*</c> invocation's arguments as an emitted <c>name: literal</c> list,
    /// or <c>null</c> when the call passes no arguments. Bound (library) calls identify the
    /// mirrored ctor via the extension method's parameters (minus the builder receiver);
    /// own-assembly calls select it structurally, preferring the fewest parameters.
    /// </summary>
    private static string? CaptureWithFilterArgs(
        string filterTypeFqn,
        EquatableArray<FilterCtorModel> ctors,
        IMethodSymbol? boundWithMethod,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        List<Diagnostic> diagnostics)
    {
        if (invocation.ArgumentList.Arguments.Count == 0)
        {
            return null;
        }

        string shortName = filterTypeFqn.Substring(filterTypeFqn.LastIndexOf('.') + 1);
        SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;

        FilterCtorModel? selected = null;
        if (boundWithMethod is not null)
        {
            // Reduced extension methods exclude the receiver from Parameters; static forms include it.
            IEnumerable<string> boundNames = boundWithMethod.ReducedFrom is not null
                ? boundWithMethod.Parameters.Select(static p => p.Name)
                : boundWithMethod.Parameters.Skip(1).Select(static p => p.Name);
            foreach (FilterCtorModel candidate in ctors.Items)
            {
                if (candidate.Params.Items.Select(static p => p.Name).SequenceEqual(boundNames))
                {
                    selected = candidate;
                    break;
                }
            }
        }
        else
        {
            foreach (FilterCtorModel candidate in ctors.Items.OrderBy(static c => c.Params.Count))
            {
                if (TryMapWithArguments(candidate, arguments, out _))
                {
                    selected = candidate;
                    break;
                }
            }
        }

        if (selected is null ||
            !TryMapWithArguments(selected, arguments, out List<(FilterParamModel Param, ExpressionSyntax Expression)> mapped))
        {
            diagnostics.Add(Diagnostic.Create(
                PolyBotDiagnostics.NoMatchingFilterConstructor,
                invocation.GetLocation(),
                shortName));
            return null;
        }

        return EmitFilterArgList(mapped, semanticModel, shortName, diagnostics);
    }

    /// <summary>
    /// Maps invocation arguments onto a candidate ctor's parameters: positional in declaration
    /// order, <c>name:</c> by parameter name. Fails on unknown names or duplicate assignments.
    /// The result is ordered by parameter declaration order.
    /// </summary>
    private static bool TryMapWithArguments(
        FilterCtorModel candidate,
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        out List<(FilterParamModel Param, ExpressionSyntax Expression)> mapped)
    {
        mapped = new List<(FilterParamModel, ExpressionSyntax)>();
        int positionalIndex = 0;
        HashSet<string> assigned = new(StringComparer.Ordinal);
        foreach (ArgumentSyntax argument in arguments)
        {
            FilterParamModel? parameter;
            if (argument.NameColon is not null)
            {
                string parameterName = argument.NameColon.Name.Identifier.ValueText;
                parameter = candidate.Params.Items.FirstOrDefault(p => p.Name == parameterName);
            }
            else
            {
                parameter = positionalIndex < candidate.Params.Count ? candidate.Params[positionalIndex] : null;
                positionalIndex++;
            }

            if (parameter is null || !assigned.Add(parameter.Name))
            {
                mapped = new List<(FilterParamModel, ExpressionSyntax)>();
                return false;
            }

            mapped.Add((parameter, argument.Expression));
        }

        mapped = mapped
            .OrderBy(m => candidate.Params.Items.ToList().IndexOf(m.Param))
            .ToList();
        return true;
    }

    /// <summary>
    /// Emits the captured argument mappings as a <c>name: literal, …</c> list; reports CUR044
    /// and returns <c>null</c> when any argument is not a compile-time constant.
    /// </summary>
    private static string? EmitFilterArgList(
        IReadOnlyList<(FilterParamModel Param, ExpressionSyntax Expression)> mapped,
        SemanticModel semanticModel,
        string shortName,
        List<Diagnostic> diagnostics)
    {
        List<string> parts = new();
        foreach ((FilterParamModel parameter, ExpressionSyntax expression) in mapped)
        {
            ITypeSymbol? expectedType = ResolveFilterParamType(parameter.TypeFqn, semanticModel.Compilation);
            if (!FilterArgEmitter.TryEmit(expression, expectedType, semanticModel, out string literal))
            {
                diagnostics.Add(Diagnostic.Create(
                    PolyBotDiagnostics.NonConstantFilterArgument,
                    expression.GetLocation(),
                    shortName));
                return null;
            }

            parts.Add($"{parameter.Name}: {literal}");
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Maps attribute arguments onto a candidate ctor's parameters: positional in declaration
    /// order, <c>name:</c> by parameter name, <c>Name =</c> by property name. Fails on unknown
    /// names or duplicate assignments. The result is ordered by parameter declaration order.
    /// </summary>
    private static bool TryMapFilterArguments(
        FilterCtorModel candidate,
        SeparatedSyntaxList<AttributeArgumentSyntax> arguments,
        out List<(FilterParamModel Param, ExpressionSyntax Expression)> mapped)
    {
        mapped = new List<(FilterParamModel, ExpressionSyntax)>();
        int positionalIndex = 0;
        HashSet<string> assigned = new(StringComparer.Ordinal);
        foreach (AttributeArgumentSyntax argument in arguments)
        {
            FilterParamModel? parameter = null;
            if (argument.NameEquals is not null)
            {
                string propertyName = argument.NameEquals.Name.Identifier.ValueText;
                parameter = candidate.Params.Items.FirstOrDefault(p => p.PropertyName == propertyName);
            }
            else if (argument.NameColon is not null)
            {
                string parameterName = argument.NameColon.Name.Identifier.ValueText;
                parameter = candidate.Params.Items.FirstOrDefault(p => p.Name == parameterName);
            }
            else
            {
                parameter = positionalIndex < candidate.Params.Count ? candidate.Params[positionalIndex] : null;
                positionalIndex++;
            }

            if (parameter is null || !assigned.Add(parameter.Name))
            {
                mapped = new List<(FilterParamModel, ExpressionSyntax)>();
                return false;
            }

            mapped.Add((parameter, argument.Expression));
        }

        mapped = mapped
            .OrderBy(m => candidate.Params.Items.ToList().IndexOf(m.Param))
            .ToList();
        return true;
    }

    /// <summary>
    /// Resolves a mirrored parameter's emitted type name back to a symbol (for enum-member and
    /// array-element literal evaluation).
    /// </summary>
    private static ITypeSymbol? ResolveFilterParamType(string typeFqn, Compilation compilation)
    {
        string name = typeFqn.StartsWith("global::", StringComparison.Ordinal) ? typeFqn.Substring("global::".Length) : typeFqn;
        if (name.EndsWith("?", StringComparison.Ordinal))
        {
            name = name.Substring(0, name.Length - 1);
        }

        if (name.EndsWith("[]", StringComparison.Ordinal))
        {
            ITypeSymbol? elementType = ResolveNamedType(name.Substring(0, name.Length - 2), compilation);
            return elementType is null ? null : compilation.CreateArrayTypeSymbol(elementType);
        }

        return ResolveNamedType(name, compilation);
    }

    private static readonly Dictionary<string, SpecialType> SpecialTypeKeywords = new(StringComparer.Ordinal)
    {
        ["bool"] = SpecialType.System_Boolean,
        ["byte"] = SpecialType.System_Byte,
        ["sbyte"] = SpecialType.System_SByte,
        ["short"] = SpecialType.System_Int16,
        ["ushort"] = SpecialType.System_UInt16,
        ["int"] = SpecialType.System_Int32,
        ["uint"] = SpecialType.System_UInt32,
        ["long"] = SpecialType.System_Int64,
        ["ulong"] = SpecialType.System_UInt64,
        ["float"] = SpecialType.System_Single,
        ["double"] = SpecialType.System_Double,
        ["char"] = SpecialType.System_Char,
        ["string"] = SpecialType.System_String,
    };

    private static ITypeSymbol? ResolveNamedType(string name, Compilation compilation)
    {
        return SpecialTypeKeywords.TryGetValue(name, out SpecialType specialType)
            ? compilation.GetSpecialType(specialType)
            : compilation.GetTypeByMetadataName(name);
    }

    /// <summary>
    /// Reads the <c>Types</c> named argument of a bound <c>[UpdateHandler]</c> attribute into
    /// update-type member names (declaration order, deduplicated). An empty result means the
    /// handler is a universal catch-all.
    /// </summary>
    private static List<string> ReadUpdateHandlerTypes(AttributeData handlerAttributeData, INamedTypeSymbol updateTypeEnum)
    {
        List<string> memberNames = new();
        foreach (KeyValuePair<string, TypedConstant> namedArgument in handlerAttributeData.NamedArguments)
        {
            if (!string.Equals(namedArgument.Key, "Types", StringComparison.Ordinal) ||
                namedArgument.Value.Kind != TypedConstantKind.Array)
            {
                continue;
            }

            foreach (TypedConstant item in namedArgument.Value.Values)
            {
                if (item.Value is null)
                {
                    continue;
                }

                string? memberName = EnumValueToMemberName(updateTypeEnum, item.Value);
                if (memberName is not null && !memberNames.Contains(memberName))
                {
                    memberNames.Add(memberName);
                }
            }
        }

        return memberNames;
    }

    private static IFieldSymbol? FindEnumMember(INamedTypeSymbol updateTypeEnum, string name)
    {
        foreach (ISymbol member in updateTypeEnum.GetMembers())
        {
            if (member is IFieldSymbol { HasConstantValue: true } field &&
                string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return field;
            }
        }

        return null;
    }

    private static string? EnumValueToMemberName(INamedTypeSymbol updateTypeEnum, object? value)
    {
        foreach (ISymbol member in updateTypeEnum.GetMembers())
        {
            if (member is IFieldSymbol { HasConstantValue: true } field &&
                Equals(field.ConstantValue, value))
            {
                return field.Name;
            }
        }

        return null;
    }

    private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        if (SymbolEqualityComparer.Default.Equals(type, baseType))
        {
            return true;
        }

        for (INamedTypeSymbol? current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }

    private static string TrimSuffix(this string value, string suffix)
    {
        return value.EndsWith(suffix, StringComparison.Ordinal)
            ? value.Substring(0, value.Length - suffix.Length)
            : value;
    }
}
