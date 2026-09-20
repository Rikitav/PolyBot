using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PolyBot.Commands;

/// <summary>
/// Helpers for parsing and matching bot commands in messages.
/// </summary>
public static class CommandHelper
{
    /// <summary>
    /// Extracts the command name from a message if it starts with a bot command entity.
    /// </summary>
    /// <param name="message">The message to inspect.</param>
    /// <param name="command">
    /// When this method returns <c>true</c>, the command name without the leading '/'
    /// and any <c>@botname</c> suffix.
    /// </param>
    public static bool TryGetCommandName(Message message, out string command)
    {
        command = string.Empty;
        if (message.Entities is null || message.Text is null)
        {
            return false;
        }

        foreach (MessageEntity entity in message.Entities)
        {
            if (entity.Type != MessageEntityType.BotCommand || entity.Offset != 0)
            {
                continue;
            }

            if (entity.Offset + entity.Length > message.Text.Length)
            {
                return false;
            }

            string text = message.Text.Substring(entity.Offset, entity.Length);
            int atIndex = text.IndexOf('@');
            command = atIndex >= 0 ? text.Substring(0, atIndex) : text;
            if (command.StartsWith("/", StringComparison.Ordinal))
            {
                command = command.Substring(1);
            }

            return command.Length > 0;
        }

        return false;
    }

    /// <summary>
    /// Compares a parsed command name against an alias, ignoring case.
    /// </summary>
    public static bool CommandEquals(string command, string alias)
    {
        return string.Equals(command, alias, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the message matches a command alias under the given prefix mode.
    /// </summary>
    /// <param name="message">The message to inspect.</param>
    /// <param name="alias">The command alias without the prefix.</param>
    /// <param name="prefix">
    /// The matching mode: <c>'/'</c> matches <c>BotCommand</c> entities (with
    /// <c>@botsuffix</c> verification against <paramref name="botUsername"/>),
    /// <c>' '</c> or <c>'\0'</c> matches the text prefix ordinal-ignore-case, and any other
    /// character requires that character at the start of the text followed by the alias.
    /// </param>
    /// <param name="botUsername">
    /// The bot username used to verify <c>@botsuffix</c> entity text; when <c>null</c> or
    /// empty, suffixes cannot be verified and every suffix is accepted.
    /// </param>
    /// <param name="argsStartIndex">
    /// When this method returns <c>true</c>, the index in <see cref="Message.Text"/> where
    /// the command arguments begin (the end of the command entity or alias).
    /// </param>
    public static bool MatchCommand(Message message, string alias, char prefix, string? botUsername, out int argsStartIndex)
    {
        argsStartIndex = 0;
        string? text = message.Text;
        if (text is null)
        {
            return false;
        }

        if (prefix == '/')
        {
            return MatchEntityCommand(message, text, alias, botUsername, out argsStartIndex);
        }

        if (prefix == ' ' || prefix == '\0')
        {
            return MatchTextCommand(text, alias, 0, alias.Length, out argsStartIndex);
        }

        if (text.Length < 2 || text[0] != prefix)
        {
            return false;
        }

        return MatchTextCommand(text, alias, 1, alias.Length + 1, out argsStartIndex);
    }

    /// <summary>
    /// Parses the whitespace-separated tokens that follow a command into values for the
    /// given argument specifications.
    /// </summary>
    /// <param name="message">The original message that carried the command.</param>
    /// <param name="argsStartIndex">The index in <see cref="Message.Text"/> where the arguments begin.</param>
    /// <param name="specs">The argument specifications, one per declared handler argument.</param>
    /// <param name="noArgs">
    /// When <c>true</c>, any token after the command throws a
    /// <see cref="CommandArgsParseException"/>; ignored when <paramref name="tailPatterns"/>
    /// is non-empty, because the tail consumers absorb all trailing text.
    /// </param>
    /// <param name="tailPatterns">
    /// One entry per tail consumer, in handler-parameter declaration order: the regex
    /// pattern of a <c>[Parse]</c> parameter, or <c>null</c> for a <c>[Rest]</c> parameter.
    /// Each <c>[Parse]</c> consumer scans the tail region for its pattern (advancing past
    /// the match), each <c>[Rest]</c> consumer claims everything that is left.
    /// </param>
    /// <returns>
    /// One entry per specification, followed by one entry per tail consumer; missing
    /// optional arguments yield the default value of their type.
    /// </returns>
    /// <exception cref="CommandArgsParseException">
    /// Thrown when <paramref name="noArgs"/> is <c>true</c> and tokens are present, when a
    /// required argument is missing, when a token cannot be parsed into its type, or when
    /// a <c>[Parse]</c> pattern does not match the remaining text.
    /// </exception>
    public static object?[] ParseArgs(Message message, int argsStartIndex, ArgSpec[] specs, bool noArgs, string?[] tailPatterns)
    {
        string text = message.Text ?? string.Empty;
        string argsText = text.Substring(argsStartIndex);
        string[] tokens = argsText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (noArgs && tailPatterns.Length == 0 && tokens.Length > 0)
        {
            throw new CommandArgsParseException("found unwanted arguments", null, message);
        }

        object?[] values = new object?[specs.Length + tailPatterns.Length];
        for (int i = 0; i < specs.Length; i++)
        {
            ArgSpec spec = specs[i];
            if (i >= tokens.Length)
            {
                if (!spec.IsOptional)
                {
                    throw new CommandArgsParseException($"missing required argument '{spec.Name}'", spec.Name, message);
                }

                values[i] = spec.Type.IsValueType ? Activator.CreateInstance(spec.Type) : null;
                continue;
            }

            values[i] = ParseToken(tokens[i], spec, message);
        }

        if (tailPatterns.Length > 0)
        {
            int consumedTokens = tokens.Length < specs.Length ? tokens.Length : specs.Length;
            string remaining = ComputeRest(argsText, consumedTokens);
            for (int t = 0; t < tailPatterns.Length; t++)
            {
                string? pattern = tailPatterns[t];
                if (pattern is null)
                {
                    values[specs.Length + t] = remaining.Trim();
                    continue;
                }

                Match match = Regex.Match(remaining, pattern);
                if (!match.Success)
                {
                    throw new CommandArgsParseException(
                        $"rest \"{remaining}\" does not match pattern \"{pattern}\"",
                        null,
                        message);
                }

                values[specs.Length + t] = match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
                remaining = remaining.Substring(match.Index + match.Length);
            }
        }

        return values;
    }

    private static string ComputeRest(string argsText, int consumedTokens)
    {
        if (consumedTokens <= 0)
        {
            return argsText.Trim();
        }

        int index = 0;
        int consumedEnd = 0;
        int consumed = 0;
        while (index < argsText.Length && consumed < consumedTokens)
        {
            while (index < argsText.Length && char.IsWhiteSpace(argsText[index]))
            {
                index++;
            }

            while (index < argsText.Length && !char.IsWhiteSpace(argsText[index]))
            {
                index++;
            }

            consumedEnd = index;
            consumed++;
        }

        return argsText.Substring(consumedEnd).Trim();
    }

    private static bool MatchEntityCommand(Message message, string text, string alias, string? botUsername, out int argsStartIndex)
    {
        argsStartIndex = 0;
        if (message.Entities is null)
        {
            return false;
        }

        foreach (MessageEntity entity in message.Entities)
        {
            if (entity.Type != MessageEntityType.BotCommand || entity.Offset != 0)
            {
                continue;
            }

            if (entity.Offset + entity.Length > text.Length)
            {
                return false;
            }

            string entityText = text.Substring(entity.Offset, entity.Length);
            int atIndex = entityText.IndexOf('@');
            string name = atIndex >= 0 ? entityText.Substring(0, atIndex) : entityText;
            if (name.StartsWith("/", StringComparison.Ordinal))
            {
                name = name.Substring(1);
            }

            if (!string.Equals(name, alias, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (atIndex >= 0 && botUsername is { Length: > 0 } &&
                !string.Equals(entityText.Substring(atIndex + 1), botUsername, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int endIndex = entity.Offset + entity.Length;
            if (text.Length > endIndex && !char.IsWhiteSpace(text[endIndex]))
            {
                return false;
            }

            argsStartIndex = endIndex;
            return true;
        }

        return false;
    }

    private static bool MatchTextCommand(string text, string alias, int compareOffset, int argsStart, out int argsStartIndex)
    {
        argsStartIndex = 0;
        if (!text.Substring(compareOffset).StartsWith(alias, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (text.Length > argsStart && !char.IsWhiteSpace(text[argsStart]))
        {
            return false;
        }

        argsStartIndex = argsStart;
        return true;
    }

    private static object ParseToken(string token, ArgSpec spec, Message sourceMessage)
    {
        Type type = spec.Type;
        Type? underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType is not null)
        {
            type = underlyingType;
        }

        if (type == typeof(string))
        {
            return token;
        }

        if (type == typeof(bool))
        {
            if (bool.TryParse(token, out bool boolValue))
            {
                return boolValue;
            }

            throw CannotParse(token, spec, type, sourceMessage);
        }

        if (type.IsEnum)
        {
            foreach (string enumName in Enum.GetNames(type))
            {
                if (string.Equals(enumName, token, StringComparison.OrdinalIgnoreCase))
                {
                    return Enum.Parse(type, enumName);
                }
            }

            if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out long numericEnumValue))
            {
                return Enum.ToObject(type, numericEnumValue);
            }

            throw CannotParse(token, spec, type, sourceMessage);
        }

        if (TryParseNumeric(token, type, out object? numericValue))
        {
            return numericValue!;
        }

        TypeConverter converter = TypeDescriptor.GetConverter(type);
        if (converter.CanConvertFrom(typeof(string)))
        {
            try
            {
                return converter.ConvertFromInvariantString(token)!;
            }
            catch (Exception ex)
            {
                throw new CommandArgsParseException(CannotParseMessage(token, spec, type), spec.Name, sourceMessage, ex);
            }
        }

        throw CannotParse(token, spec, type, sourceMessage);
    }

    private static CommandArgsParseException CannotParse(string token, ArgSpec spec, Type type, Message sourceMessage)
    {
        return new CommandArgsParseException(CannotParseMessage(token, spec, type), spec.Name, sourceMessage);
    }

    private static string CannotParseMessage(string token, ArgSpec spec, Type type)
    {
        return $"argument '{spec.Name}': cannot parse '{token}' as {type.Name}";
    }

    private static bool TryParseNumeric(string token, Type type, out object? value)
    {
        if (type == typeof(byte))
        {
            bool parsed = byte.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte result);
            value = result;
            return parsed;
        }

        if (type == typeof(sbyte))
        {
            bool parsed = sbyte.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out sbyte result);
            value = result;
            return parsed;
        }

        if (type == typeof(short))
        {
            bool parsed = short.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out short result);
            value = result;
            return parsed;
        }

        if (type == typeof(ushort))
        {
            bool parsed = ushort.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort result);
            value = result;
            return parsed;
        }

        if (type == typeof(int))
        {
            bool parsed = int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result);
            value = result;
            return parsed;
        }

        if (type == typeof(uint))
        {
            bool parsed = uint.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint result);
            value = result;
            return parsed;
        }

        if (type == typeof(long))
        {
            bool parsed = long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result);
            value = result;
            return parsed;
        }

        if (type == typeof(ulong))
        {
            bool parsed = ulong.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong result);
            value = result;
            return parsed;
        }

        if (type == typeof(float))
        {
            bool parsed = float.TryParse(token, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float result);
            value = result;
            return parsed;
        }

        if (type == typeof(double))
        {
            bool parsed = double.TryParse(token, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double result);
            value = result;
            return parsed;
        }

        if (type == typeof(decimal))
        {
            bool parsed = decimal.TryParse(token, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal result);
            value = result;
            return parsed;
        }

        value = null;
        return false;
    }
}
