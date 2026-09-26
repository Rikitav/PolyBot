using Telegram.Bot.Types;
using PolyBot.Routing;

namespace PolyBot.Filters;

/// <summary>
/// Filter that checks environment variable values.
/// When no value is given, the filter passes as soon as the variable exists (with any value).
/// </summary>
public sealed class EnvironmentVariableFilter : IUpdateFilter
{
    private readonly string _variable;
    private readonly string? _value;
    private readonly StringComparison _comparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvironmentVariableFilter"/> class that checks for existence.
    /// </summary>
    /// <param name="variable">The environment variable name to check.</param>
    public EnvironmentVariableFilter(string variable)
        : this(variable, null, StringComparison.Ordinal) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvironmentVariableFilter"/> class with a specific value.
    /// </summary>
    /// <param name="variable">The environment variable name to check.</param>
    /// <param name="value">The expected value of the environment variable.</param>
    public EnvironmentVariableFilter(string variable, string? value)
        : this(variable, value, StringComparison.Ordinal) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvironmentVariableFilter"/> class with a specific value and custom comparison.
    /// </summary>
    /// <param name="variable">The environment variable name to check.</param>
    /// <param name="value">The expected value of the environment variable.</param>
    /// <param name="comparison">The string comparison type to use for value matching.</param>
    public EnvironmentVariableFilter(string variable, string? value, StringComparison comparison)
    {
        _variable = variable;
        _value = value;
        _comparison = comparison;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvironmentVariableFilter"/> class with custom comparison.
    /// </summary>
    /// <param name="variable">The environment variable name to check.</param>
    /// <param name="comparison">The string comparison type to use for value matching.</param>
    public EnvironmentVariableFilter(string variable, StringComparison comparison)
        : this(variable, null, comparison) { }

    /// <inheritdoc/>
    public bool CanPass(Update update)
    {
        string? envValue = Environment.GetEnvironmentVariable(_variable);

        if (_value is null)
            return envValue is not null;

        return envValue is not null && envValue.Equals(_value, _comparison);
    }
}
