// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Shared plumbing for the pipeline-specific slow-test reporters (Azure DevOps and GitHub Actions).
/// Tracks in-progress tests, runs the background scan loop, and applies exponential backoff so a
/// genuinely stuck test does not spam the log. Host-specific concerns — how to resolve the emission
/// threshold and how to render the surfaced line — are supplied by derived types.
/// </summary>
/// <remarks>
/// Once the platform-level <c>IProgressEnricher</c> hook (issue #9139) ships, the surfacing/backoff logic
/// here should migrate onto it and each host should only supply the threshold and decoration.
/// </remarks>
internal abstract class SlowTestReporterBase : IDataConsumer, ITestSessionLifetimeHandler, IOutputDeviceDataProducer
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(1);

    private readonly ITask _task;
    private readonly IClock _clock;
    private readonly ConcurrentDictionary<string, InProgressTest> _inProgress = new(StringComparer.Ordinal);

    private volatile bool _active;
    private CancellationTokenSource? _loopCancellationTokenSource;
    private Task? _loopTask;

    protected SlowTestReporterBase(IOutputDevice outputDevice, ITask task, IClock clock, ILogger logger)
    {
        OutputDevice = outputDevice;
        _task = task;
        _clock = clock;
        Logger = logger;
    }

    public Type[] DataTypesConsumed { get; } = [typeof(TestNodeUpdateMessage)];

    public abstract string Uid { get; }

    public string Version => ExtensionVersion.DefaultSemVer;

    public abstract string DisplayName { get; }

    public abstract string Description { get; }

    /// <summary>
    /// Gets a value indicating whether the reporter is enabled, based on the host-specific options.
    /// </summary>
    protected abstract bool IsEnabled { get; }

    protected IOutputDevice OutputDevice { get; }

    protected ILogger Logger { get; }

    public Task<bool> IsEnabledAsync() => Task.FromResult(IsEnabled);

    public Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        try
        {
            testSessionContext.CancellationToken.ThrowIfCancellationRequested();

            _active = false;
            _inProgress.Clear();

            if (!IsEnabled)
            {
                return Task.CompletedTask;
            }

            // Host-specific activation gate (e.g. Azure DevOps requires TF_BUILD and warms up history state).
            if (!OnActivating())
            {
                return Task.CompletedTask;
            }

            _loopCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(testSessionContext.CancellationToken);
            _active = true;
            _loopTask = _task.RunLongRunning(() => ScanLoopAsync(_loopCancellationTokenSource.Token), Uid, _loopCancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogUnexpectedException(nameof(OnTestSessionStartingAsync), ex);
        }

        return Task.CompletedTask;
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
            TestNodeStateProperty? state = update.TestNode.Properties.FirstOrDefault<TestNodeStateProperty>();
            if (state is InProgressTestNodeStateProperty)
            {
                string testName = GetTestName(update.TestNode);
                string displayLabel = GetDisplayLabel(update.TestNode);
                TimeSpan threshold = ResolveThreshold(testName);

                // Use the first-seen start time: the platform can emit InProgress more than once for the same
                // test (progress heartbeats), and resetting the start time on each would keep pushing the slow
                // threshold out so a genuinely slow test would never surface.
                _inProgress.TryAdd(uid, new InProgressTest(testName, displayLabel, _clock.UtcNow, threshold));
            }
            else if (state is not null || update.TestNode.Properties.Any<TestNodeExecutionCompletedProperty>())
            {
                // Any non-in-progress state, including outcome-less execution completion, is terminal for surfacing.
                _inProgress.TryRemove(uid, out _);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogUnexpectedException(nameof(ConsumeAsync), ex);
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
                Logger.LogUnexpectedException(nameof(OnTestSessionFinishingAsync), ex);
            }
        }

        loopCancellationTokenSource?.Dispose();
        _loopCancellationTokenSource = null;
        _loopTask = null;
        _inProgress.Clear();
    }

    /// <summary>
    /// Resolves the stable, fully-qualified test name for a <see cref="TestNode"/>.
    /// </summary>
    protected abstract string GetTestName(TestNode testNode);

    /// <summary>
    /// Resolves a human-friendly label for a <see cref="TestNode"/> used when rendering the surfaced line.
    /// Unlike <see cref="GetTestName"/> (which stays stable for history/threshold lookups), this may carry
    /// the parameterized-instance suffix so data-driven tests sharing one name are distinguishable.
    /// </summary>
    protected abstract string GetDisplayLabel(TestNode testNode);

    /// <summary>
    /// Resolves the elapsed time after which the given test should first surface a slow-test notice.
    /// </summary>
    protected abstract TimeSpan ResolveThreshold(string testName);

    /// <summary>
    /// Renders and emits the host-specific slow-test line for a test that has passed its threshold.
    /// </summary>
    /// <param name="testName">The stable, fully-qualified name used for history/threshold lookups.</param>
    /// <param name="displayLabel">The human-friendly label to render, distinguishing parameterized instances.</param>
    /// <param name="elapsed">The elapsed time the test has been running.</param>
    /// <param name="cancellationToken">A token to observe while emitting.</param>
    protected abstract Task EmitSlowTestAsync(string testName, string displayLabel, TimeSpan elapsed, CancellationToken cancellationToken);

    /// <summary>
    /// Host-specific activation gate invoked once the option-based <see cref="IsEnabled"/> check has passed.
    /// Returns <see langword="false"/> to keep the reporter dormant (e.g. when not running on the host CI).
    /// </summary>
    protected virtual bool OnActivating() => true;

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
            if (!test.SlowTestThreshold.IsDue(elapsed))
            {
                continue;
            }

            // The enumeration is a moving snapshot of the ConcurrentDictionary; a test can complete (and be
            // removed) between the snapshot and here. Skip it so we don't surface a slow-test notice for a
            // test that has already finished.
            if (!_inProgress.ContainsKey(entry.Key))
            {
                continue;
            }

            try
            {
                await EmitSlowTestAsync(test.TestName, test.DisplayLabel, elapsed, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogUnexpectedException(nameof(ScanOnceAsync), ex);
            }
        }
    }

    private sealed class InProgressTest
    {
        public InProgressTest(string testName, string displayLabel, DateTimeOffset startTime, TimeSpan threshold)
        {
            TestName = testName;
            DisplayLabel = displayLabel;
            StartTime = startTime;
            SlowTestThreshold = new(threshold);
        }

        public string TestName { get; }

        public string DisplayLabel { get; }

        public DateTimeOffset StartTime { get; }

        public SlowTestThresholdState SlowTestThreshold { get; }
    }
}
