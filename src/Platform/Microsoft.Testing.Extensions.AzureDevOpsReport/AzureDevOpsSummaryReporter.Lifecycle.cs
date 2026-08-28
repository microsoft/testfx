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
                _records.Clear();
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

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_isEnabled || value is not TestNodeUpdateMessage update)
            {
                return Task.CompletedTask;
            }

            TestNodeStateProperty? state = update.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>();
            TerminalKind kind = SummaryReporterHelpers.GetTerminalKind(state);
            if (kind == TerminalKind.NotTerminal)
            {
                return Task.CompletedTask;
            }

            string uid = update.TestNode.Uid;
            string displayName = update.TestNode.DisplayName;

            // Single-pass collection of TimingProperty and the FQN SerializableKeyValuePairStringProperty:
            // replaces 1 × SingleOrDefault<TimingProperty>() + 1 × OfType<>().FirstOrDefault() with one
            // GetStructEnumerator() walk, saving 1 linked-list traversal and 1 LINQ allocation per terminal result.
            // Singleton-typed properties use the local GetSingleOrDefaultValue helper to preserve the
            // throw-on-duplicate invariant that SingleOrDefault<T>() provided; the FQN key keeps the
            // prior FirstOrDefault semantics (first match wins) so we don't silently overwrite earlier values.
            TimingProperty? timing = null;
            string? fqnValue = null;
            PropertyBag.PropertyBagEnumerator enumerator = update.TestNode.Properties.GetStructEnumerator();
            while (enumerator.MoveNext())
            {
                switch (enumerator.Current)
                {
                    case TimingProperty t: timing = GetSingleOrDefaultValue(timing, t); break;
                    case SerializableKeyValuePairStringProperty kv when kv.Key == FullyQualifiedNamePropertyKey && fqnValue is null:
                        fqnValue = kv.Value;
                        break;
                }
            }

            static TProperty GetSingleOrDefaultValue<TProperty>(TProperty? existingProperty, TProperty property)
                where TProperty : class, IProperty
                => existingProperty is not null
                    ? throw new InvalidOperationException($"Found multiple properties of type '{typeof(TProperty)}'.")
                    : property;

            string fullyQualifiedName = fqnValue ?? displayName;
            TimeSpan duration = timing?.GlobalTiming.Duration ?? TimeSpan.Zero;

            lock (_stateLock)
            {
                _records[uid] = new TestRecord(displayName, fullyQualifiedName, kind, duration);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogUnexpectedException(nameof(ConsumeAsync), ex);
        }

        return Task.CompletedTask;
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
            lock (_stateLock)
            {
                snapshot = [.. _records.Values];
            }

            string assemblyName = _testApplicationModuleInfo.TryGetAssemblyName() ?? "unknown";
            CiCoverageSummaryData coverage = CiCoverageSummary.Create(_testCoverageResult, testSessionContext.SessionUid);
            if (_shouldDeferToArtifactPostProcessing()
                && _configuration.GetTestResultDirectory() is { } resultsDirectory
                && !RoslynString.IsNullOrWhiteSpace(resultsDirectory))
            {
                CiRunSummaryModule module = CreateModule(snapshot, assemblyName, testSessionContext, coverage);
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

            string markdown = BuildMarkdown(snapshot, assemblyName, _targetFrameworkMoniker.Value, coverage);
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

            string line = $"##vso[task.uploadsummary]{AzDoEscaper.Escape(path)}";
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
        CiCoverageSummaryData coverage)
        => CiRunSummaryAggregation.CreateModule(
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

    private int GetAttemptNumber()
        => int.TryParse(
            _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out int attemptNumber)
            && attemptNumber > 0
                ? attemptNumber
                : 1;

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
}
