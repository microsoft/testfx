// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsSummaryReporter
{
    public async Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        try
        {
            testSessionContext.CancellationToken.ThrowIfCancellationRequested();

            _emitAzureDevOpsCommands = false;
            lock (_stateLock)
            {
                _rows.Clear();
                _finalRowCountsByUid.Clear();
                _flakyRowIndicesByUid.Clear();
                _inProcessFailedTests.Clear();
                _notRecoveredTests.Clear();
            }

            if (!_isEnabled)
            {
                return;
            }

            _emitAzureDevOpsCommands = AzureDevOpsConstants.IsRunningInAzureDevOps(_environment);
            if (_emitAzureDevOpsCommands)
            {
                return;
            }

            if (_logger.IsEnabled(LogLevel.Warning))
            {
                await _logger.LogWarningAsync(AzureDevOpsResources.SummaryRequiresTfBuildWarning).ConfigureAwait(false);
            }

            await _outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(AzureDevOpsResources.SummaryRequiresTfBuildWarning), testSessionContext.CancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogUnexpectedException(nameof(OnTestSessionStartingAsync), ex);
        }
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        try
        {
            testSessionContext.CancellationToken.ThrowIfCancellationRequested();

            if (!_emitAzureDevOpsCommands)
            {
                return;
            }

            List<TestRecord> snapshot;
            CiRunSummaryHistoryTest[] historyTests;
            CiRunSummaryDependency[] dependencies;
            lock (_stateLock)
            {
                SummaryRow[] rows =
                [
                    .. _rows
                        .OrderBy(static row => row.Record.FullyQualifiedName, StringComparer.Ordinal)
                        .ThenBy(static row => row.Record.DisplayName, StringComparer.Ordinal)
                        .ThenBy(static row => row.Uid, StringComparer.Ordinal),
                ];
                snapshot = [.. rows.Select(static row => row.Record)];
                historyTests =
                [
                    .. rows
                        .Select(static row => row.HistoryTest)
                        .Where(static historyTest => historyTest is not null)
                        .Select(static historyTest => historyTest!),
                ];
                dependencies =
                [
                    .. rows
                        .SelectMany(static row => row.Dependencies)
                        .OrderBy(static dependency => dependency.DependentFullyQualifiedName, StringComparer.Ordinal)
                        .ThenBy(static dependency => dependency.Prerequisite, StringComparer.Ordinal)
                        .ThenBy(static dependency => dependency.ProceedOnFailure)
                        .GroupBy(static dependency => (
                            dependency.DependentFullyQualifiedName,
                            dependency.Prerequisite,
                            dependency.ProceedOnFailure))
                        .Select(static group => group.First())
                        .Take(MaxCapturedDependencies),
                ];
            }

            string assemblyName = _testApplicationModuleInfo.TryGetAssemblyName() ?? "unknown";
            CiCoverageSummaryData coverage = CiCoverageSummary.Create(_testCoverageResult, testSessionContext.SessionUid);
            CiRunSummaryModule module = CreateModule(snapshot, assemblyName, testSessionContext, coverage, historyTests, dependencies);
            if (_shouldDeferToArtifactPostProcessing()
                && _configuration.GetTestResultDirectory() is { } resultsDirectory
                && !RoslynString.IsNullOrWhiteSpace(resultsDirectory))
            {
                string fragmentPath = await CiRunSummaryAggregation.WriteFragmentAsync(
                    resultsDirectory,
                    AzureDevOpsSummaryArtifactPostProcessor.Provider,
                    AzureDevOpsSummaryArtifactPostProcessor.ProviderSlug,
                    module).ConfigureAwait(false);
                await _messageBus.PublishAsync(
                    this,
                    new SessionFileArtifact(
                        testSessionContext.SessionUid,
                        new FileInfo(fragmentPath),
                        AzureDevOpsResources.DisplayName,
                        AzureDevOpsResources.Description,
                        AzureDevOpsSummaryArtifactPostProcessor.FragmentArtifactKind)).ConfigureAwait(false);
                return;
            }

            string markdown = BuildMarkdown(module);
            string? path = ResolveSummaryPath();
            if (path is null)
            {
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    await _logger.LogTraceAsync("Could not resolve Azure DevOps summary path.").ConfigureAwait(false);
                }

                return;
            }

            try
            {
                string? directory = Path.GetDirectoryName(path);
                if (!RoslynString.IsNullOrEmpty(directory) && !_fileSystem.ExistDirectory(directory))
                {
                    _fileSystem.CreateDirectory(directory!);
                }

                using IFileStream stream = _fileSystem.NewFileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(stream.Stream, new UTF8Encoding(false));
                await writer.WriteAsync(markdown).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                string warning = string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.SummaryWriteFailedWarning, path, ex.Message);
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    await _logger.LogWarningAsync(warning).ConfigureAwait(false);
                }

                await _outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(warning), testSessionContext.CancellationToken).ConfigureAwait(false);
                return;
            }

            string attachmentName = SanitizeAttachmentName(
                $"Test results - {assemblyName} ({_targetFrameworkMoniker.Value}, {RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()}, attempt {module.AttemptNumber.ToString(CultureInfo.InvariantCulture)}, session {module.SessionUid})");
            string line = $"##vso[task.addattachment type=Distributedtask.Core.Summary;name={AzDoEscaper.Escape(attachmentName)};]{AzDoEscaper.Escape(path)}";
            await _outputDevice.DisplayAsync(this, new AzureDevOpsCommandOutputDeviceData(line), testSessionContext.CancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogUnexpectedException(nameof(OnTestSessionFinishingAsync), ex);
        }
    }

    private CiRunSummaryModule CreateModule(
        IReadOnlyList<TestRecord> records,
        string assemblyName,
        ITestSessionContext testSessionContext,
        CiCoverageSummaryData coverage,
        CiRunSummaryHistoryTest[] historyTests,
        CiRunSummaryDependency[] dependencies)
    {
        CiRunSummaryModule module = CiRunSummaryAggregation.CreateModule(
            records,
            assemblyName,
            _testApplicationModuleInfo.GetCurrentTestApplicationFullPath(),
            _targetFrameworkMoniker.Value,
            RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
            _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID),
            testSessionContext.SessionUid.Value,
            GetAttemptNumber(),
            _testApplicationProcessExitCode.GetProcessExitCode(),
            ResolveExplicitSummaryPath(_commandLineOptions),
            coverage);
        module.HistoryTests = historyTests;
        module.Dependencies = dependencies;
        return module;
    }

    private string? ResolveSummaryPath()
    {
        if (_commandLineOptions.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.AzureDevOpsSummary, out string[]? arguments)
            && arguments is [string explicitPath]
            && !RoslynString.IsNullOrWhiteSpace(explicitPath))
        {
            return Path.GetFullPath(explicitPath);
        }

        string? configuredTestResultsDirectory = _configuration.GetTestResultDirectory();
        if (RoslynString.IsNullOrWhiteSpace(configuredTestResultsDirectory))
        {
            return null;
        }

        // Include the assembly name and process architecture in the default file name (matching the
        // <asm>_<tfm>_<arch> shape used by the TRX/HTML/JUnit reports) so that multiple test assemblies
        // that share the same target framework and TestResults directory (a common CI setup) don't all
        // resolve to the same path and race to write it, which surfaced as
        // "The process cannot access the file ... because it is being used by another process".
        string assemblyName = _testApplicationModuleInfo.TryGetAssemblyName() ?? "unknown";
        string architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();

        // Sanitize the whole file name (matching TRX/HTML/JUnit) so that unexpected characters in any segment
        // - including the target framework moniker or architecture, not just the assembly name - cannot produce
        // an invalid file name.
        string fileName = ReportFileNameSanitizer.ReplaceInvalidFileNameChars(string.Format(
            CultureInfo.InvariantCulture,
            DefaultSummaryFileNameFormat,
            assemblyName,
            _targetFrameworkMoniker.Value,
            architecture));
        return Path.GetFullPath(Path.Combine(configuredTestResultsDirectory!, fileName));
    }

    internal static string? ResolveExplicitSummaryPath(ICommandLineOptions commandLineOptions)
        => commandLineOptions.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.AzureDevOpsSummary, out string[]? arguments)
            && arguments is [string explicitPath]
            && !RoslynString.IsNullOrWhiteSpace(explicitPath)
                ? Path.GetFullPath(explicitPath)
                : null;

    private static string SanitizeAttachmentName(string value)
    {
        char[] invalidFileNameCharacters = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            bool isInvalid = character is '%' or ';' or ']' or '\r' or '\n'
                || Array.IndexOf(invalidFileNameCharacters, character) >= 0;
            builder.Append(isInvalid
                ? character is '\r' or '\n' ? ' ' : '_'
                : character);
        }

        return builder.ToString();
    }
}
