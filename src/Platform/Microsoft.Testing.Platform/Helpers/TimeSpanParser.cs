// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.Helpers;

[Embedded]
internal enum TimeSpanDefaultUnit
{
    Milliseconds,
    Seconds,
    Minutes,
    Hours,
    Days,
}

[Embedded]
internal static partial class TimeSpanParser
{
    private static readonly Regex Pattern = GetRegex();

#if NET7_0_OR_GREATER
    [GeneratedRegex(@"^(?<value>\d+(?:\.\d+)?)(?:\s*(?<suffix>ms|mils?|milliseconds?|s|secs?|seconds?|m|mins?|minutes?|h|hours?|d|days?))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GetRegex();
#else
    private static Regex GetRegex() => new(@"^(?<value>\d+(?:\.\d+)?)(?:\s*(?<suffix>ms|mils?|milliseconds?|s|secs?|seconds?|m|mins?|minutes?|h|hours?|d|days?))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
#endif

    public static TimeSpan Parse(string? time) => Parse(time, TimeSpanDefaultUnit.Milliseconds);

    public static TimeSpan Parse(string? time, TimeSpanDefaultUnit defaultUnit)
        => TryParse(time, defaultUnit, out TimeSpan result) ? result : throw GetFormatException(time, defaultUnit, requireSuffix: false);

    public static TimeSpan ParseRequireSuffix(string? time)
        => TryParseRequireSuffix(time, out TimeSpan result) ? result : throw GetFormatException(time, defaultUnit: null, requireSuffix: true);

    public static bool TryParse(string? time, out TimeSpan result)
        => TryParse(time, TimeSpanDefaultUnit.Milliseconds, out result);

    public static bool TryParse(string? time, TimeSpanDefaultUnit defaultUnit, out TimeSpan result)
        => TryParseCore(time, defaultUnit, requireSuffix: false, out result);

    /// <summary>
    /// Parses a time value. Inputs without an explicit unit suffix (e.g. a bare number like "200")
    /// are rejected. Use this overload for options where the unit must be explicit.
    /// </summary>
    public static bool TryParseRequireSuffix(string? time, out TimeSpan result)
        => TryParseCore(time, defaultUnit: TimeSpanDefaultUnit.Milliseconds, requireSuffix: true, out result);

    private static bool TryParseCore(string? time, TimeSpanDefaultUnit defaultUnit, bool requireSuffix, out TimeSpan result)
    {
        if (RoslynString.IsNullOrWhiteSpace(time))
        {
            result = TimeSpan.Zero;
            return false;
        }

        Match match = Pattern.Match(time);
        if (!match.Success)
        {
            result = TimeSpan.Zero;
            return false;
        }

        string value = match.Groups["value"].Value;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
        {
            result = TimeSpan.Zero;
            return false;
        }

        string suffix = match.Groups["suffix"].Value;

        // No suffix: dispatch on the caller-provided default unit (or reject if a suffix is required).
        if (suffix.Length == 0)
        {
            if (requireSuffix)
            {
                result = TimeSpan.Zero;
                return false;
            }

            return TryFromUnit(number, defaultUnit, out result);
        }

        StringComparison c = StringComparison.OrdinalIgnoreCase;

        // "ms"/"mil"/"millisecond[s]" all map to milliseconds and are checked first to
        // disambiguate from the "m" (minutes) prefix.
        if (suffix.StartsWith("ms", c) || suffix.StartsWith("mil", c))
        {
            return TryCreateTimeSpan(TimeSpan.FromMilliseconds, number, out result);
        }

        if (suffix.StartsWith("s", c))
        {
            return TryCreateTimeSpan(TimeSpan.FromSeconds, number, out result);
        }

        if (suffix.StartsWith("m", c))
        {
            return TryCreateTimeSpan(TimeSpan.FromMinutes, number, out result);
        }

        if (suffix.StartsWith("h", c))
        {
            return TryCreateTimeSpan(TimeSpan.FromHours, number, out result);
        }

        if (suffix.StartsWith("d", c))
        {
            return TryCreateTimeSpan(TimeSpan.FromDays, number, out result);
        }

        result = TimeSpan.Zero;
        return false;
    }

    private static bool TryFromUnit(double number, TimeSpanDefaultUnit unit, out TimeSpan result)
        => unit switch
        {
            TimeSpanDefaultUnit.Milliseconds => TryCreateTimeSpan(TimeSpan.FromMilliseconds, number, out result),
            TimeSpanDefaultUnit.Seconds => TryCreateTimeSpan(TimeSpan.FromSeconds, number, out result),
            TimeSpanDefaultUnit.Minutes => TryCreateTimeSpan(TimeSpan.FromMinutes, number, out result),
            TimeSpanDefaultUnit.Hours => TryCreateTimeSpan(TimeSpan.FromHours, number, out result),
            TimeSpanDefaultUnit.Days => TryCreateTimeSpan(TimeSpan.FromDays, number, out result),
            _ => Fail(out result),
        };

    private static bool Fail(out TimeSpan result)
    {
        result = TimeSpan.Zero;
        return false;
    }

    private static bool TryCreateTimeSpan(Func<double, TimeSpan> factory, double number, out TimeSpan result)
    {
        try
        {
            result = factory(number);
            return true;
        }
        catch (OverflowException)
        {
            result = TimeSpan.Zero;
            return false;
        }
        catch (ArgumentException)
        {
            result = TimeSpan.Zero;
            return false;
        }
    }

    private static FormatException GetFormatException(string? value, TimeSpanDefaultUnit? defaultUnit, bool requireSuffix)
        => new($"The value '{value}' is not a valid time string. {GetGrammarHint(defaultUnit, requireSuffix)}");

    private static string GetGrammarHint(TimeSpanDefaultUnit? defaultUnit, bool requireSuffix)
    {
        const string SuffixGrammar = "Accepted suffixes are 'ms'/'mil(s)'/'millisecond(s)', 's'/'sec(s)'/'second(s)', 'm'/'min(s)'/'minute(s)', 'h'/'hour(s)', and 'd'/'day(s)', e.g. '500ms', '5400s', '90m', '1.5h', '1d'.";
        return requireSuffix || defaultUnit is null
            ? SuffixGrammar + " A unit suffix is required."
            : SuffixGrammar + $" A bare number defaults to {defaultUnit.Value.ToString().ToLowerInvariant()}.";
    }
}
