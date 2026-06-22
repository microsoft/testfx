// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal static partial class StackTraceHelper
{
#if NET7_0_OR_GREATER
    // Keep the location-less branch because terminal stack rendering preserves frames that do not include source locations.
    [GeneratedRegex(@"^   at ((?<code>.+) in (?<file>.+):line (?<line>\d+)|(?<code1>.+))$", RegexOptions.ExplicitCapture, StackTraceRegexHelper.MatchTimeoutMilliseconds)]
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

        // Keep the location-less branch because terminal stack rendering preserves frames that do not include source locations.
        s_regex = new Regex(
            StackTraceRegexHelper.CreateFrameRegexPattern(matchFramesWithoutLocation: true),
            RegexOptions.Compiled | RegexOptions.ExplicitCapture,
            StackTraceRegexHelper.MatchTimeout);
        return s_regex;
    }
#endif
}
