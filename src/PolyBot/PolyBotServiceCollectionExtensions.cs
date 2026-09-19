using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PolyBot.Awaits;
using PolyBot.State;

namespace PolyBot;

/// <summary>
/// Dependency injection extensions for PolyBot.
/// </summary>
public static partial class PolyBotServiceCollectionExtensions
{
    /// <summary>
    /// Adds the PolyBot core services with try-add semantics — register your own
    /// implementations <em>before</em> calling this to override them.
    /// </summary>
    /// <remarks>
    /// <see cref="PolyBotOptions"/> is registered by <see cref="PolyBotClient"/> (or falls back
    /// to a default in the webhook integration); preset your own instance when running behind a webhook.
    /// </remarks>
    public static IServiceCollection AddPolyBotDefaults(this IServiceCollection services)
    {
        services.TryAddSingleton<IStateStorage, MemoryStateStorage>();
        services.TryAddSingleton<IStateMachine, StateMachine>();
        services.TryAddSingleton<IUpdateContextAccessor, UpdateContextAccessor>();
        services.TryAddSingleton<IUpdateAwaiter, UpdateAwaiter>();
        return services;
    }
}
