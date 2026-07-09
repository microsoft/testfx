// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

namespace Microsoft.Testing.Extensions.Policy;

/// <summary>
/// Stateless string/list utilities used by the retry orchestrator and its collaborators.
/// </summary>
internal static class RetryOrchestratorHelper
{
    public static int GetOptionArgumentIndex(string optionName, string[] executableArgs)
    {
        int index = Array.IndexOf(executableArgs, "-" + optionName);
        if (index >= 0)
        {
            return index;
        }

        index = Array.IndexOf(executableArgs, "--" + optionName);
        return index >= 0 ? index : -1;
    }

    public static void RemoveOption(List<string> arguments, string optionName)
    {
        string longForm = $"--{optionName}";
        string shortForm = $"-{optionName}";

        // Remove all occurrences since options like --filter-uid can appear multiple times.
        // Also handle --option=value and --option:value forms produced by the command-line parser.
        while (true)
        {
            int idx = -1;
            for (int i = 0; i < arguments.Count; i++)
            {
                string arg = arguments[i];
                if (arg == longForm || arg == shortForm
                    || arg.StartsWith(longForm + "=", StringComparison.Ordinal) || arg.StartsWith(longForm + ":", StringComparison.Ordinal)
                    || arg.StartsWith(shortForm + "=", StringComparison.Ordinal) || arg.StartsWith(shortForm + ":", StringComparison.Ordinal))
                {
                    idx = i;
                    break;
                }
            }

            if (idx < 0)
            {
                break;
            }

            arguments.RemoveAt(idx);

            // Always remove subsequent non-option arguments (the option's values),
            // even when the first value was provided inline with = or :, because
            // multi-arity options (e.g. --filter-uid=1 2) can have trailing values.
            while (idx < arguments.Count && (arguments[idx].Length == 0 || arguments[idx][0] != '-'))
            {
                arguments.RemoveAt(idx);
            }
        }
    }

    // Renders a retry delay the same way the --retry-failed-tests-delay option accepts it (e.g. '500ms', '1s',
    // '1500ms') so the displayed wait is consistent with how the user configured it. The output is always a single
    // value + unit that TimeSpanParser round-trips, and uses InvariantCulture so it never varies by locale.
    public static string FormatDelay(TimeSpan delay)
        => delay.TotalMilliseconds < 1000
            ? string.Format(CultureInfo.InvariantCulture, "{0}ms", (int)delay.TotalMilliseconds)
            : delay.Milliseconds == 0
                ? string.Format(CultureInfo.InvariantCulture, "{0}s", (long)delay.TotalSeconds)
                : string.Format(CultureInfo.InvariantCulture, "{0}ms", (long)delay.TotalMilliseconds);

    // Compact, human-friendly duration for the retry summary, mirroring the platform terminal style
    // (e.g. '240ms', '1s 240ms', '2m 03s'). InvariantCulture keeps the numeric separators stable across locales.
    public static string FormatDuration(TimeSpan duration)
        => duration.TotalSeconds < 1
            ? string.Format(CultureInfo.InvariantCulture, "{0}ms", (int)duration.TotalMilliseconds)
            : duration.TotalMinutes < 1
                ? string.Format(CultureInfo.InvariantCulture, "{0}s {1:000}ms", duration.Seconds, duration.Milliseconds)
                : string.Format(CultureInfo.InvariantCulture, "{0}m {1:00}s", (int)duration.TotalMinutes, duration.Seconds);
}
