// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

/// <summary>
/// Surfaces tests that are still running past a per-test threshold as durable scrollback lines, lowering the
/// threshold for tests with a known-short historical runtime and decorating each emission with the historical
/// p95/p99 fetched from Azure DevOps test history.
/// </summary>
/// <remarks>
/// This is a self-contained emitter for the Azure DevOps host. Once the platform-level <c>IProgressEnricher</c>
/// hook (issue #9139) ships, the surfacing/backoff logic should migrate onto it and this type should only
/// supply the history-driven threshold and decoration.
/// </remarks>
internal sealed class AzureDevOpsSlowTestReporter : IDataConsumer, ITestSessionLifetimeHandler, IOutputDeviceDataProducer
{
    private const string AzureDevOpsTfBuildVariableName = "TF_BUILD";
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(1);

    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IEnvironment _environment;
    private readonly IOutputDevice _outputDevice;
    private readonly ITask _task;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly IAzureDevOpsHistoryService _historyService;
    private readonly ConcurrentDictionary<string, InProgressTest> _inProgress = new(StringComparer.Ordinal);
    private readonly bool _isEnabled;
    private readonly TimeSpan _staticThreshold;

    private double _multiplier;
    private volatile int _minimumSampleCount;
    private volatile bool _active;
    private CancellationTokenSource? _loopCancellationTokenSource;
    private Task? _loopTask;

    public AzureDevOpsSlowTestReporter(
        ICommandLineOptions commandLineOptions,
        IEnvironment environment,
        IOutputDevice outputDevice,
        ITask task,
        IClock clock,
        ILoggerFactory loggerFactory,
        IAzureDevOpsHistoryService historyService)
    {
        _commandLineOptions = commandLineOptions;
        _environment = environment;
        _outputDevice = outputDevice;
        _task = task;
        _clock = clock;
        _logger = loggerFactory.CreateLogger<AzureDevOpsSlowTestReporter>();
        _historyService = historyService;
        _staticThreshold = TimeSpan.FromSeconds(AzureDevOpsCommandLineOptions.SlowTestStaticThresholdSeconds);
        _isEnabled = commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsOptionName)
            && commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsSlowTestHistory);
    }

    public Type[] DataTypesConsumed { get; } = [typeof(TestNodeUpdateMessage)];

    public string Uid => nameof(AzureDevOpsSlowTestReporter);

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => AzureDevOpsResources.DisplayName;

    public string Description => AzureDevOpsResources.Description;

    public Task<bool> IsEnabledAsync() => Task.FromResult(_isEnabled);

    public async Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        try
        {
            testSessionContext.CancellationToken.ThrowIfCancellationRequested();

            _active = false;
            _inProgress.Clear();

            if (!_isEnabled)
            {
                return;
            }

            if (!string.Equals(_environment.GetEnvironmentVariable(AzureDevOpsTfBuildVariableName), "true", StringComparison.OrdinalIgnoreCase))
            {
                // Outside Azure DevOps the feature truly no-ops: we only leave a low-noise trace and never
                // surface an output-device line, so local/dev runs that happen to pass the option stay quiet.
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace(AzureDevOpsResources.SlowTestHistoryRequiresTfBuildWarning);
                }

                return;
            }

            // 'double' cannot be marked 'volatile', so publish the multiplier through Volatile.Write; the
            // remaining fields use the 'volatile' modifier. Writing _active = true last (below) acts as the
            // release fence that publishes all three to the test-data-producer threads in ConsumeAsync.
            Volatile.Write(ref _multiplier, GetMultiplier());
            _minimumSampleCount = GetMinimumSampleCount();

            _loopCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(testSessionContext.CancellationToken);
            _active = true;
            _loopTask = _task.RunLongRunning(() => ScanLoopAsync(_loopCancellationTokenSource.Token), nameof(AzureDevOpsSlowTestReporter), _loopCancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogUnexpectedException(nameof(OnTestSessionStartingAsync), ex);
        }
    }

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_active || value is not TestNodeUpdateMessage update)
            {
                return Task.CompletedTask;
            }

            string uid = update.TestNode.Uid;
            TestNodeStateProperty? state = update.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>();
            if (state is InProgressTestNodeStateProperty)
            {
                string testName = TestNodeIdentity.GetTestName(update.TestNode);
                TimeSpan threshold = ResolveThreshold(testName);
                _inProgress[uid] = new InProgressTest(testName, _clock.UtcNow, threshold);
            }
            else if (state is not null)
            {
                // Any non-in-progress state (passed/failed/skipped/error/timeout/cancelled) is terminal for surfacing.
                _inProgress.TryRemove(uid, out _);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogUnexpectedException(nameof(ConsumeAsync), ex);
        }

        return Task.CompletedTask;
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        _active = false;

        CancellationTokenSource? loopCancellationTokenSource = _loopCancellationTokenSource;
        if (loopCancellationTokenSource is not null)
        {
#pragma warning disable VSTHRD103 // CancelAsync is unavailable on all target frameworks.
            loopCancellationTokenSource.Cancel();
#pragma warning restore VSTHRD103
        }

        if (_loopTask is not null)
        {
            try
            {
                await _loopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during normal shutdown: cancelling _loopCancellationTokenSource above unblocks the
                // scan loop, which surfaces as a cancellation here. Nothing to do — swallow and finish teardown.
            }
            catch (Exception ex)
            {
                LogUnexpectedException(nameof(OnTestSessionFinishingAsync), ex);
            }
        }

        loopCancellationTokenSource?.Dispose();
        _loopCancellationTokenSource = null;
        _loopTask = null;
        _inProgress.Clear();
    }

    private async Task ScanLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _task.Delay(ScanInterval, cancellationToken).ConfigureAwait(false);
                await ScanOnceAsync(_clock.UtcNow, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    // Internal for unit testing: performs a single surfacing pass at the given 'now' so tests can drive
    // the emission/backoff logic deterministically without relying on the timer-driven loop.
    internal async Task ScanOnceAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (KeyValuePair<string, InProgressTest> entry in _inProgress)
        {
            InProgressTest test = entry.Value;
            TimeSpan elapsed = now - test.StartTime;
            if (elapsed < test.NextEmitThreshold)
            {
                continue;
            }

            // Exponential backoff so a genuinely stuck test does not spam the log: T, 2T, 4T, ...
            // Clamp at TimeSpan.MaxValue so a very long-running test cannot overflow Ticks * 2 into a
            // negative value (which would make the backoff fire on every scan).
            long currentTicks = test.NextEmitThreshold.Ticks;
            test.NextEmitThreshold = currentTicks > TimeSpan.MaxValue.Ticks / 2
                ? TimeSpan.MaxValue
                : TimeSpan.FromTicks(currentTicks * 2);

            try
            {
                await EmitSlowTestAsync(test, elapsed, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogUnexpectedException(nameof(ScanOnceAsync), ex);
            }
        }
    }

    private async Task EmitSlowTestAsync(InProgressTest test, TimeSpan elapsed, CancellationToken cancellationToken)
    {
        string elapsedText = AzureDevOpsSlowTestThresholds.FormatDuration(elapsed.TotalMilliseconds);
        string line = string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.SlowTestStillRunning, elapsedText, test.TestName);

        if (_historyService.TryGetDurationStats(test.TestName, out DurationHistoryStats stats)
            && AzureDevOpsSlowTestThresholds.HasUsableHistory(stats, hasStats: true, _minimumSampleCount))
        {
            string decoration = string.Format(
                CultureInfo.InvariantCulture,
                AzureDevOpsResources.SlowTestHistoryDecoration,
                AzureDevOpsSlowTestThresholds.FormatDuration(stats.P95Milliseconds),
                AzureDevOpsSlowTestThresholds.FormatDuration(stats.P99Milliseconds),
                stats.SampleCount.ToString(CultureInfo.InvariantCulture));
            line = $"{line}  {decoration}";
        }

        await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData(line), cancellationToken).ConfigureAwait(false);
    }

    private TimeSpan ResolveThreshold(string testName)
    {
        bool hasStats = _historyService.TryGetDurationStats(testName, out DurationHistoryStats stats);
        return AzureDevOpsSlowTestThresholds.ComputeThreshold(_staticThreshold, stats, hasStats, Volatile.Read(ref _multiplier), _minimumSampleCount);
    }

    private double GetMultiplier()
        => _commandLineOptions.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.AzureDevOpsSlowTestHistoryMultiplier, out string[]? arguments)
            && arguments is [string value]
            && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double multiplier)
            && multiplier > 0
                ? multiplier
                : AzureDevOpsCommandLineOptions.SlowTestHistoryDefaultMultiplier;

    private int GetMinimumSampleCount()
        => _commandLineOptions.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.AzureDevOpsSlowTestHistoryMinSample, out string[]? arguments)
            && arguments is [string value]
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int minimum)
            && minimum >= 1
                ? minimum
                : AzureDevOpsCommandLineOptions.SlowTestHistoryDefaultMinSample;

    private void LogUnexpectedException(string callbackName, Exception ex)
    {
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning($"Unexpected exception in {callbackName}: {ex}");
        }
    }

    private sealed class InProgressTest
    {
        public InProgressTest(string testName, DateTimeOffset startTime, TimeSpan threshold)
        {
            TestName = testName;
            StartTime = startTime;
            NextEmitThreshold = threshold;
        }

        public string TestName { get; }

        public DateTimeOffset StartTime { get; }

        public TimeSpan NextEmitThreshold { get; set; }
    }
}
