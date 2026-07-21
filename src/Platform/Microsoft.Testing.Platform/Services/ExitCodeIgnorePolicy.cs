// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Services;

internal static class ExitCodeIgnorePolicy
{
    /// <summary>
    /// Applies the shared <c>--ignore-exit-code</c> / <c>TESTINGPLATFORM_EXITCODE_IGNORE</c> policy to the
    /// given exit code, returning <see cref="ExitCode.Success"/> when the code is in the ignore list.
    /// </summary>
    /// <remarks>
    /// Kept as a single shared implementation so every exit-code verdict (test results, coverage threshold,
    /// ...) is filtered consistently. Verdicts that are computed after
    /// <see cref="ITestApplicationProcessExitCode.GetProcessExitCode"/> has already run (for example the
    /// coverage threshold verdict applied by the hosts) must be routed through this so they can be ignored
    /// the same way the built-in verdicts are.
    /// </remarks>
    public static int Apply(int exitCode, ICommandLineOptions commandLineOptions, IEnvironment environment)
    {
        // If the user has specified the IgnoreExitCode, then we don't want to return a non-zero exit code if the exit code matches the one specified.
        string? exitCodeToIgnore = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_EXITCODE_IGNORE);
        if (RoslynString.IsNullOrEmpty(exitCodeToIgnore))
        {
            if (commandLineOptions.TryGetOptionArgumentList(PlatformCommandLineProvider.IgnoreExitCodeOptionKey, out string[]? commandLineExitCodes) && commandLineExitCodes.Length > 0)
            {
                exitCodeToIgnore = commandLineExitCodes[0];
            }
        }

        if (exitCodeToIgnore is not null)
        {
            if (ContainsExitCode(exitCodeToIgnore, exitCode))
            {
                exitCode = (int)ExitCode.Success;
            }
        }

        return exitCode;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="exitCodeToIgnore"/> contains <paramref name="exitCode"/>
    /// in its ';'-delimited list, without allocating a <see cref="string"/> array, a substring, or a LINQ closure.
    /// </summary>
    /// <remarks>
    /// Each segment is parsed using the invariant integer format (optional leading/trailing whitespace and an optional
    /// ASCII <c>+</c>/<c>-</c> sign). Exit codes are a machine-level contract supplied via the <c>--ignore-exit-code</c>
    /// option or the <c>TESTINGPLATFORM_EXITCODE_IGNORE</c> environment variable, so parsing is intentionally
    /// culture-invariant and identical on every target framework.
    /// </remarks>
    private static bool ContainsExitCode(string exitCodeToIgnore, int exitCode)
    {
        int start = 0;
        while (start <= exitCodeToIgnore.Length)
        {
            int separatorIndex = exitCodeToIgnore.IndexOf(';', start);
            int end = separatorIndex < 0 ? exitCodeToIgnore.Length : separatorIndex;

#if NETCOREAPP
            if (int.TryParse(exitCodeToIgnore.AsSpan(start, end - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedExitCode) && parsedExitCode == exitCode)
#else
            if (TryParseExitCode(exitCodeToIgnore, start, end, out int parsedExitCode) && parsedExitCode == exitCode)
#endif
            {
                return true;
            }

            if (separatorIndex < 0)
            {
                break;
            }

            start = separatorIndex + 1;
        }

        return false;
    }

#if !NETCOREAPP
    /// <summary>
    /// Parses the <c>[start, end)</c> slice of <paramref name="value"/> as an <see cref="int"/> without allocating a
    /// substring. Mirrors the invariant <c>int.TryParse(ReadOnlySpan&lt;char&gt;, NumberStyles.Integer, CultureInfo.InvariantCulture, out int)</c>
    /// used by the <c>NETCOREAPP</c> branch: optional leading/trailing whitespace and an optional leading ASCII
    /// <c>+</c>/<c>-</c> sign.
    /// </summary>
    private static bool TryParseExitCode(string value, int start, int end, out int result)
    {
        result = 0;

        while (start < end && char.IsWhiteSpace(value[start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(value[end - 1]))
        {
            end--;
        }

        if (start >= end)
        {
            return false;
        }

        bool isNegative = value[start] == '-';
        if (value[start] is '+' or '-')
        {
            start++;
        }

        if (start >= end)
        {
            return false;
        }

        // int.MinValue is -2147483648, so the negative branch must accept a magnitude one larger than int.MaxValue.
        long limit = isNegative ? -(long)int.MinValue : int.MaxValue;
        long accumulated = 0;
        for (int i = start; i < end; i++)
        {
            char c = value[i];
            if (c is < '0' or > '9')
            {
                return false;
            }

            int digit = c - '0';

            // Guard before multiplying so an arbitrarily long token cannot overflow and wrap into a valid value.
            if (accumulated > (limit - digit) / 10)
            {
                return false;
            }

            accumulated = (accumulated * 10) + digit;
        }

        result = (int)(isNegative ? -accumulated : accumulated);
        return true;
    }
#endif
}
