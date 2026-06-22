// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal static class StackTraceRegexHelper
{
    internal const int MatchTimeoutMilliseconds = 1_000;

    internal static TimeSpan MatchTimeout => TimeSpan.FromMilliseconds(MatchTimeoutMilliseconds);

    internal static string CreateFrameRegexPattern(bool matchFramesWithoutLocation)
    {
        (string atString, string inPattern) = GetLocalizedStackFrameRegexParts();

        return matchFramesWithoutLocation
            ? @$"^   {atString} ((?<code>.+) {inPattern}|(?<code1>.+))$"
            : @$"^   {atString} (?<code>.+) {inPattern}$";
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
        catch
        {
            // If we fail, populate the defaults below.
        }

        atString = atString is null || atString == atResourceName ? "at" : atString;
        inString = inString is null || inString == inResourceName ? "in {0}:line {1}" : inString;

        string inPattern = string.Format(CultureInfo.InvariantCulture, inString, "(?<file>.+)", @"(?<line>\d+)");
        return (atString, inPattern);
    }
}
