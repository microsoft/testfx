// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

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

        if (match.Success)
        {
            // get the exact info from stack frame.
            place = match.Groups["code"].Value;
            file = match.Groups["file"].Value;
            _ = int.TryParse(match.Groups["line"].Value, out line);
        }

        return match.Success;
    }

    [MemberNotNull(nameof(s_regex))]
    private static void InitializeRegex()
    {
        if (s_regex != null)
        {
            return;
        }

        string atResourceName = "Word_At";
        string inResourceName = "StackTrace_InFileLineNumber";

        string? atString = null;
        string? inString = null;

        // Grab words from localized resource, in case the stack trace is localized.
        try
        {
            // Get these resources: https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/Resources/Strings.resx
            MethodInfo? getResourceStringMethod = typeof(Environment).GetMethod("GetResourceString", BindingFlags.Static | BindingFlags.NonPublic, null, [typeof(string)], null);
            if (getResourceStringMethod is not null)
            {
                // <value>at</value>
                atString = (string?)getResourceStringMethod.Invoke(null, [atResourceName]);

                // <value>in {0}:line {1}</value>
                inString = (string?)getResourceStringMethod.Invoke(null, [inResourceName]);
            }
        }
        catch
        {
            // If we fail, populate the defaults below.
        }

        atString = atString == null || atString == atResourceName ? "at" : atString;
        inString = inString == null || inString == inResourceName ? "in {0}:line {1}" : inString;

        string inPattern = string.Format(CultureInfo.InvariantCulture, inString, "(?<file>.+)", @"(?<line>\d+)");

        s_regex = new Regex(@$"^   {atString} (?<code>.+) {inPattern}$", RegexOptions.Compiled, matchTimeout: TimeSpan.FromSeconds(1));
    }
}
