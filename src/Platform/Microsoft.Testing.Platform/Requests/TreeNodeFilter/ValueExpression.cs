// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Requests;

// An expression representing a single value.
[DebuggerDisplay("{Value}")]
internal sealed class ValueExpression : FilterExpression
{
    private readonly CultureInfo _culture;
    private readonly bool _matchesAnyValue;
    private readonly string? _asciiLiteral;
    private Regex? _regex;

    public ValueExpression(string value)
    {
        Value = value;

        // The regex captures the current culture for case-insensitive matching when it's created.
        // It's created lazily, so capture the culture here to keep matching independent of the calling thread.
        _culture = CultureInfo.CurrentCulture;

        // Most filter segments are either a lone wildcard or a plain name. Both can be matched without
        // a regex, which avoids the cost of constructing and compiling one for every run that uses a filter.
        _matchesAnyValue = IsOnlyWildcards(value);
        if (!_matchesAnyValue && !ContainsWildcard(value) && !HasTurkishCasing(_culture))
        {
            string literal = Regex.Unescape(value);
            if (IsAscii(literal))
            {
                _asciiLiteral = literal;
            }
        }
    }

    // The value, as a regex pattern: every character is escaped, except wildcards which are '.*'.
    public string Value { get; }

    public bool IsMatch(string input)
    {
        // '.' doesn't match '\n' and '$' matches before a final '\n', so leave those inputs to the regex.
        if (input.IndexOf('\n') < 0)
        {
            if (_matchesAnyValue)
            {
                return true;
            }

            if (_asciiLiteral is not null)
            {
                if (input.Length != _asciiLiteral.Length)
                {
                    return false;
                }

                // Outside of ASCII, the regex has case equivalences that an ordinal comparison doesn't (e.g. 'K' and the Kelvin sign).
                if (IsAscii(input))
                {
                    return string.Equals(input, _asciiLiteral, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        return GetRegex().IsMatch(input);
    }

    private Regex GetRegex()
    {
        if (_regex is not null)
        {
            return _regex;
        }

        CultureInfo currentCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = _culture;
        try
        {
            Regex regex = new($"^{Value}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            return Interlocked.CompareExchange(ref _regex, regex, null) ?? regex;
        }
        finally
        {
            CultureInfo.CurrentCulture = currentCulture;
        }
    }

    private static bool IsOnlyWildcards(string value)
    {
        if (value.Length == 0 || value.Length % 2 != 0)
        {
            return false;
        }

        for (int i = 0; i < value.Length; i += 2)
        {
            if (value[i] != '.' || value[i + 1] != '*')
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsWildcard(string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\')
            {
                // Skip the escaped character.
                i++;
            }
            else if (value[i] == '.')
            {
                // A literal '.' is always escaped, so this is the start of a '.*' wildcard.
                return true;
            }
        }

        return false;
    }

    // In these cultures 'i' and 'I' aren't case-insensitively equal, so an ordinal comparison would differ from the regex.
    private static bool HasTurkishCasing(CultureInfo culture)
        => culture.Name.StartsWith("tr", StringComparison.OrdinalIgnoreCase)
        || culture.Name.StartsWith("az", StringComparison.OrdinalIgnoreCase);

    private static bool IsAscii(string value)
    {
        foreach (char c in value)
        {
            if (c > 0x7F)
            {
                return false;
            }
        }

        return true;
    }
}
