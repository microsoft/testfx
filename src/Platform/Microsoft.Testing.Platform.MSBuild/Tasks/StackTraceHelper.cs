// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.MSBuild;

internal static class StackTraceHelper
{
    private static readonly string[] NewLineSeparator = [Environment.NewLine];

    private static Regex? s_regex;

    internal static bool TryFindLocationFromStackFrame(string? errorStackTrace, [NotNullWhen(true)] out string? file, out int lineNumber, out string? place)
    {
        file = null;
        place = null;
        lineNumber = 0;

        if (errorStackTrace is null)
        {
            return false;
        }

        string[] stackFrames = errorStackTrace.Split(NewLineSeparator, StringSplitOptions.None);
        if (stackFrames.Length == 0)
        {
            return false;
        }

        // Take 20 frames at max, so we don't search 1000 items in a long stack trace.
        int limit = Math.Min(stackFrames.Length, 20);
        for (int i = 0; i < limit; i++)
        {
            if (TryGetStackFrameLocation(stackFrames[i], out lineNumber, out file, out place))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetStackFrameLocation(string stackFrame, out int line, [NotNullWhen(true)] out string? file, out string? place)
    {
        Regex regex = GetOrCreateRegex();

        // stack frame looks like this '   at Program.<Main>$(String[] args) in S:\t\ConsoleApp81\ConsoleApp81\Program.cs:line 9'
        Match match;
        try
        {
            match = regex.Match(stackFrame);
        }
        catch (RegexMatchTimeoutException)
        {
            line = 0;
            file = null;
            place = null;
            return false;
        }

        line = 0;
        file = null;
        place = null;

        bool hasLocation = match.Success && match.Groups["file"].Success && match.Groups["line"].Success;
        if (hasLocation)
        {
            // get the exact info from stack frame.
            Group code = match.Groups["code"];
            Group codeWithoutLocation = match.Groups["code1"];
            place = code.Success ? code.Value : codeWithoutLocation.Value;

            file = match.Groups["file"].Value;
            _ = int.TryParse(match.Groups["line"].Value, out line);
        }

        return hasLocation;
    }

    private static Regex GetOrCreateRegex()
    {
        Regex? regex = Volatile.Read(ref s_regex);
        if (regex is not null)
        {
            return regex;
        }

        // Keep this location-only pattern because MSBuild only reports frames that can provide a file and line.
        regex = new Regex(
            StackTraceRegexHelper.CreateFrameRegexPattern(matchFramesWithoutLocation: false),
            RegexOptions.Compiled,
            StackTraceRegexHelper.MatchTimeout);

        // Racing callers on first use may each build an instance. Publish the first one and return it to all of them,
        // so every caller observes the same object, and once it is published no caller builds the regex again.
        return Interlocked.CompareExchange(ref s_regex, regex, null) ?? regex;
    }
}
