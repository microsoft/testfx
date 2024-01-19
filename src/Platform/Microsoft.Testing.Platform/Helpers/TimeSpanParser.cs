// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace Microsoft.Testing.Platform.Helpers;

internal static partial class TimeSpanParser
{
    private static readonly Regex Pattern = GetRegex();

#if NET7_0_OR_GREATER
    [GeneratedRegex(@"(?<value>^\d+(?:\.\d+)?)\s*(?<suffix>ms|mil|m|h|d|s?[a-z]*)$", RegexOptions.IgnoreCase)]
    private static partial Regex GetRegex();
#else
    private static Regex GetRegex() => new(@"(?<value>^\d+(?:\.\d+)?)\s*(?<suffix>ms|mil|m|h|d|s?[a-z]*)$", RegexOptions.IgnoreCase);
#endif

    public static TimeSpan Parse(string? time) => TryParse(time, out TimeSpan result) ? result : throw GetFormatException(time);

    public static bool TryParse(string? time, out TimeSpan result)
    {
        if (RoslynString.IsNullOrWhiteSpace(time))
        {
            result = TimeSpan.Zero;
            return true;
        }

        Match match = Pattern.Match(time);
        if (!match.Success)
        {
            result = TimeSpan.Zero;
            return false;
        }

        string value = match.Groups["value"].Value;
        if (!double.TryParse(value, out double number))
        {
            throw GetFormatException(value);
        }

        string suffix = match.Groups["suffix"].Value;
        StringComparison c = StringComparison.OrdinalIgnoreCase;

        // mil to distinguish milliseconds from minutes
        // ""  when there is just the raw milliseconds value
        if (suffix.StartsWith("ms", c) || suffix.StartsWith("mil", c) || suffix == string.Empty)
        {
            result = TimeSpan.FromMilliseconds(number);
            return true;
        }

        if (suffix.StartsWith('s'))
        {
            result = TimeSpan.FromSeconds(number);
            return true;
        }

        if (suffix.StartsWith('m'))
        {
            result = TimeSpan.FromMinutes(number);
            return true;
        }

        if (suffix.StartsWith('h'))
        {
            result = TimeSpan.FromHours(number);
            return true;
        }

        if (suffix.StartsWith('d'))
        {
            result = TimeSpan.FromDays(number);
            return true;
        }

        result = TimeSpan.Zero;
        return false;
    }

    private static FormatException GetFormatException(string? value) => new($"The value '{value}' is not a valid time string. Use a time string in this format 5400000 / 5400000ms / 5400s / 90m");
}
