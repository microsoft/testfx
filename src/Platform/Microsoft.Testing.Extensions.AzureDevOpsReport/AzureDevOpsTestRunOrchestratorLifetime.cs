// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security;

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

/// <summary>
/// Owns the Azure DevOps test run for the whole orchestrated execution, so that a run spanning several
/// test host processes is created once and completed once.
/// </summary>
/// <remarks>
/// <para>
/// An orchestrator (today: <c>--retry-failed-tests</c>) runs each attempt in its own test host process,
/// each of which used to create and complete its own Azure DevOps run. That produced one run per attempt
/// and left a test rescued by a retry recorded as failed forever in the earlier run (see
/// <see href="https://github.com/microsoft/testfx/issues/10360"/>).
/// </para>
/// <para>
/// The run's lifetime therefore has to belong to a process that outlives every attempt, and the
/// orchestrator process is the only one that qualifies: attempts are sequential and an attempt cannot
/// know whether it is the last one, because the retry options are stripped from its command line. This
/// lifetime is only built when an orchestrator is active, so it is inert for a normal run — and it is
/// deliberately orchestrator-agnostic, taking no dependency on the retry extension.
/// </para>
/// </remarks>
internal sealed class AzureDevOpsTestRunOrchestratorLifetime : ITestHostOrchestratorApplicationLifetime, IOutputDeviceDataProducer
{
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IConfiguration _configuration;
    private readonly IEnvironment _environment;
    private readonly IFileSystem _fileSystem;
    private readonly IOutputDevice _outputDevice;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly IAzureDevOpsTestResultsClient _client;
    private readonly ITask _task;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly AzureDevOpsTestResultsPublisherOptions _options;

    private AzureDevOpsPublishConfiguration? _publishConfiguration;
    private AzureDevOpsRunIdCoordinator? _runIdCoordinator;
    private AzureDevOpsCoordinatedRun? _coordinatedRun;
    private string? _resultMapPath;

    public AzureDevOpsTestRunOrchestratorLifetime(
        ICommandLineOptions commandLineOptions,
        IConfiguration configuration,
        IEnvironment environment,
        IFileSystem fileSystem,
        IOutputDevice outputDevice,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        IAzureDevOpsTestResultsClient client,
        ITask task,
        IClock clock,
        ILoggerFactory loggerFactory)
        : this(commandLineOptions, configuration, environment, fileSystem, outputDevice, testApplicationModuleInfo, client, task, clock, loggerFactory.CreateLogger<AzureDevOpsTestRunOrchestratorLifetime>(), AzureDevOpsTestResultsPublisherOptions.Default)
    {
    }

    internal AzureDevOpsTestRunOrchestratorLifetime(
        ICommandLineOptions commandLineOptions,
        IConfiguration configuration,
        IEnvironment environment,
        IFileSystem fileSystem,
        IOutputDevice outputDevice,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        IAzureDevOpsTestResultsClient client,
        ITask task,
        IClock clock,
        ILogger logger,
        AzureDevOpsTestResultsPublisherOptions options)
    {
        _commandLineOptions = commandLineOptions;
        _configuration = configuration;
        _environment = environment;
        _fileSystem = fileSystem;
        _outputDevice = outputDevice;
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _client = client;
        _task = task;
        _clock = clock;
        _logger = logger;
        _options = options;
    }

    public string Uid => nameof(AzureDevOpsTestRunOrchestratorLifetime);

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => AzureDevOpsResources.DisplayName;

    public string Description => AzureDevOpsResources.Description;

    internal int? RunId => _coordinatedRun?.RunId;

    public Task<bool> IsEnabledAsync()
        => Task.FromResult(_commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.PublishAzureDevOpsTestResultsOptionName));

    public async Task BeforeRunAsync(CancellationToken cancellationToken)
    {
        if (!AzureDevOpsPublishConfigurationFactory.TryCreate(_commandLineOptions, _configuration, _environment, _testApplicationModuleInfo, out AzureDevOpsPublishConfiguration? publishConfiguration, out string? warning))
        {
            // Not configured for live publishing. Keep this off the output device — the test host publisher
            // reports it there once — but still record it in the diagnostic log.
            TryLogWarning(warning);
            return;
        }

        // A run id already in the environment for this build means an ancestor process owns the run (e.g.
        // nested orchestrators). Leave its lifetime alone and let the test hosts join it as they would
        // anyway. Checked after the configuration is built because the build id is what scopes the handoff.
        if (AzureDevOpsConstants.TryGetInheritedTestRunId(_environment, publishConfiguration.BuildId) is not null)
        {
            return;
        }

        AzureDevOpsRunIdCoordinator coordinator = new(_fileSystem, _task, _clock, _environment, _logger, _options);
        try
        {
            // Go through the coordinator rather than creating a run directly, so that several test
            // applications sharing a results directory (a multi-project 'dotnet test') still land in a
            // single run for the build. Orchestrator processes are well-behaved participants: this one
            // outlives every attempt it launches, so it can hold the lease for all of them.
            _coordinatedRun = await coordinator.AcquireRunAsync(
                publishConfiguration,
                createRunCancellationToken => _client.CreateTestRunAsync(publishConfiguration, createRunCancellationToken),
                cancellationToken).ConfigureAwait(false);
            _runIdCoordinator = coordinator;
            _publishConfiguration = publishConfiguration;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // No run was acquired (AcquireRunAsync cleans up after itself), so the test hosts fall back to
            // coordinating among themselves — the pre-existing behaviour, not a loss of results.
            // The filter tests the caller's token rather than the exception type because the HTTP client
            // surfaces its own internal timeouts as TaskCanceledException; those must be reported, not
            // rethrown as if the user had canceled.
            await WarnAsync($"{AzureDevOpsResources.AzureDevOpsLivePublishingCreateRunFailed} {ex.Message}", cancellationToken).ConfigureAwait(false);
            return;
        }

        // The run now exists and this process owns closing it. Nothing below may discard that state: doing
        // so would make AfterRunAsync skip finalization and leave the run "InProgress" forever.
        try
        {
            // Published before any test host is launched so that every orchestrated process inherits it.
            _environment.SetEnvironmentVariable(AzureDevOpsConstants.TestRunIdEnvironmentVariableName, AzureDevOpsConstants.FormatInheritedTestRunId(publishConfiguration.BuildId, _coordinatedRun.RunId));

            // Attempts publish into one run but run in separate processes with separate results
            // directories, so the mapping from test to result id needs a location they all agree on and
            // that only they can see. A random orchestration id keeps two orchestrations of the same build
            // apart even if an agent reuses a process id after a crash.
            _resultMapPath = Path.Combine(
                publishConfiguration.ResultsDirectory,
                $"azdo-results.{publishConfiguration.BuildId.ToString(CultureInfo.InvariantCulture)}.{Guid.NewGuid():N}.json");
            try
            {
                _environment.SetEnvironmentVariable(AzureDevOpsConstants.ResultMapPathEnvironmentVariableName, AzureDevOpsConstants.FormatResultMapPath(publishConfiguration.BuildId, _resultMapPath));
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _resultMapPath = null;
                TryLogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingResultMapHandoffFailed} {ex.Message}");
            }

            // Only the process that created the run announces it; the others share the same id and would
            // just repeat the same line.
            if (_coordinatedRun.IsOwner)
            {
                await DisplayAsync(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        AzureDevOpsResources.AzureDevOpsLivePublishingRunCreated,
                        _coordinatedRun.RunId,
                        AzureDevOpsPublishConfigurationFactory.BuildTestRunUrl(publishConfiguration, _coordinatedRun.RunId)),
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Without the handoff the attempts coordinate among themselves instead of joining this run,
            // which is degraded but not broken. The run itself stays owned by this process.
            TryLogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingRunIdHandoffFailed} {ex.Message}");
        }
    }

    public async Task AfterRunAsync(int exitCode, CancellationToken cancellationToken)
    {
        // Read and clear together so a second invocation cannot finalize the run twice. AfterRunAsync can
        // be reached without a successful BeforeRunAsync (another lifetime may have faulted first), in
        // which case there is nothing to release.
        AzureDevOpsCoordinatedRun? coordinatedRun = _coordinatedRun;
        AzureDevOpsRunIdCoordinator? coordinator = _runIdCoordinator;
        AzureDevOpsPublishConfiguration? publishConfiguration = _publishConfiguration;
        string? resultMapPath = _resultMapPath;
        _coordinatedRun = null;
        _runIdCoordinator = null;
        _publishConfiguration = null;
        _resultMapPath = null;

        if (coordinatedRun is null || coordinator is null || publishConfiguration is null)
        {
            return;
        }

        // Stop handing the run id to any process started later: the run is about to be closed, and a
        // straggler publishing into a completed run would be rejected by Azure DevOps.
        TrySetEnvironmentVariable(
            AzureDevOpsConstants.TestRunIdEnvironmentVariableName,
            null,
            AzureDevOpsResources.AzureDevOpsLivePublishingRunIdHandoffFailed);
        TrySetEnvironmentVariable(
            AzureDevOpsConstants.ResultMapPathEnvironmentVariableName,
            null,
            AzureDevOpsResources.AzureDevOpsLivePublishingResultMapHandoffFailed);

        // The map only has meaning for the run being closed, and it lives in the results directory, which
        // is published as a build artifact. Removing it keeps an internal coordination file out of the
        // artifacts without affecting anything already in Azure DevOps.
        if (resultMapPath is not null)
        {
            TryDeleteFile(resultMapPath);
        }

        // Azure DevOps uses "Aborted" for cancellation and session-level infrastructure failures.
        // Individual failing tests must still complete the run, so only exit codes that are not a test
        // result are treated as an abort — mirroring how the test host publisher finalizes a run it owns.
        bool exitCodeIsTestResult = exitCode is (int)ExitCode.Success or (int)ExitCode.AtLeastOneTestFailed;
        string finalState = cancellationToken.IsCancellationRequested || !exitCodeIsTestResult
            ? AzureDevOpsLivePublishingConstants.AbortedTestRunState
            : AzureDevOpsLivePublishingConstants.CompletedTestRunState;

        try
        {
            // A fresh token so the run is still closed when the orchestration itself was canceled: a run
            // left "InProgress" never shows up in the build's Tests tab, which looks like total data loss
            // even though every result was uploaded.
            using var cleanupCts = new CancellationTokenSource(_options.CoordinationFinalizeMaxWaitTime + TimeSpan.FromSeconds(60));
            await coordinator.FinalizeRunAsync(
                coordinatedRun,
                finalizeCancellationToken => _client.UpdateTestRunStateAsync(publishConfiguration, coordinatedRun.RunId, finalState, finalizeCancellationToken),
                cleanupCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Cancellation is handled here too: this step runs on its own token, so an expiry is a
            // finalization failure rather than a canceled session, and must not escape and fail an
            // otherwise successful run.
            await WarnAsync(
                $"{AzureDevOpsResources.AzureDevOpsLivePublishingCompleteRunFailed} {ex.Message} {AzureDevOpsResources.AzureDevOpsLivePublishingRunLeftInProgress}",
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Deletes a coordination file, swallowing any failure.
    /// </summary>
    /// <remarks>
    /// Called from teardown, where a leftover file is only untidy: nothing reads it once the handoff
    /// variable is withdrawn, and every later orchestration receives a different random map path.
    /// </remarks>
    private void TryDeleteFile(string path)
    {
        try
        {
            if (_fileSystem.ExistFile(path))
            {
                _fileSystem.DeleteFile(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            TryLogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingFailedToDeleteCoordinationFile} {path}: {ex.Message}");
        }
    }

    /// <summary>
    /// Sets an environment variable, swallowing any failure.
    /// </summary>
    /// <remarks>
    /// Called from teardown, where the host may refuse the write (a <c>SecurityException</c> on
    /// .NET Framework). Failing to withdraw the handoff is far less harmful than failing the run.
    /// </remarks>
    private void TrySetEnvironmentVariable(string name, string? value, string failureMessage)
    {
        try
        {
            _environment.SetEnvironmentVariable(name, value);
        }
        catch (Exception ex)
        {
            TryLogWarning($"{failureMessage} {ex.Message}");
        }
    }

    /// <summary>
    /// Reports a live-publishing problem both to the diagnostic log and to the output device, so that it
    /// is visible in CI logs instead of only in an opt-in log file. Never throws: it is invoked from
    /// error-recovery paths where propagating would turn a warning into a failed run.
    /// </summary>
    private async Task WarnAsync(string message, CancellationToken cancellationToken)
    {
        TryLogWarning(message);
        await DisplayCoreAsync(new WarningMessageOutputDeviceData(message), cancellationToken).ConfigureAwait(false);
    }

    private Task DisplayAsync(string message, CancellationToken cancellationToken)
        => DisplayCoreAsync(new SessionMessageOutputDeviceData(message), cancellationToken);

    private async Task DisplayCoreAsync(IOutputDeviceData data, CancellationToken cancellationToken)
    {
        try
        {
            await _outputDevice.DisplayAsync(this, data, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The message is already in the diagnostic log (for warnings); losing the console copy is
            // preferable to failing the test run from inside a diagnostic helper.
            TryLogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingWarningDisplayFailed} {ex.Message}");
        }
    }

    private void TryLogWarning(string message)
    {
        try
        {
            _logger.LogWarning(message);
        }
        catch (Exception)
        {
            // There is nowhere left to report this: the diagnostic logger is the fallback sink.
        }
    }
}
