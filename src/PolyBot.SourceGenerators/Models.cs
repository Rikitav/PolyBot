using Microsoft.CodeAnalysis;

namespace PolyBot.SourceGenerators;

internal enum ParameterSource
{
    BotClient,
    CancellationToken,
    Update,
    Payload,
    KeyedService,
    Service,
    Arg,
    Rest,
    Parse,
    StateMachine,
    StateStorage,
    Awaiter,
    ErrorException,
    ErrorSource,
}

internal sealed class StateConditionModel : IEquatable<StateConditionModel>
{
    public required string EnumTypeFqn { get; init; }

    public required string EnumTypeDisplayName { get; init; }

    public required string StateKeyName { get; init; }

    public required bool IsNoState { get; init; }

    public required EquatableArray<string> Comparands { get; init; }

    public bool Equals(StateConditionModel? other)
    {
        return other is not null
            && EnumTypeFqn == other.EnumTypeFqn
            && EnumTypeDisplayName == other.EnumTypeDisplayName
            && StateKeyName == other.StateKeyName
            && IsNoState == other.IsNoState
            && Comparands.Equals(other.Comparands);
    }

    public override bool Equals(object? obj) => Equals(obj as StateConditionModel);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + EnumTypeFqn.GetHashCode();
            hash = (hash * 31) + EnumTypeDisplayName.GetHashCode();
            hash = (hash * 31) + StateKeyName.GetHashCode();
            hash = (hash * 31) + IsNoState.GetHashCode();
            hash = (hash * 31) + Comparands.GetHashCode();
            return hash;
        }
    }
}

internal sealed class ArgModel : IEquatable<ArgModel>
{
    public required string SpecTypeFqn { get; init; }

    public required string ParamTypeFqn { get; init; }

    public required string Name { get; init; }

    public required bool IsOptional { get; init; }

    public required int ParameterIndex { get; init; }

    public bool Equals(ArgModel? other)
    {
        return other is not null
            && SpecTypeFqn == other.SpecTypeFqn
            && ParamTypeFqn == other.ParamTypeFqn
            && Name == other.Name
            && IsOptional == other.IsOptional
            && ParameterIndex == other.ParameterIndex;
    }

    public override bool Equals(object? obj) => Equals(obj as ArgModel);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + SpecTypeFqn.GetHashCode();
            hash = (hash * 31) + ParamTypeFqn.GetHashCode();
            hash = (hash * 31) + Name.GetHashCode();
            hash = (hash * 31) + IsOptional.GetHashCode();
            hash = (hash * 31) + ParameterIndex.GetHashCode();
            return hash;
        }
    }
}

internal sealed class TailConsumerModel : IEquatable<TailConsumerModel>
{
    public required int ParameterIndex { get; init; }

    public required string? Pattern { get; init; }

    public bool Equals(TailConsumerModel? other)
    {
        return other is not null
            && ParameterIndex == other.ParameterIndex
            && Pattern == other.Pattern;
    }

    public override bool Equals(object? obj) => Equals(obj as TailConsumerModel);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + ParameterIndex.GetHashCode();
            hash = (hash * 31) + (Pattern?.GetHashCode() ?? 0);
            return hash;
        }
    }
}

internal sealed class FilterClassModel : IEquatable<FilterClassModel>
{
    public required string TypeFqn { get; init; }

    public required string ShortName { get; init; }

    /// <summary>
    /// Name of the generated filter base class in the filter's base-type chain, or <c>null</c> when the filter implements <c>IUpdateFilter</c> directly.
    /// </summary>
    public string? DtoBaseName { get; init; }

    public bool Equals(FilterClassModel? other)
    {
        return other is not null && TypeFqn == other.TypeFqn && ShortName == other.ShortName && DtoBaseName == other.DtoBaseName;
    }

    public override bool Equals(object? obj) => Equals(obj as FilterClassModel);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + TypeFqn.GetHashCode();
            hash = (hash * 31) + ShortName.GetHashCode();
            hash = (hash * 31) + (DtoBaseName?.GetHashCode() ?? 0);
            return hash;
        }
    }
}

internal enum AwaitKeyMode
{
    UserId,
    ChatId,
    UserInChat,
}

/// <summary>
/// One condition item of an await site's static branch, in source (chain) order.
/// </summary>
internal enum AwaitConditionKind
{
    TextPattern,
    WhereLambda,
    Filter,
}

internal sealed class AwaitSiteModel : IEquatable<AwaitSiteModel>
{
    public required string DtoTypeFqn { get; init; }

    public required EquatableArray<string> TextPatterns { get; init; }

    public required EquatableArray<string> Filters { get; init; }

    public required EquatableArray<AwaitConditionModel> Conditions { get; init; }

    public required AwaitKeyMode KeyMode { get; init; }

    public required EquatableArray<UpdateTypeMapping> Members { get; init; }

    public bool Equals(AwaitSiteModel? other)
    {
        return other is not null
            && DtoTypeFqn == other.DtoTypeFqn
            && TextPatterns.Equals(other.TextPatterns)
            && Filters.Equals(other.Filters)
            && Conditions.Equals(other.Conditions)
            && KeyMode == other.KeyMode
            && Members.Equals(other.Members);
    }

    public override bool Equals(object? obj) => Equals(obj as AwaitSiteModel);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + DtoTypeFqn.GetHashCode();
            hash = (hash * 31) + TextPatterns.GetHashCode();
            hash = (hash * 31) + Filters.GetHashCode();
            hash = (hash * 31) + Conditions.GetHashCode();
            hash = (hash * 31) + KeyMode.GetHashCode();
            hash = (hash * 31) + Members.GetHashCode();
            return hash;
        }
    }
}

internal sealed record AwaitSiteEntry(AwaitSiteModel Site, int SiteIndex);

internal sealed record StateCasePair(string EnumTypeFqn, string StateKeyName);

internal sealed class AwaitConditionModel : IEquatable<AwaitConditionModel>
{
    public required AwaitConditionKind Kind { get; init; }

    /// <summary>
    /// Regex pattern, validated lambda source text, or filter type FQN, per <see cref="Kind"/>.
    /// </summary>
    public required string Value { get; init; }

    public bool Equals(AwaitConditionModel? other)
    {
        return other is not null && Kind == other.Kind && Value == other.Value;
    }

    public override bool Equals(object? obj) => Equals(obj as AwaitConditionModel);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + Kind.GetHashCode();
            hash = (hash * 31) + Value.GetHashCode();
            return hash;
        }
    }
}

internal enum HandlerReturnKind
{
    Result,
    Plain,
}

internal sealed class ParameterModel : IEquatable<ParameterModel>
{
    public required string TypeFqn { get; init; }
    public required ParameterSource Source { get; init; }

    public string? KeyLiteral { get; init; }

    public int ArgIndex { get; init; } = -1;

    public bool Equals(ParameterModel? other)
    {
        return other is not null
            && TypeFqn == other.TypeFqn
            && Source == other.Source
            && KeyLiteral == other.KeyLiteral
            && ArgIndex == other.ArgIndex;
    }

    public override bool Equals(object? obj) => Equals(obj as ParameterModel);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + TypeFqn.GetHashCode();
            hash = (hash * 31) + Source.GetHashCode();
            hash = (hash * 31) + (KeyLiteral?.GetHashCode() ?? 0);
            hash = (hash * 31) + ArgIndex.GetHashCode();
            return hash;
        }
    }
}

internal enum PatternParseKind
{
    /// <summary>
    /// Numeric <c>TryParse</c> with <c>NumberStyles</c>/<c>IFormatProvider</c>, emitted with invariant culture so captures parse the same for every locale.
    /// </summary>
    TryParseInvariant,

    TryParse,

    String,

    Enum,

    /// <summary>
    /// No accessible parse method (CUR028 reported); bound to <c>default</c>.
    /// </summary>
    Failed,
}

internal sealed class PatternSegmentModel : IEquatable<PatternSegmentModel>
{
    public required bool IsLiteral { get; init; }

    public required bool IsWildcard { get; init; }

    /// <summary>
    /// Capture name (placeholder) or the literal text.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Typed constraint name of a capture (e.g. <c>"int"</c>), when present.
    /// </summary>
    public required string? Constraint { get; init; }

    public required string? SpecTypeFqn { get; init; }

    public required PatternParseKind ParseKind { get; init; }

    public bool Equals(PatternSegmentModel? other)
    {
        return other is not null
            && IsLiteral == other.IsLiteral
            && IsWildcard == other.IsWildcard
            && Text == other.Text
            && Constraint == other.Constraint
            && SpecTypeFqn == other.SpecTypeFqn
            && ParseKind == other.ParseKind;
    }

    public override bool Equals(object? obj) => Equals(obj as PatternSegmentModel);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + IsLiteral.GetHashCode();
            hash = (hash * 31) + IsWildcard.GetHashCode();
            hash = (hash * 31) + Text.GetHashCode();
            hash = (hash * 31) + (Constraint?.GetHashCode() ?? 0);
            hash = (hash * 31) + (SpecTypeFqn?.GetHashCode() ?? 0);
            hash = (hash * 31) + ParseKind.GetHashCode();
            return hash;
        }
    }
}

internal sealed class PatternModel : IEquatable<PatternModel>
{
    public required char Separator { get; init; }

    public required EquatableArray<PatternSegmentModel> Segments { get; init; }

    public required int WildcardIndex { get; init; }

    public bool Equals(PatternModel? other)
    {
        return other is not null
            && Separator == other.Separator
            && Segments.Equals(other.Segments)
            && WildcardIndex == other.WildcardIndex;
    }

    public override bool Equals(object? obj) => Equals(obj as PatternModel);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + Separator.GetHashCode();
            hash = (hash * 31) + Segments.GetHashCode();
            hash = (hash * 31) + WildcardIndex.GetHashCode();
            return hash;
        }
    }
}

internal sealed class ThrottleModel : IEquatable<ThrottleModel>
{
    public required int Limit { get; init; }

    public required int PeriodMilliseconds { get; init; }

    /// <summary>
    /// <see cref="PolyBot.ThrottleScope"/> member name.
    /// </summary>
    public required string ScopeName { get; init; }

    /// <summary>
    /// <see cref="PolyBot.ThrottleAction"/> member name.
    /// </summary>
    public required string ActionName { get; init; }

    public bool Equals(ThrottleModel? other)
    {
        return other is not null
            && Limit == other.Limit
            && PeriodMilliseconds == other.PeriodMilliseconds
            && ScopeName == other.ScopeName
            && ActionName == other.ActionName;
    }

    public override bool Equals(object? obj) => Equals(obj as ThrottleModel);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + Limit.GetHashCode();
            hash = (hash * 31) + PeriodMilliseconds.GetHashCode();
            hash = (hash * 31) + ScopeName.GetHashCode();
            hash = (hash * 31) + ActionName.GetHashCode();
            return hash;
        }
    }
}

internal sealed class FilterModel : IEquatable<FilterModel>
{
    public required string TypeFqn { get; init; }

    public bool Equals(FilterModel? other) => other is not null && TypeFqn == other.TypeFqn;

    public override bool Equals(object? obj) => Equals(obj as FilterModel);

    public override int GetHashCode() => TypeFqn.GetHashCode();
}

internal sealed class HandlerModel : IEquatable<HandlerModel>
{
    public required string ContainingTypeFqn { get; init; }

    public required string MethodName { get; init; }

    public required bool IsStatic { get; init; }

    public required string UpdateTypeMemberName { get; init; }

    public required string UpdatePropertyName { get; init; }

    public required string PayloadTypeFqn { get; init; }

    public required int Priority { get; init; }

    public required EquatableArray<string> Aliases { get; init; }

    public char Prefix { get; init; } = '/';

    public bool NoArgs { get; init; }

    public string? CommandDescription { get; init; }

    public string? CommandLanguageCode { get; init; }

    /// <summary>
    /// <see cref="PolyBot.CommandScope"/> member name, as written on the attribute.
    /// </summary>
    public string CommandScope { get; init; } = "Default";

    public bool CommandHidden { get; init; }

    public required EquatableArray<ArgModel> Args { get; init; }

    public required EquatableArray<TailConsumerModel> TailConsumers { get; init; }

    public required EquatableArray<StateConditionModel> StateConditions { get; init; }

    public required EquatableArray<FilterModel> Filters { get; init; }

    public required EquatableArray<ParameterModel> Parameters { get; init; }

    public required HandlerReturnKind ReturnKind { get; init; }

    public bool ReturnsValueTask { get; init; }

    public required EquatableArray<AwaitSiteModel> AwaitSites { get; init; }

    public PatternModel? Pattern { get; init; }

    public ThrottleModel? Throttle { get; init; }

    public bool Equals(HandlerModel? other)
    {
        return other is not null
            && ContainingTypeFqn == other.ContainingTypeFqn
            && MethodName == other.MethodName
            && IsStatic == other.IsStatic
            && UpdateTypeMemberName == other.UpdateTypeMemberName
            && UpdatePropertyName == other.UpdatePropertyName
            && PayloadTypeFqn == other.PayloadTypeFqn
            && Priority == other.Priority
            && Aliases.Equals(other.Aliases)
            && Prefix == other.Prefix
            && NoArgs == other.NoArgs
            && CommandDescription == other.CommandDescription
            && CommandLanguageCode == other.CommandLanguageCode
            && CommandScope == other.CommandScope
            && CommandHidden == other.CommandHidden
            && Args.Equals(other.Args)
            && TailConsumers.Equals(other.TailConsumers)
            && StateConditions.Equals(other.StateConditions)
            && Filters.Equals(other.Filters)
            && Parameters.Equals(other.Parameters)
            && ReturnKind == other.ReturnKind
            && ReturnsValueTask == other.ReturnsValueTask
            && AwaitSites.Equals(other.AwaitSites)
            && Equals(Pattern, other.Pattern)
            && Equals(Throttle, other.Throttle);
    }

    public override bool Equals(object? obj) => Equals(obj as HandlerModel);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + ContainingTypeFqn.GetHashCode();
            hash = (hash * 31) + MethodName.GetHashCode();
            hash = (hash * 31) + IsStatic.GetHashCode();
            hash = (hash * 31) + UpdateTypeMemberName.GetHashCode();
            hash = (hash * 31) + UpdatePropertyName.GetHashCode();
            hash = (hash * 31) + PayloadTypeFqn.GetHashCode();
            hash = (hash * 31) + Priority.GetHashCode();
            hash = (hash * 31) + Aliases.GetHashCode();
            hash = (hash * 31) + Prefix.GetHashCode();
            hash = (hash * 31) + NoArgs.GetHashCode();
            hash = (hash * 31) + (CommandDescription?.GetHashCode() ?? 0);
            hash = (hash * 31) + (CommandLanguageCode?.GetHashCode() ?? 0);
            hash = (hash * 31) + CommandScope.GetHashCode();
            hash = (hash * 31) + CommandHidden.GetHashCode();
            hash = (hash * 31) + Args.GetHashCode();
            hash = (hash * 31) + TailConsumers.GetHashCode();
            hash = (hash * 31) + StateConditions.GetHashCode();
            hash = (hash * 31) + Filters.GetHashCode();
            hash = (hash * 31) + Parameters.GetHashCode();
            hash = (hash * 31) + ReturnKind.GetHashCode();
            hash = (hash * 31) + ReturnsValueTask.GetHashCode();
            hash = (hash * 31) + AwaitSites.GetHashCode();
            hash = (hash * 31) + (Pattern?.GetHashCode() ?? 0);
            hash = (hash * 31) + (Throttle?.GetHashCode() ?? 0);
            return hash;
        }
    }
}

internal sealed class HandlerItem
{
    public static readonly HandlerItem Skip = new();

    public HandlerModel? Model { get; private init; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; private init; } = Array.Empty<Diagnostic>();

    public static HandlerItem ForModel(HandlerModel? model, IReadOnlyList<Diagnostic>? diagnostics = null) => new() { Model = model, Diagnostics = diagnostics ?? Array.Empty<Diagnostic>() };
}

/// <summary>
/// A minimal equatable array to keep incremental generator caching effective.
/// </summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>
{
    private readonly T[]? _items;

    public EquatableArray(T[] items)
    {
        _items = items;
    }

    public T[] Items => _items ?? [];

    public int Count => _items?.Length ?? 0;

    public T this[int index] => Items[index];

    public bool Equals(EquatableArray<T> other)
    {
        if (_items is null || other._items is null)
        {
            return _items is null && other._items is null;
        }

        if (_items.Length != other._items.Length)
        {
            return false;
        }

        for (int i = 0; i < _items.Length; i++)
        {
            if (!_items[i].Equals(other._items[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            foreach (T item in Items)
            {
                hash = (hash * 31) + item.GetHashCode();
            }

            return hash;
        }
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Items).GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
