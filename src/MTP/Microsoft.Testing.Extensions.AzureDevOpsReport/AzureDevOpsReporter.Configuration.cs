// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsReporter
{
    private string GetConfiguredSeverity()
        => _commandLine.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.AzureDevOpsReportSeverity, out string[]? arguments)
            && arguments is [string configuredSeverity]
                ? configuredSeverity.ToLowerInvariant()
                : "error";

    private QuarantineFile? LoadQuarantineFile()
    {
        if (!_commandLine.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.AzureDevOpsQuarantineFile, out string[]? arguments)
            || arguments is not [string quarantineFilePath])
        {
            return null;
        }

        // NOTE: The value is treated as an explicit filesystem path supplied by the caller; this extension only validates existence and readability.
        if (!_fileSystem.ExistFile(quarantineFilePath))
        {
            _logger.LogWarning(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.QuarantineFileMissingWarning, quarantineFilePath));
            return null;
        }

        try
        {
            return new QuarantineFile(quarantineFilePath, _fileSystem, _logger);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.QuarantineFileLoadFailedWarning, quarantineFilePath, ex.Message));
            return null;
        }
    }

    private string GetSeverity(string testName, bool isQuarantined)
        => isQuarantined || (_demoteKnownFlaky && _historyService.IsLikelyFlaky(testName, KnownFlakyFailureRateThreshold))
            ? WarningSeverity
            : _severity ?? "error";

    private string BuildAnnotationSuffix(string testName, bool isQuarantined)
    {
        string? historyAnnotation = GetHistoryAnnotation(testName);
        if (historyAnnotation is null && !isQuarantined)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        if (historyAnnotation is not null)
        {
            builder.Append(' ').Append(historyAnnotation);
        }

        if (isQuarantined)
        {
            builder.Append(' ').Append(AzureDevOpsResources.QuarantinedAnnotation);
        }

        return builder.ToString();
    }

    private string? GetHistoryAnnotation(string testName)
        => !_historyService.TryGetStats(testName, out FlakyStats stats) || stats.TotalCount == 0
            ? null
            : stats.FailCount == 0
                ? stats.TotalCount >= MinSamplesForRegressionAnnotation
                    ? AzureDevOpsResources.FlakyHistoryRegressionAnnotation
                    : null
                : string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.FlakyHistoryFailureAnnotation, stats.FailCount, stats.TotalCount, _historyService.HistoryWindowInDays);

    private void EnsureEnabledConfigurationLoaded()
    {
        if (_hasLoadedEnabledConfiguration)
        {
            return;
        }

        _severity = GetConfiguredSeverity();
        _demoteKnownFlaky = _commandLine.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsDemoteKnownFlaky);
        _quarantineFile = LoadQuarantineFile();
        _userStackFrameFilters = LoadUserStackFrameFilters();
        _hasLoadedEnabledConfiguration = true;
    }

    private Regex[] LoadUserStackFrameFilters()
    {
        if (!_commandLine.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.AzureDevOpsStackFrameFilter, out string[]? patterns)
            || patterns is not { Length: > 0 })
        {
            return [];
        }

        var compiled = new List<Regex>(patterns.Length);
        foreach (string pattern in patterns)
        {
            try
            {
                compiled.Add(new Regex(
                    pattern,
                    RegexOptions.CultureInvariant | RegexOptions.Compiled,
                    TimeSpan.FromMilliseconds(AzureDevOpsCommandLineProvider.StackFrameFilterMatchTimeoutMs)));
            }
            catch (ArgumentException ex)
            {
                // Should have been caught at validation time, but log and skip if not.
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning($"Skipping invalid '--report-azdo-stackframe-filter' regex '{pattern}': {ex.Message}");
                }
            }
        }

        return [.. compiled];
    }
}
