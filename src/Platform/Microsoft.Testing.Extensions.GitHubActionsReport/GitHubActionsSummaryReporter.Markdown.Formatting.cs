// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

internal sealed partial class GitHubActionsSummaryReporter
{
    private static string FormatDuration(TimeSpan duration)
        => SummaryReporterHelpers.FormatDuration(duration, "{0}m {1:00}s", "{0}h {1:00}m {2:00}s");


    private static string EscapeInlineCode(string value)
        => RoslynString.IsNullOrEmpty(value)
            ? value
            : value.Replace("`", "'").Replace("\r", string.Empty).Replace("\n", " ");

    private static string HtmlEncode(string value)
        => System.Net.WebUtility.HtmlEncode(value);
}
