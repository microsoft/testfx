// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.MSBuild;

internal static class StackTraceHelper
{
    private static Regex? s_regex;

    internal static bool TryFindLocationFromStackFrame(string? errorStackTrace, [NotNullWhen(true)] out string? file, out int lineNumber, out string? place)
    {
        file = null;
        place = null;
        lineNumber = 0;

        if (errorStackTrace == null)
        {
            return false;
        }

        string[] stackFrames = Regex.Split(errorStackTrace, Environment.NewLine);
        if (stackFrames.Length == 0)
        {
            return false;
        }

        // Take 20 frames at max, so we don't search 1000 items in a long stack trace.
        foreach (string? stackFrame in stackFrames.Take(20))
        {
            if (TryGetStackFrameLocation(stackFrame, out lineNumber, out file, out place))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetStackFrameLocation(string stackFrame, out int line, [NotNullWhen(true)] out string? file, out string? place)
    {
        InitializeRegex();

        // stack frame looks like this '   at Program.<Main>$(String[] args) in S:\t\ConsoleApp81\ConsoleApp81\Program.cs:line 9'
        Match match = s_regex.Match(stackFrame);

        line = 0;
        file = null;
        place = null;

        bool hasLocation = match.Groups["file"].Success && match.Groups["line"].Success;
        if (hasLocation)
        {
            // get the exact info from stack frame.
            place = match.Groups["code"].Success ? match.Groups["code"].Value : match.Groups["code1"].Value;
            file = match.Groups["file"].Value;
            _ = int.TryParse(match.Groups["line"].Value, out line);
        }

        return hasLocation;
    }

    [MemberNotNull(nameof(s_regex))]
    private static void InitializeRegex()
    {
        if (s_regex != null)
        {
            return;
        }

        s_regex = new Regex(StackTraceRegexPatternFactory.CreateFramePattern(), RegexOptions.Compiled, matchTimeout: TimeSpan.FromSeconds(1));
    }
}
