// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.CommandLine;

internal static partial class CommandLineOptionsValidator
{
    private static bool TryAppendVSTestOptionGuidance(
        StringBuilder stringBuilder,
        string unknownOptionName,
        IReadOnlyList<string> arguments,
        HashSet<string> validOptionNames)
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
            AppendVSTestLoggerGuidance(stringBuilder, value, validOptionNames);
        }
        else
        {
            AppendVSTestCollectGuidance(stringBuilder, value, validOptionNames);
        }

        return true;
    }

    private static void AppendVSTestLoggerGuidance(
        StringBuilder stringBuilder,
        string? value,
        HashSet<string> validOptionNames)
    {
        if (value is not null
            && IsVSTestNamedValue(value, "trx"))
        {
            string replacement = GetVSTestSubOptionValue(value, "LogFileName") is not null
                ? "'--report-trx', '--report-trx-filename <FILE>'"
                : "'--report-trx'";
            stringBuilder.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.CommandLineVSTestReplacementSuggestion,
                replacement));
            AppendMissingExtensionSuggestionIfNeeded(
                stringBuilder,
                validOptionNames,
                "report-trx",
                "Microsoft.Testing.Extensions.TrxReport");
            return;
        }

        if (value is not null
            && IsVSTestNamedValue(value, "console")
            && GetVSTestSubOptionValue(value, "verbosity") is { } verbosity
            && (verbosity.Equals("minimal", StringComparison.OrdinalIgnoreCase)
                || verbosity.Equals("normal", StringComparison.OrdinalIgnoreCase)
                || verbosity.Equals("detailed", StringComparison.OrdinalIgnoreCase)))
        {
            stringBuilder.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.CommandLineVSTestConsoleLoggerReplacement,
                verbosity.ToLowerInvariant()));
            return;
        }

        stringBuilder.AppendLine(PlatformResources.CommandLineVSTestLoggerGuidance);
    }

    private static void AppendVSTestCollectGuidance(
        StringBuilder stringBuilder,
        string? value,
        HashSet<string> validOptionNames)
    {
        if (value is not null
            && IsVSTestNamedValue(value, "Code Coverage"))
        {
            string replacement = GetVSTestSubOptionValue(value, "Format") is { } format
                ? $"'--coverage', '--coverage-output-format {format}'"
                : "'--coverage'";
            stringBuilder.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.CommandLineVSTestReplacementSuggestion,
                replacement));
            AppendMissingExtensionSuggestionIfNeeded(
                stringBuilder,
                validOptionNames,
                "coverage",
                "Microsoft.Testing.Extensions.CodeCoverage");
            return;
        }

        if (value is not null
            && IsVSTestNamedValue(value, "XPlat Code Coverage"))
        {
            stringBuilder.AppendLine(PlatformResources.CommandLineVSTestXPlatCoverageReplacement);
            AppendMissingExtensionSuggestionIfNeeded(
                stringBuilder,
                validOptionNames,
                "coverage",
                "Microsoft.Testing.Extensions.CodeCoverage");
            return;
        }

        if (value is not null
            && IsVSTestNamedValue(value, "blame"))
        {
            stringBuilder.AppendLine(PlatformResources.CommandLineVSTestBlameReplacement);
            AppendMissingExtensionSuggestionIfNeeded(
                stringBuilder,
                validOptionNames,
                "crashdump",
                "Microsoft.Testing.Extensions.CrashDump");
            AppendMissingExtensionSuggestionIfNeeded(
                stringBuilder,
                validOptionNames,
                "hangdump",
                "Microsoft.Testing.Extensions.HangDump");
            return;
        }

        stringBuilder.AppendLine(PlatformResources.CommandLineVSTestCollectorGuidance);
    }

    private static bool IsVSTestNamedValue(string value, string name)
        => value.Equals(name, StringComparison.OrdinalIgnoreCase)
            || value.StartsWith($"{name};", StringComparison.OrdinalIgnoreCase);

    private static string? GetVSTestSubOptionValue(string value, string subOptionName)
    {
        string prefix = $"{subOptionName}=";
        return value.Split(';').Skip(1).FirstOrDefault(segment =>
            segment.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && segment.Length > prefix.Length)?[prefix.Length..];
    }
}
