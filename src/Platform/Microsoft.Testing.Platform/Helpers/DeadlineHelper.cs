// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.Helpers;

/// <summary>
/// Reads the CI-imposed hard-cancel deadline and its associated margins from the environment.
/// The deadline is exported by the CI system (or Arcade) as an absolute wall-clock instant so
/// that both the in-process test host and the out-of-process test host controller can schedule
/// their reactions (graceful stop, hang dump) backwards from the same instant.
/// </summary>
[Embedded]
internal static class DeadlineHelper
{
    // Prototype defaults. stopMargin > dumpMargin so the graceful stop is attempted first and the
    // hang dump is the fallback for a host that did not stop in time.
    private static readonly TimeSpan DefaultStopMargin = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan DefaultDumpMargin = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Attempts to read <see cref="EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE"/> and parse
    /// it as an absolute instant in UTC.
    /// </summary>
    public static bool TryGetDeadline(IEnvironment environment, out DateTimeOffset deadlineUtc)
    {
        deadlineUtc = default;
        string? raw = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE);
        if (RoslynString.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (!DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset parsed))
        {
            return false;
        }

        deadlineUtc = parsed.ToUniversalTime();
        return true;
    }

    /// <summary>
    /// Gets the lead time before the deadline at which the platform should gracefully stop scheduling
    /// new tests. Reads <see cref="EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE_STOP_MARGIN"/>
    /// (bare numbers are seconds); falls back to a default when unset or unparsable.
    /// </summary>
    public static TimeSpan GetStopMargin(IEnvironment environment)
        => GetMargin(environment, EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE_STOP_MARGIN, DefaultStopMargin);

    /// <summary>
    /// Gets the lead time before the deadline at which the platform should take a hang dump. Reads
    /// <see cref="EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE_DUMP_MARGIN"/> (bare numbers
    /// are seconds); falls back to a default when unset or unparsable.
    /// </summary>
    public static TimeSpan GetDumpMargin(IEnvironment environment)
        => GetMargin(environment, EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE_DUMP_MARGIN, DefaultDumpMargin);

    /// <summary>
    /// Subtracts <paramref name="margin"/> from <paramref name="instant"/>, clamping the result at
    /// <see cref="DateTimeOffset.MinValue"/> instead of throwing when the subtraction would underflow.
    /// A very old (but valid) deadline, or a large margin, could otherwise overflow while computing
    /// the stop/dump instant. Saturating means "this instant is already in the past", which for both
    /// callers translates to "act immediately".
    /// </summary>
    public static DateTimeOffset SubtractSaturating(DateTimeOffset instant, TimeSpan margin)
        => margin > instant - DateTimeOffset.MinValue
            ? DateTimeOffset.MinValue
            : instant - margin;

    private static TimeSpan GetMargin(IEnvironment environment, string variableName, TimeSpan defaultValue)
    {
        string? raw = environment.GetEnvironmentVariable(variableName);
        return !RoslynString.IsNullOrWhiteSpace(raw)
            && TimeSpanParser.TryParse(raw, TimeSpanDefaultUnit.Seconds, out TimeSpan parsed)
            && parsed >= TimeSpan.Zero
                ? parsed
                : defaultValue;
    }
}
