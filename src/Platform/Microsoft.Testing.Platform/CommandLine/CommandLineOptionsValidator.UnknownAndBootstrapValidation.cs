// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
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

    private static ValidationResult ValidateNoUnknownOptions(
        CommandLineParseResult parseResult,
        IReadOnlyList<JsonCommandLineOptionEntry>? jsonCommandLineOptions,
        Dictionary<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> extensionOptionsByProvider,
        Dictionary<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> systemOptionsByProvider)
    {
        // Use OrdinalIgnoreCase so a JSON entry like "Timeout" resolves to the registered "timeout"
        // option (testconfig.json keys are case-insensitive everywhere else in the platform). CLI
        // parsing is already case-sensitive but a case-insensitive lookup is a strict superset and
        // does not change CLI behavior.
        var validOptionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visibleOptionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool includeKnownExtensionOptions = !parseResult.HasTool;
        CollectOptionNames(extensionOptionsByProvider, validOptionNames, visibleOptionNames);
        CollectOptionNames(systemOptionsByProvider, validOptionNames, visibleOptionNames);

        StringBuilder? stringBuilder = null;
        foreach (CommandLineParseOption optionRecord in parseResult.Options)
        {
            if (!validOptionNames.Contains(optionRecord.Name))
            {
                stringBuilder ??= new();
                AppendUnknownOptionError(stringBuilder, optionRecord.Name, optionRecord.Arguments, validOptionNames, visibleOptionNames, includeKnownExtensionOptions);
            }
        }

        // Also surface unknown entries under the testconfig.json "commandLineOptions" section.
        // We intentionally validate even when the CLI provides a matching option of the same name
        // (which would shadow the JSON value at lookup time): a JSON typo silently overridden by
        // the CLI is still a typo that the user wants to know about.
        if (jsonCommandLineOptions is { Count: > 0 })
        {
            foreach (JsonCommandLineOptionEntry entry in jsonCommandLineOptions)
            {
                if (!validOptionNames.Contains(entry.OptionName))
                {
                    stringBuilder ??= new();
                    StringBuilder innerErrorBuilder = new();
                    AppendUnknownOptionError(innerErrorBuilder, entry.OptionName, entry.Arguments, validOptionNames, visibleOptionNames, includeKnownExtensionOptions);
                    string innerError = innerErrorBuilder.ToTrimmedString();
                    stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.JsonCommandLineOptionsValidationErrorPrefix, innerError));
                }
            }
        }

        if (stringBuilder?.Length > 0)
        {
            stringBuilder.AppendLine(PlatformResources.CommandLineUnknownOptionsHint);
        }

        return stringBuilder?.Length > 0
            ? ValidationResult.Invalid(stringBuilder.ToTrimmedString())
            : ValidationResult.Valid();
    }

    private static void CollectOptionNames(
        Dictionary<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> optionsByProvider,
        HashSet<string> validOptionNames,
        HashSet<string> visibleOptionNames)
    {
        foreach (KeyValuePair<ICommandLineOptionsProvider, IReadOnlyCollection<CommandLineOption>> provider in optionsByProvider)
        {
            foreach (CommandLineOption option in provider.Value)
            {
                validOptionNames.Add(option.Name);
                if (!option.IsHidden)
                {
                    visibleOptionNames.Add(option.Name);
                }
            }
        }
    }

    private static void AppendUnknownOptionError(
        StringBuilder stringBuilder,
        string unknownOptionName,
        IReadOnlyList<string> arguments,
        HashSet<string> validOptionNames,
        HashSet<string> visibleOptionNames,
        bool includeKnownExtensionOptions)
    {
        stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineUnknownOption, unknownOptionName));

        if (includeKnownExtensionOptions
            && TryAppendVSTestOptionGuidance(stringBuilder, unknownOptionName, arguments))
        {
            return;
        }

        if (includeKnownExtensionOptions
            && GetKnownExtensionPackage(unknownOptionName) is { } packageName)
        {
            AppendMissingExtensionSuggestion(stringBuilder, unknownOptionName, packageName);
            return;
        }

        IEnumerable<string> candidateOptionNames = includeKnownExtensionOptions
            ? visibleOptionNames.Concat(KnownExtensionOptionsByPackage.Values.SelectMany(static optionNames => optionNames))
            : visibleOptionNames;
        string? suggestedOptionName = FindSuggestedOption(
            unknownOptionName,
            candidateOptionNames);
        if (suggestedOptionName is null)
        {
            return;
        }

        stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineOptionSuggestion, suggestedOptionName));

        if (includeKnownExtensionOptions
            && GetKnownExtensionPackage(suggestedOptionName) is { } suggestedPackageName
            && !validOptionNames.Contains(suggestedOptionName))
        {
            AppendMissingExtensionSuggestion(stringBuilder, suggestedOptionName, suggestedPackageName);
        }
    }

    private static bool TryAppendVSTestOptionGuidance(
        StringBuilder stringBuilder,
        string unknownOptionName,
        IReadOnlyList<string> arguments)
    {
        bool isLogger = unknownOptionName.Equals("logger", StringComparison.OrdinalIgnoreCase);
        bool isCollect = unknownOptionName.Equals("collect", StringComparison.OrdinalIgnoreCase);
        if (!isLogger && !isCollect)
        {
            return false;
        }

        stringBuilder.AppendLine(string.Format(
            CultureInfo.InvariantCulture,
            PlatformResources.CommandLineVSTestOptionUnsupported,
            unknownOptionName));

        string? value = arguments.Count == 1 ? arguments[0] : null;
        if (isLogger)
        {
            AppendVSTestLoggerGuidance(stringBuilder, value);
        }
        else
        {
            AppendVSTestCollectGuidance(stringBuilder, value);
        }

        return true;
    }

    private static void AppendVSTestLoggerGuidance(StringBuilder stringBuilder, string? value)
    {
        if (value is not null
            && (value.Equals("trx", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("trx;", StringComparison.OrdinalIgnoreCase)))
        {
            string replacement = value.Split(';').Skip(1).Any(static segment =>
                segment.StartsWith("LogFileName=", StringComparison.OrdinalIgnoreCase)
                && segment.Length > "LogFileName=".Length)
                ? "'--report-trx' and '--report-trx-filename <FILE>'"
                : "'--report-trx'";
            stringBuilder.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.CommandLineVSTestReplacementSuggestion,
                replacement));
            AppendMissingExtensionSuggestion(stringBuilder, "report-trx", "Microsoft.Testing.Extensions.TrxReport");
            return;
        }

        if (value is not null
            && value.StartsWith("console;", StringComparison.OrdinalIgnoreCase)
            && value.Split(';').Skip(1).FirstOrDefault(static segment =>
                segment.StartsWith("verbosity=", StringComparison.OrdinalIgnoreCase)) is { } verbositySegment)
        {
            string verbosity = verbositySegment.Substring("verbosity=".Length);
            if (verbosity.Equals("minimal", StringComparison.OrdinalIgnoreCase)
                || verbosity.Equals("normal", StringComparison.OrdinalIgnoreCase)
                || verbosity.Equals("detailed", StringComparison.OrdinalIgnoreCase))
            {
                stringBuilder.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.CommandLineVSTestConsoleLoggerReplacement,
                    verbosity.ToLowerInvariant()));
                return;
            }
        }

        stringBuilder.AppendLine(PlatformResources.CommandLineVSTestLoggerGuidance);
    }

    private static void AppendVSTestCollectGuidance(StringBuilder stringBuilder, string? value)
    {
        if (value?.Equals("Code Coverage", StringComparison.OrdinalIgnoreCase) == true)
        {
            stringBuilder.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.CommandLineVSTestReplacementSuggestion,
                "'--coverage'"));
            AppendMissingExtensionSuggestion(stringBuilder, "coverage", "Microsoft.Testing.Extensions.CodeCoverage");
            return;
        }

        if (value?.Equals("XPlat Code Coverage", StringComparison.OrdinalIgnoreCase) == true)
        {
            stringBuilder.AppendLine(PlatformResources.CommandLineVSTestXPlatCoverageReplacement);
            AppendMissingExtensionSuggestion(stringBuilder, "coverage", "Microsoft.Testing.Extensions.CodeCoverage");
            return;
        }

        if (value is not null
            && (value.Equals("blame", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("blame;", StringComparison.OrdinalIgnoreCase)))
        {
            stringBuilder.AppendLine(PlatformResources.CommandLineVSTestBlameReplacement);
            AppendMissingExtensionSuggestion(stringBuilder, "crashdump", "Microsoft.Testing.Extensions.CrashDump");
            AppendMissingExtensionSuggestion(stringBuilder, "hangdump", "Microsoft.Testing.Extensions.HangDump");
            return;
        }

        stringBuilder.AppendLine(PlatformResources.CommandLineVSTestCollectorGuidance);
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

    private static string? FindSuggestedOption(string unknownOptionName, IEnumerable<string> candidateOptionNames)
    {
        int maximumDistance = unknownOptionName.Length switch
        {
            <= 4 => 1,
            <= 12 => 2,
            _ => 3,
        };

        string? bestCandidate = null;
        int bestDistance = maximumDistance + 1;
        bool hasAmbiguousBestCandidate = false;

        foreach (string candidateOptionName in candidateOptionNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Math.Abs(candidateOptionName.Length - unknownOptionName.Length) > maximumDistance)
            {
                continue;
            }

            int distance = CalculateEditDistance(unknownOptionName, candidateOptionName);
            if (distance < bestDistance)
            {
                bestCandidate = candidateOptionName;
                bestDistance = distance;
                hasAmbiguousBestCandidate = false;
            }
            else if (distance == bestDistance)
            {
                hasAmbiguousBestCandidate = true;
            }
        }

        return bestDistance <= maximumDistance && !hasAmbiguousBestCandidate
            ? bestCandidate
            : null;
    }

    private static int CalculateEditDistance(string source, string target)
    {
        int[,] distances = new int[source.Length + 1, target.Length + 1];
        for (int i = 0; i <= source.Length; i++)
        {
            distances[i, 0] = i;
        }

        for (int j = 0; j <= target.Length; j++)
        {
            distances[0, j] = j;
        }

        for (int i = 1; i <= source.Length; i++)
        {
            for (int j = 1; j <= target.Length; j++)
            {
                int substitutionCost = char.ToUpperInvariant(source[i - 1]) == char.ToUpperInvariant(target[j - 1]) ? 0 : 1;
                distances[i, j] = Math.Min(
                    Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                    distances[i - 1, j - 1] + substitutionCost);

                if (i > 1
                    && j > 1
                    && char.ToUpperInvariant(source[i - 1]) == char.ToUpperInvariant(target[j - 2])
                    && char.ToUpperInvariant(source[i - 2]) == char.ToUpperInvariant(target[j - 1]))
                {
                    distances[i, j] = Math.Min(distances[i, j], distances[i - 2, j - 2] + 1);
                }
            }
        }

        return distances[source.Length, target.Length];
    }

    private static ValidationResult ValidateNoBootstrapOnlyOptionsInJson(
        IReadOnlyList<JsonCommandLineOptionEntry>? jsonCommandLineOptions)
    {
        if (jsonCommandLineOptions is not { Count: > 0 })
        {
            return ValidationResult.Valid();
        }

        StringBuilder? stringBuilder = null;
        foreach (JsonCommandLineOptionEntry entry in jsonCommandLineOptions)
        {
            if (!BootstrapOnlyOptions.Contains(entry.OptionName))
            {
                continue;
            }

            stringBuilder ??= new();
            string innerError = string.Format(CultureInfo.InvariantCulture, PlatformResources.JsonCommandLineOptionIsBootstrapOnlyErrorMessage, entry.OptionName);
            stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.JsonCommandLineOptionsValidationErrorPrefix, innerError));
        }

        return stringBuilder?.Length > 0
            ? ValidationResult.Invalid(stringBuilder.ToTrimmedString())
            : ValidationResult.Valid();
    }
}
