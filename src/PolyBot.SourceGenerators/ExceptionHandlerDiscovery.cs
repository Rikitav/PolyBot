using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PolyBot.SourceGenerators;

internal sealed class ExceptionHandlerModel : IEquatable<ExceptionHandlerModel>
{
    public required string ContainingTypeFqn { get; init; }

    public required string MethodName { get; init; }

    public required bool IsStatic { get; init; }

    public required bool ReturnsValueTask { get; init; }

    public required EquatableArray<ParameterModel> Parameters { get; init; }

    public bool Equals(ExceptionHandlerModel? other)
    {
        return other is not null
            && ContainingTypeFqn == other.ContainingTypeFqn
            && MethodName == other.MethodName
            && IsStatic == other.IsStatic
            && ReturnsValueTask == other.ReturnsValueTask
            && Parameters.Equals(other.Parameters);
    }

    public override bool Equals(object? obj) => Equals(obj as ExceptionHandlerModel);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + ContainingTypeFqn.GetHashCode();
            hash = (hash * 31) + MethodName.GetHashCode();
            hash = (hash * 31) + IsStatic.GetHashCode();
            hash = (hash * 31) + ReturnsValueTask.GetHashCode();
            hash = (hash * 31) + Parameters.GetHashCode();
            return hash;
        }
    }
}

internal sealed class ExceptionHandlerItem
{
    public ExceptionHandlerModel? Model { get; init; }

    public Location? Location { get; init; }

    public List<Diagnostic> Diagnostics { get; init; } = new();
}

/// <summary>
/// Discovers the single method decorated with <c>PolyBot.ExceptionHandlerAttribute</c> and
/// classifies its parameters for the error path: bot client, cancellation token and DI
/// (keyed or plain) resolve; update-bound parameters have nothing to bind to and are
/// rejected.
/// </summary>
internal static class ExceptionHandlerDiscovery
{
    public static ExceptionHandlerItem Discover(GeneratorSyntaxContext context)
    {
        List<Diagnostic> diagnostics = new();
        if (context.Node is not MethodDeclarationSyntax methodSyntax ||
            !HasExceptionHandlerAttribute(methodSyntax))
        {
            return new ExceptionHandlerItem();
        }

        if (context.SemanticModel.GetDeclaredSymbol(methodSyntax) is not IMethodSymbol methodSymbol)
        {
            return new ExceptionHandlerItem();
        }

        Compilation compilation = context.SemanticModel.Compilation;
        INamedTypeSymbol? botClientType = compilation.GetTypeByMetadataName("Telegram.Bot.ITelegramBotClient");
        INamedTypeSymbol? cancellationTokenType = compilation.GetTypeByMetadataName("System.Threading.CancellationToken");
        INamedTypeSymbol? updateType = compilation.GetTypeByMetadataName("Telegram.Bot.Types.Update");
        INamedTypeSymbol? exceptionType = compilation.GetTypeByMetadataName("System.Exception");
        INamedTypeSymbol? errorSourceType = compilation.GetTypeByMetadataName("Telegram.Bot.Polling.HandleErrorSource");
        INamedTypeSymbol? keyAttribute = compilation.GetTypeByMetadataName("PolyBot.Attributes.KeyAttribute");
        INamedTypeSymbol? taskType = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
        INamedTypeSymbol? valueTaskType = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask");
        Location location = methodSymbol.Locations.FirstOrDefault() ?? Location.None;

        bool returnsTask = SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType, taskType);
        bool returnsValueTask = SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType, valueTaskType);
        if (!returnsTask && !returnsValueTask)
        {
            diagnostics.Add(Diagnostic.Create(
                PolyBotDiagnostics.InvalidExceptionHandlerSignature,
                location,
                methodSymbol.Name));
            return new ExceptionHandlerItem { Diagnostics = diagnostics };
        }

        List<ParameterModel> parameters = new();
        foreach (IParameterSymbol parameter in methodSymbol.Parameters)
        {
            if (updateType is not null && SymbolEqualityComparer.Default.Equals(parameter.Type, updateType))
            {
                diagnostics.Add(Diagnostic.Create(
                    PolyBotDiagnostics.InvalidExceptionHandlerParameter,
                    parameter.Locations.FirstOrDefault() ?? location,
                    parameter.Name));
                continue;
            }

            ParameterSource source = ParameterSource.Service;
            string? keyLiteral = null;
            AttributeData? keyData = keyAttribute is not null
                ? parameter.GetAttributes().FirstOrDefault(a =>
                    a.AttributeClass is not null && SymbolEqualityComparer.Default.Equals(a.AttributeClass, keyAttribute))
                : null;
            if (keyData is not null)
            {
                source = ParameterSource.KeyedService;
                if (keyData.ConstructorArguments.Length == 1 &&
                    keyData.ConstructorArguments[0].Value is { } keyValue)
                {
                    keyLiteral = keyValue is string keyString
                        ? "\"" + keyString.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""
                        : Convert.ToString(keyValue, System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            else if (botClientType is not null && SymbolEqualityComparer.Default.Equals(parameter.Type, botClientType))
            {
                source = ParameterSource.BotClient;
            }
            else if (cancellationTokenType is not null && SymbolEqualityComparer.Default.Equals(parameter.Type, cancellationTokenType))
            {
                source = ParameterSource.CancellationToken;
            }
            else if (exceptionType is not null && SymbolEqualityComparer.Default.Equals(parameter.Type, exceptionType))
            {
                source = ParameterSource.ErrorException;
            }
            else if (errorSourceType is not null && SymbolEqualityComparer.Default.Equals(parameter.Type, errorSourceType))
            {
                source = ParameterSource.ErrorSource;
            }

            parameters.Add(new ParameterModel
            {
                TypeFqn = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                Source = source,
                KeyLiteral = keyLiteral,
                ArgIndex = -1,
            });
        }

        return new ExceptionHandlerItem
        {
            Model = new ExceptionHandlerModel
            {
                ContainingTypeFqn = methodSymbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                MethodName = methodSymbol.Name,
                IsStatic = methodSymbol.IsStatic,
                ReturnsValueTask = returnsValueTask,
                Parameters = new EquatableArray<ParameterModel>(parameters.ToArray()),
            },
            Location = location,
            Diagnostics = diagnostics,
        };
    }

    private static bool HasExceptionHandlerAttribute(MethodDeclarationSyntax methodSyntax)
    {
        foreach (AttributeSyntax attribute in methodSyntax.AttributeLists.SelectMany(list => list.Attributes))
        {
            string name = attribute.Name.ToString();
            if (name is "ExceptionHandler" or "ExceptionHandlerAttribute" or "PolyBot.Attributes.ExceptionHandler" or "PolyBot.Attributes.ExceptionHandlerAttribute")
            {
                return true;
            }
        }

        return false;
    }
}
