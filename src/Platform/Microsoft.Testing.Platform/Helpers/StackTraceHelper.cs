// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal static partial class StackTraceHelper
{
#if NET7_0_OR_GREATER
    // NOTE: GeneratedRegex requires a compile-time-constant pattern, so localization via
    // StackTraceRegexHelper.CreateFrameRegexPattern()/GetLocalizedStackFrameRegexParts() is intentionally not used on
    // this path. Pre-NET7 targets fall back to the #else branch which does attempt localization.
    // Specifying no timeout, the regex is linear. And the timeout does not measure the regex only, but measures also any
    // thread suspends, so the regex gets blamed incorrectly.
    [GeneratedRegex(@"^   at ((?<code>.+) in (?<file>.+):line (?<line>\d+)|(?<code1>.+))$", RegexOptions.ExplicitCapture)]
    public static partial Regex GetFrameRegex();
#else
    private static Regex? s_regex;

    [MemberNotNull(nameof(s_regex))]
    public static Regex GetFrameRegex()
    {
        if (s_regex is not null)
        {
            return s_regex;
        }

        // Specifying no timeout, the regex is linear. And the timeout does not measure the regex only, but measures also any
        // thread suspends, so the regex gets blamed incorrectly.
        s_regex = new Regex(
            StackTraceRegexHelper.CreateFrameRegexPattern(matchFramesWithoutLocation: true),
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        return s_regex;
    }
#endif
}
