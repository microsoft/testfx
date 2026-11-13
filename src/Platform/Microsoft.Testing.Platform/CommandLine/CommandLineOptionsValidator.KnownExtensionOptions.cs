// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.CommandLine;

internal static partial class CommandLineOptionsValidator
{
    // Keep in sync with public command-line providers that are automatically registered by the listed packages.
    // Packages without public options, options requiring explicit framework registration, and hidden internal options
    // are intentionally omitted.
    private static readonly Dictionary<string, string[]> KnownExtensionOptionsByPackage = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Microsoft.Testing.Extensions.AzureDevOpsReport"] =
        [
            "publish-azdo-run-name",
            "publish-azdo-test-results",
            "report-azdo",
            "report-azdo-annotations",
            "report-azdo-demote-known-flaky",
            "report-azdo-flaky-history",
            "report-azdo-groups",
            "report-azdo-quarantine-file",
            "report-azdo-severity",
            "report-azdo-slow-test-history",
            "report-azdo-slow-test-history-min-sample",
            "report-azdo-slow-test-history-multiplier",
            "report-azdo-stackframe-filter",
            "report-azdo-summary",
            "report-azdo-upload-artifact-exclude",
            "report-azdo-upload-artifact-include",
            "report-azdo-upload-artifact-name",
            "report-azdo-upload-artifacts",
        ],
        ["Microsoft.Testing.Extensions.CodeCoverage"] =
        [
            "coverage",
            "coverage-output",
            "coverage-output-format",
            "coverage-settings",
        ],
        ["Microsoft.Testing.Extensions.CrashDump"] =
        [
            "crash-report",
            "crash-report-if-supported",
            "crash-sequence",
            "crashdump",
            "crashdump-filename",
            "crashdump-type",
        ],
        ["Microsoft.Testing.Extensions.CtrfReport"] =
        [
            "report-ctrf",
            "report-ctrf-filename",
        ],
        ["Microsoft.Testing.Extensions.GitHubActionsReport"] =
        [
            "report-gh",
            "report-gh-annotations",
            "report-gh-attachments",
            "report-gh-failure-details",
            "report-gh-groups",
            "report-gh-slow-test-notices",
            "report-gh-slow-test-threshold",
            "report-gh-step-summary",
            "report-gh-step-summary-sections",
        ],
        ["Microsoft.Testing.Extensions.HangDump"] =
        [
            "hangdump",
            "hangdump-filename",
            "hangdump-timeout",
            "hangdump-type",
            "hangdump-type-if-supported",
        ],
        ["Microsoft.Testing.Extensions.HtmlReport"] =
        [
            "report-html",
            "report-html-filename",
        ],
        ["Microsoft.Testing.Extensions.JUnitReport"] =
        [
            "report-junit",
            "report-junit-filename",
        ],
        ["Microsoft.Testing.Extensions.Retry"] =
        [
            "retry-failed-tests",
            "retry-failed-tests-delay",
            "retry-failed-tests-max-percentage",
            "retry-failed-tests-max-tests",
        ],
        ["Microsoft.Testing.Extensions.TrxReport"] =
        [
            "report-trx",
            "report-trx-filename",
        ],
        ["Microsoft.Testing.Extensions.VideoRecorder"] =
        [
            "capture-video",
            "capture-video-args",
            "capture-video-chapters",
            "capture-video-granularity",
            "capture-video-max-duration",
            "capture-video-source",
        ],
    };

    private static void AppendMissingExtensionSuggestionIfNeeded(
        StringBuilder stringBuilder,
        HashSet<string> validOptionNames,
        string optionName,
        string packageName)
    {
        if (!validOptionNames.Contains(optionName))
        {
            AppendMissingExtensionSuggestion(stringBuilder, optionName, packageName);
        }
    }

    private static void AppendMissingExtensionSuggestion(StringBuilder stringBuilder, string optionName, string packageName)
        => stringBuilder.AppendLine(string.Format(
            CultureInfo.InvariantCulture,
            PlatformResources.CommandLineOptionRequiresExtension,
            optionName,
            packageName));

    private static string? GetKnownExtensionPackage(string optionName)
    {
        foreach (KeyValuePair<string, string[]> extensionOptions in KnownExtensionOptionsByPackage)
        {
            if (extensionOptions.Value.Contains(optionName, StringComparer.OrdinalIgnoreCase))
            {
                return extensionOptions.Key;
            }
        }

        return null;
    }
}
