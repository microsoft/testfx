// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal static class StackTraceRegexHelper
{
    // Only the MSBuild caller applies this timeout (the Platform caller runs the regex with no timeout because the
    // pattern is linear). Note that Regex.Match measures wall-clock elapsed time, not CPU time, so a match can be
    // blamed for time spent in a GC pause, thread-pool starvation or a debugger stop. 1 second is chosen as a generous
    // upper bound that should never be reached by the linear pattern under normal conditions while still bounding the
    // worst case. Do not remove the RegexMatchTimeoutException catch in the MSBuild caller assuming "this never fires".
    internal const int MatchTimeoutMilliseconds = 1_000;

    internal static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(MatchTimeoutMilliseconds);

    internal static string CreateFrameRegexPattern(bool matchFramesWithoutLocation)
    {
        (string atString, string inPattern) = GetLocalizedStackFrameRegexParts();

        // atString comes from a localized resource (e.g. "at", "bei", "à") so it must be escaped in case a locale ever
        // produces a regex metacharacter. inPattern is deliberately built from regex fragments ((?<file>.+), (?<line>\d+))
        // and must NOT be escaped.
        string escapedAt = Regex.Escape(atString);

        return matchFramesWithoutLocation
            ? @$"^   {escapedAt} ((?<code>.+) {inPattern}|(?<code1>.+))$"
            : @$"^   {escapedAt} (?<code>.+) {inPattern}$";
    }

    private static (string AtString, string InPattern) GetLocalizedStackFrameRegexParts()
    {
        string atResourceName = "Word_At";
        string inResourceName = "StackTrace_InFileLineNumber";

        string? atString = null;
        string? inString = null;

        // Grab words from localized resource, in case the stack trace is localized.
        try
        {
            // Get these resources: https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/Resources/Strings.resx
#pragma warning disable RS0030 // Do not use banned APIs
            MethodInfo? getResourceStringMethod = typeof(Environment).GetMethod(
                "GetResourceString",
                BindingFlags.Static | BindingFlags.NonPublic, null, [typeof(string)], null);
#pragma warning restore RS0030 // Do not use banned APIs
            if (getResourceStringMethod is not null)
            {
                // <value>at</value>
                atString = (string?)getResourceStringMethod.Invoke(null, [atResourceName]);

                // <value>in {0}:line {1}</value>
                inString = (string?)getResourceStringMethod.Invoke(null, [inResourceName]);
            }
        }
        catch (Exception ex) when (ex is AmbiguousMatchException
            or TargetInvocationException
            or TargetParameterCountException
            or MemberAccessException
            or InvalidOperationException
            or NotSupportedException)
        {
            // If reflection lookup/invocation fails, populate the defaults below.
        }

        atString = atString is null || atString == atResourceName ? "at" : atString;
        inString = inString is null || inString == inResourceName ? "in {0}:line {1}" : inString;

        string inPattern = string.Format(CultureInfo.InvariantCulture, inString, "(?<file>.+)", @"(?<line>\d+)");
        return (atString, inPattern);
    }
}
