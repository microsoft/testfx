// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal static partial class StackTraceHelper
{
#if NET7_0_OR_GREATER
    // Specifying no timeout, the regex is linear. And the timeout does not measure the regex only, but measures also any
    // thread suspends, so the regex gets blamed incorrectly.
    [GeneratedRegex(@"^   at ((?<code>.+) in (?<file>.+):line (?<line>\d+)|(?<code1>.+))$", RegexOptions.ExplicitCapture)]
    public static partial Regex GetFrameRegex();
#else
    private static Regex? s_regex;

    [MemberNotNull(nameof(s_regex))]
    public static Regex GetFrameRegex()
    {
        if (s_regex != null)
        {
            return s_regex;
        }

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

        atString = atString == null || atString == atResourceName ? "at" : atString;
        inString = inString == null || inString == inResourceName ? "in {0}:line {1}" : inString;

        string inPattern = string.Format(CultureInfo.InvariantCulture, inString, "(?<file>.+)", @"(?<line>\d+)");

        // Specifying no timeout, the regex is linear. And the timeout does not measure the regex only, but measures also any
        // thread suspends, so the regex gets blamed incorrectly.
        s_regex = new Regex(@$"^   {atString} ((?<code>.+) {inPattern}|(?<code1>.+))$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        return s_regex;
    }
#endif
}
