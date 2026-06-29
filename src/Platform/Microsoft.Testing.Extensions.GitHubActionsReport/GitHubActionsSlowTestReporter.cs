// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

/// <summary>
/// Surfaces tests that are still running past a per-test threshold as GitHub Actions <c>::notice</c>
/// workflow commands, mirroring <c>AzureDevOpsSlowTestReporter</c>.
/// </summary>
/// <remarks>
/// This is a self-contained emitter for the GitHub Actions host. Once the platform-level
/// <c>IProgressEnricher</c> hook (issue #9139) ships, the surfacing/backoff logic should migrate onto it.
/// </remarks>
internal sealed class GitHubActionsSlowTestReporter : IDataConsumer, ITestSessionLifetimeHandler, IOutputDeviceDataProducer
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(1);

    private readonly IOutputDevice _outputDevice;
    private readonly ITask _task;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, InProgressTest> _inProgress = new(StringComparer.Ordinal);
    private readonly bool _isEnabled;
    private readonly TimeSpan _threshold;

    private volatile bool _active;
    private CancellationTokenSource? _loopCancellationTokenSource;
    private Task? _loopTask;

    public GitHubActionsSlowTestReporter(
        ICommandLineOptions commandLineOptions,
        IEnvironment environment,
        IOutputDevice outputDevice,
        ITask task,
        IClock clock,
        ILoggerFactory loggerFactory)
    {
        _outputDevice = outputDevice;
        _task = task;
        _clock = clock;
        _logger = loggerFactory.CreateLogger<GitHubActionsSlowTestReporter>();
        _isEnabled = GitHubActionsFeature.IsEnabled(commandLineOptions, environment, GitHubActionsCommandLineOptions.GitHubActionsSlowTestNotices);
        _threshold = TimeSpan.FromSeconds(GetThresholdSeconds(commandLineOptions));
    }

    public Type[] DataTypesConsumed { get; } = [typeof(TestNodeUpdateMessage)];

    public string Uid => nameof(GitHubActionsSlowTestReporter);

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => GitHubActionsResources.DisplayName;

    public string Description => GitHubActionsResources.Description;

    public Task<bool> IsEnabledAsync() => Task.FromResult(_isEnabled);

    public Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        try
        {
            testSessionContext.CancellationToken.ThrowIfCancellationRequested();

            _active = false;
            _inProgress.Clear();

            if (!_isEnabled)
            {
                return Task.CompletedTask;
            }

            _loopCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(testSessionContext.CancellationToken);
            _active = true;
            _loopTask = _task.RunLongRunning(() => ScanLoopAsync(_loopCancellationTokenSource.Token), nameof(GitHubActionsSlowTestReporter), _loopCancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogUnexpectedException(nameof(OnTestSessionStartingAsync), ex);
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
            TestNodeStateProperty? state = update.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>();
            if (state is InProgressTestNodeStateProperty)
            {
                string testName = TestNodeIdentity.GetTestName(update.TestNode);
                _inProgress[uid] = new InProgressTest(testName, _clock.UtcNow, _threshold);
            }
            else if (state is not null)
            {
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
                // Expected during normal shutdown.
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

    // Internal for unit testing: performs a single surfacing pass at the given 'now'.
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
        string line = BuildNoticeLine(test.TestName, elapsed);
        await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData(line), cancellationToken).ConfigureAwait(false);
    }

    internal static /* for testing */ string BuildNoticeLine(string testName, TimeSpan elapsed)
    {
        string message = string.Format(
            CultureInfo.InvariantCulture,
            GitHubActionsResources.SlowTestStillRunning,
            testName,
            ((long)elapsed.TotalSeconds).ToString(CultureInfo.InvariantCulture));
        string title = string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.SlowTestNoticeTitle, testName);

        return string.Format(
            CultureInfo.InvariantCulture,
            "::notice title={0}::{1}",
            GitHubActionsEscaper.EscapeProperty(title),
            GitHubActionsEscaper.EscapeData(message));
    }

    private static int GetThresholdSeconds(ICommandLineOptions commandLineOptions)
        => commandLineOptions.TryGetOptionArgumentList(GitHubActionsCommandLineOptions.GitHubActionsSlowTestThreshold, out string[]? arguments)
            && arguments is [string value]
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds)
            && seconds >= 1
                ? seconds
                : GitHubActionsCommandLineOptions.SlowTestThresholdDefaultSeconds;

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
