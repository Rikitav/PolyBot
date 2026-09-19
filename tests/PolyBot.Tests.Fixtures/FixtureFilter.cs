using PolyBot.Routing;
using Telegram.Bot.Types;

namespace PolyBot.Tests.Fixtures;

/// <summary>Lives in a referenced assembly so the consuming test assembly must not regenerate its wrapper attribute (full-type-name collision).</summary>
public sealed class FixtureFilter : IUpdateFilter
{
    public bool CanPass(Update update)
    {
        return update.Message?.Text?.Contains("magicword", StringComparison.OrdinalIgnoreCase) == true;
    }
}
