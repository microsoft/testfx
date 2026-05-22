// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed class AzureDevOpsTestResultsPublisher : IDataConsumer, ITestSessionLifetimeHandler, IDisposable
{
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IConfiguration _configuration;
    private readonly IEnvironment _environment;
    private readonly IFileSystem _fileSystem;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly ITestApplicationProcessExitCode _testApplicationProcessExitCode;
    private readonly IAzureDevOpsTestResultsClient _client;
    private readonly ITask _task;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly AzureDevOpsTestResultsPublisherOptions _options;
    // Mutate only while holding _flushSemaphore.
    private readonly Stack<AzureDevOpsTestCaseResult> _retryResults = new();
    private readonly ConcurrentQueue<AzureDevOpsTestCaseResult> _pendingResults = new();
    private readonly SemaphoreSlim _flushSemaphore = new(1, 1);

    private AzureDevOpsPublishConfiguration? _publishConfiguration;
    private AzureDevOpsRunIdCoordinator? _runIdCoordinator;
    private AzureDevOpsCoordinatedRun? _coordinatedRun;
    private DateTimeOffset _lastFlushTime;
    private CancellationTokenSource? _backgroundFlushCts;
    private Task? _backgroundFlushTask;

    private int? CurrentRunId { get; set; }

    public AzureDevOpsTestResultsPublisher(
        ICommandLineOptions commandLineOptions,
        IConfiguration configuration,
        IEnvironment environment,
        IFileSystem fileSystem,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        ITestApplicationProcessExitCode testApplicationProcessExitCode,
        IAzureDevOpsTestResultsClient client,
        ITask task,
        IClock clock,
        ILoggerFactory loggerFactory)
        : this(commandLineOptions, configuration, environment, fileSystem, testApplicationModuleInfo, testApplicationProcessExitCode, client, task, clock, loggerFactory.CreateLogger<AzureDevOpsTestResultsPublisher>(), AzureDevOpsTestResultsPublisherOptions.Default)
    {
    }

    internal AzureDevOpsTestResultsPublisher(
        ICommandLineOptions commandLineOptions,
        IConfiguration configuration,
        IEnvironment environment,
        IFileSystem fileSystem,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        ITestApplicationProcessExitCode testApplicationProcessExitCode,
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
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _testApplicationProcessExitCode = testApplicationProcessExitCode;
        _client = client;
        _task = task;
        _clock = clock;
        _logger = logger;
        _options = options;
        _lastFlushTime = clock.UtcNow;
    }

    public Type[] DataTypesConsumed { get; } = [typeof(TestNodeUpdateMessage)];

    public string Uid => nameof(AzureDevOpsTestResultsPublisher);

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => AzureDevOpsResources.DisplayName;

    public string Description => AzureDevOpsResources.Description;

    internal int? RunId => CurrentRunId;

    public void Dispose()
    {
        // Signal the background flush loop to stop. The loop is already awaited in
        // OnTestSessionFinishingAsync; this Cancel is a safety net for cases where the
        // session lifecycle methods are not called (e.g. early disposal in tests).
#pragma warning disable VSTHRD103 // CancelAsync is only available on .NET 8+; synchronous cancel is acceptable in Dispose.
        _backgroundFlushCts?.Cancel();
#pragma warning restore VSTHRD103
        _backgroundFlushCts?.Dispose();
        _flushSemaphore.Dispose();
    }

    public Task<bool> IsEnabledAsync()
    {
        if (!_commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.PublishAzureDevOpsTestResultsOptionName))
        {
            return Task.FromResult(false);
        }

        if (_publishConfiguration is not null)
        {
            return Task.FromResult(true);
        }

        if (!TryCreatePublishConfiguration(out AzureDevOpsPublishConfiguration? publishConfiguration, out string? warning))
        {
            _logger.LogWarning(warning ?? AzureDevOpsResources.AzureDevOpsLivePublishingMissingConfiguration);
            return Task.FromResult(false);
        }

        _publishConfiguration = publishConfiguration;
        _runIdCoordinator = new AzureDevOpsRunIdCoordinator(_fileSystem, _task, _clock, _environment, _logger, _options);
        return Task.FromResult(true);
    }

    public async Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        if (_publishConfiguration is null || _runIdCoordinator is null)
        {
            return;
        }

        try
        {
            _coordinatedRun = await _runIdCoordinator.AcquireRunAsync(
                _publishConfiguration,
                cancellationToken => _client.CreateTestRunAsync(_publishConfiguration, cancellationToken),
                testSessionContext.CancellationToken).ConfigureAwait(false);
            CurrentRunId = _coordinatedRun.RunId;

            // Start a background loop that flushes pending results on the time-based interval even
            // when no new TestNodeUpdateMessages arrive (e.g. at the tail end of a slow test run).
            _backgroundFlushCts = new CancellationTokenSource();
            _backgroundFlushTask = Task.Run(() => BackgroundFlushLoopAsync(_backgroundFlushCts.Token));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingCreateRunFailed} {ex.Message}");
            _publishConfiguration = null;
            _coordinatedRun = null;
            CurrentRunId = null;
        }
    }

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_publishConfiguration is null || _runIdCoordinator is null || _coordinatedRun is null || CurrentRunId is null || value is not TestNodeUpdateMessage testNodeUpdateMessage)
        {
            return;
        }

        try
        {
            AzureDevOpsTestCaseResult? testCaseResult = CreateTestCaseResult(testNodeUpdateMessage.TestNode, _publishConfiguration.AutomatedTestStorage);
            if (testCaseResult is null)
            {
                return;
            }

            await _runIdCoordinator.RenewLeaseAsync(_coordinatedRun, cancellationToken).ConfigureAwait(false);
            _pendingResults.Enqueue(testCaseResult);
            await FlushPendingResultsAsync(force: false, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingPublishResultsFailed} {ex.Message}");
        }
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        if (_publishConfiguration is null || _coordinatedRun is null || CurrentRunId is null || _runIdCoordinator is null)
        {
            return;
        }

        // Stop the background flush loop before doing the session-end forced flush so there is no
        // concurrent flush in flight when we drain the last batch.
        if (_backgroundFlushCts is not null && _backgroundFlushTask is not null)
        {
#pragma warning disable VSTHRD103 // CancelAsync is only available on .NET 8+; multi-target sync cancel is acceptable here.
            _backgroundFlushCts.Cancel();
#pragma warning restore VSTHRD103
            try
            {
                await _backgroundFlushTask.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Unexpected failure in the background flush loop; the loop already logs per-flush warnings.
                _logger.LogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingPublishResultsFailed} {ex.Message}");
            }
            catch
            {
                // Cancellation — expected and fine.
            }
        }

        try
        {
            await _runIdCoordinator.RenewLeaseAsync(_coordinatedRun, testSessionContext.CancellationToken).ConfigureAwait(false);
            await FlushPendingResultsAsync(force: true, testSessionContext.CancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Best-effort flush: session was canceled; finalization still runs below with a fresh token.
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingPublishResultsFailed} {ex.Message}");
        }

        // Azure DevOps test runs use "Aborted" specifically for cancellation or session-level
        // infrastructure failures. Individual failing tests should still mark the run as
        // "Completed" — only treat process exit codes other than Success/AtLeastOneTestFailed as
        // an abort signal (e.g. TestSessionAborted, TestHostProcessExitedNonGracefully,
        // TestAdapterTestSessionFailure, MinimumExpectedTestsPolicyViolation, etc.).
        int exitCode = _testApplicationProcessExitCode.GetProcessExitCode();
        bool exitCodeIsTestResult = exitCode is (int)ExitCode.Success or (int)ExitCode.AtLeastOneTestFailed;
        string finalState = testSessionContext.CancellationToken.IsCancellationRequested
                || _testApplicationProcessExitCode.HasTestAdapterTestSessionFailure
                || !exitCodeIsTestResult
            ? AzureDevOpsLivePublishingConstants.AbortedTestRunState
            : AzureDevOpsLivePublishingConstants.CompletedTestRunState;

        try
        {
            // Use a fresh, non-canceled token so finalization (marking the run Aborted/Completed)
            // succeeds even when the test session itself has been canceled.
            using var cleanupCts = new CancellationTokenSource(_options.CoordinationFinalizeTimeout + TimeSpan.FromSeconds(60));
            await _runIdCoordinator.FinalizeRunAsync(
                _coordinatedRun,
                cancellationToken => _client.UpdateTestRunStateAsync(_publishConfiguration, CurrentRunId.Value, finalState, cancellationToken),
                cleanupCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingCompleteRunFailed} {ex.Message}");
        }
    }

    internal static AzureDevOpsTestCaseResult? CreateTestCaseResult(TestNode testNode, string automatedTestStorage)
    {
        TestNodeStateProperty? state = testNode.Properties.SingleOrDefault<TestNodeStateProperty>();
        if (state is null or DiscoveredTestNodeStateProperty or InProgressTestNodeStateProperty)
        {
            return null;
        }

        TimingProperty? timing = testNode.Properties.SingleOrDefault<TimingProperty>();
        string automatedTestName = testNode.Uid.Value;

        return state switch
        {
            PassedTestNodeStateProperty passed => CreateResult(testNode.DisplayName, automatedTestName, automatedTestStorage, AzureDevOpsLivePublishingConstants.PassedTestOutcome, passed.Explanation, null, timing),
            FailedTestNodeStateProperty failed => CreateResult(testNode.DisplayName, automatedTestName, automatedTestStorage, AzureDevOpsLivePublishingConstants.FailedTestOutcome, failed.Exception?.Message ?? failed.Explanation, failed.Exception?.StackTrace, timing),
            ErrorTestNodeStateProperty error => CreateResult(testNode.DisplayName, automatedTestName, automatedTestStorage, AzureDevOpsLivePublishingConstants.FailedTestOutcome, error.Exception?.Message ?? error.Explanation, error.Exception?.StackTrace, timing),
            SkippedTestNodeStateProperty skipped => CreateResult(testNode.DisplayName, automatedTestName, automatedTestStorage, AzureDevOpsLivePublishingConstants.NotExecutedTestOutcome, skipped.Explanation, null, timing),
            TimeoutTestNodeStateProperty timeout => CreateResult(testNode.DisplayName, automatedTestName, automatedTestStorage, AzureDevOpsLivePublishingConstants.FailedTestOutcome, BuildTimeoutMessage(timeout), timeout.Exception?.StackTrace, timing),
#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
            CancelledTestNodeStateProperty cancelled => CreateResult(testNode.DisplayName, automatedTestName, automatedTestStorage, AzureDevOpsLivePublishingConstants.AbortedTestOutcome, cancelled.Exception?.Message ?? cancelled.Explanation, cancelled.Exception?.StackTrace, timing),
#pragma warning restore CS0618, MTP0001 // Type or member is obsolete
            _ => null,
        };
    }

    private async Task BackgroundFlushLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.FlushInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await FlushPendingResultsAsync(force: false, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingPublishResultsFailed} {ex.Message}");
            }
        }
    }

    private async Task FlushPendingResultsAsync(bool force, CancellationToken cancellationToken)
    {
        if (_publishConfiguration is null || CurrentRunId is null)
        {
            return;
        }

        await _flushSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            while (_publishConfiguration is not null && CurrentRunId is not null)
            {
                if (!ShouldFlushUnsafe(force))
                {
                    return;
                }

                List<AzureDevOpsTestCaseResult> batch = [];
                while (batch.Count < _options.BatchSize && _retryResults.Count > 0)
                {
                    batch.Add(_retryResults.Pop());
                }

                while (batch.Count < _options.BatchSize && _pendingResults.TryDequeue(out AzureDevOpsTestCaseResult? result))
                {
                    batch.Add(result);
                }

                if (batch.Count == 0)
                {
                    return;
                }

                try
                {
                    if (_coordinatedRun is not null && _runIdCoordinator is not null)
                    {
                        await _runIdCoordinator.RenewLeaseAsync(_coordinatedRun, cancellationToken).ConfigureAwait(false);
                    }

                    await _client.PublishTestResultsAsync(_publishConfiguration, CurrentRunId.Value, batch, cancellationToken).ConfigureAwait(false);
                    _lastFlushTime = _clock.UtcNow;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Push results in reverse so Pop retries them in the original batch order.
                    for (int i = batch.Count - 1; i >= 0; i--)
                    {
                        _retryResults.Push(batch[i]);
                    }

                    // Reset the interval countdown so a transient failure does not cause a tight retry loop.
                    _lastFlushTime = _clock.UtcNow;
                    _logger.LogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingPublishResultsFailed} {ex.Message}");
                    return;
                }
            }
        }
        finally
        {
            _flushSemaphore.Release();
        }
    }

    private bool TryCreatePublishConfiguration(out AzureDevOpsPublishConfiguration? publishConfiguration, out string? warning)
    {
        publishConfiguration = null;
        warning = null;

        List<string> missingVariables = [];

        bool isTfBuild = string.Equals(_environment.GetEnvironmentVariable("TF_BUILD"), "true", StringComparison.OrdinalIgnoreCase);
        if (!isTfBuild)
        {
            missingVariables.Add("TF_BUILD=true");
        }

        string? collectionUri = GetRequiredEnvironmentVariable("SYSTEM_COLLECTIONURI", missingVariables);
        string? project = GetRequiredEnvironmentVariable("SYSTEM_TEAMPROJECT", missingVariables);
        string? accessToken = GetRequiredEnvironmentVariable("SYSTEM_ACCESSTOKEN", missingVariables);
        string? buildIdText = GetRequiredEnvironmentVariable("BUILD_BUILDID", missingVariables);

        if (missingVariables.Count > 0)
        {
            warning = string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingMissingConfiguration, string.Join(", ", missingVariables));
            return false;
        }

        if (!int.TryParse(buildIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int buildId))
        {
            warning = string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingMissingConfiguration, "BUILD_BUILDID");
            return false;
        }

        string currentTestApplicationPath = _testApplicationModuleInfo.GetCurrentTestApplicationFullPath();
        string assemblyName = _testApplicationModuleInfo.TryGetAssemblyName() ?? Path.GetFileNameWithoutExtension(currentTestApplicationPath);
        string automatedTestStorage = Path.GetFileNameWithoutExtension(currentTestApplicationPath);
        string targetFrameworkMoniker = GetTargetFrameworkMoniker();
        string agentName = _environment.GetEnvironmentVariable("AGENT_NAME") ?? _environment.MachineName;
        string? stageName = _environment.GetEnvironmentVariable("SYSTEM_STAGENAME");
        string? jobName = _environment.GetEnvironmentVariable("SYSTEM_JOBNAME");
        string runName = GetRunName(assemblyName, targetFrameworkMoniker, agentName, stageName, jobName);
        string resultsDirectory = _configuration.GetTestResultDirectory();

        if (_commandLineOptions.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.PublishAzureDevOpsRunNameOptionName, out string[]? arguments) && arguments is [string configuredRunName])
        {
            runName = configuredRunName;
        }

        publishConfiguration = new AzureDevOpsPublishConfiguration(collectionUri!, project!, accessToken!, buildId, runName, automatedTestStorage, resultsDirectory);
        return true;
    }

    private string? GetRequiredEnvironmentVariable(string variableName, List<string> missingVariables)
    {
        string? value = _environment.GetEnvironmentVariable(variableName);
        if (RoslynString.IsNullOrWhiteSpace(value))
        {
            missingVariables.Add(variableName);
        }

        return value;
    }

    private bool ShouldFlushUnsafe(bool force)
    {
        int pendingResultsCount = _retryResults.Count + _pendingResults.Count;
        return force
            ? pendingResultsCount > 0
            : pendingResultsCount >= _options.BatchSize || (pendingResultsCount > 0 && _clock.UtcNow - _lastFlushTime >= _options.FlushInterval);
    }

    private static AzureDevOpsTestCaseResult CreateResult(
        string displayName,
        string automatedTestName,
        string automatedTestStorage,
        string outcome,
        string? errorMessage,
        string? stackTrace,
        TimingProperty? timing)
        => new(
            automatedTestName,
            automatedTestStorage,
            displayName,
            outcome,
            timing is null ? null : (long)Math.Round(timing.GlobalTiming.Duration.TotalMilliseconds, MidpointRounding.AwayFromZero),
            errorMessage,
            stackTrace,
            timing?.GlobalTiming.StartTime,
            timing?.GlobalTiming.EndTime);

    private static string BuildTimeoutMessage(TimeoutTestNodeStateProperty timeout)
    {
        string? reason = timeout.Explanation ?? timeout.Exception?.Message;
        return RoslynString.IsNullOrWhiteSpace(reason)
            ? AzureDevOpsResources.AzureDevOpsLivePublishingTimeoutErrorMessage
            : string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingTimeoutErrorMessageWithReason, reason);
    }

    private static string GetRunName(string assemblyName, string targetFrameworkMoniker, string agentName, string? stageName, string? jobName)
    {
        string runName = $"{assemblyName} ({targetFrameworkMoniker}) on {agentName}";
        string? stageJob = (SanitizeRunNameComponent(stageName), SanitizeRunNameComponent(jobName)) switch
        {
            ({ Length: > 0 } stage, { Length: > 0 } job) => $"{stage}/{job}",
            ({ Length: > 0 } stage, _) => stage,
            (_, { Length: > 0 } job) => job,
            _ => null,
        };

        string candidateRunName = stageJob is null ? runName : $"{runName} [{stageJob}]";
        return candidateRunName.Length <= AzureDevOpsLivePublishingConstants.MaxRunNameLength
            ? candidateRunName
            : candidateRunName[..AzureDevOpsLivePublishingConstants.MaxRunNameLength];
    }

    private static string? SanitizeRunNameComponent(string? value)
    {
        if (RoslynString.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        char[] buffer = value.ToCharArray();
        for (int index = 0; index < buffer.Length; index++)
        {
            char current = buffer[index];
            if (current is '/' or '\\' or '\r' or '\n' || char.IsControl(current))
            {
                buffer[index] = '_';
            }
        }

        return new string(buffer);
    }

    private static string GetTargetFrameworkMoniker()
        => TargetFrameworkParser.GetShortTargetFramework(Assembly.GetEntryAssembly()?.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()?.FrameworkDisplayName)
            ?? TargetFrameworkParser.GetShortTargetFramework(RuntimeInformation.FrameworkDescription);
}
