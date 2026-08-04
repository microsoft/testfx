// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Extensions;

/// <summary>
/// Reacts to a CI-imposed hard-cancel deadline (see <see cref="DeadlineHelper"/>): a short margin
/// before the deadline it asks the test framework to gracefully stop scheduling new tests, so the
/// in-flight session can end normally and all reporters (TRX/HTML/AzDO live run) get to finalize
/// before the CI runner hard-kills the process.
/// </summary>
/// <remarks>
/// This is a prototype. It is timer-driven (the deadline is an absolute instant, so the timer is
/// armed at construction). It implements <see cref="IDataConsumer"/> only so the message bus keeps
/// a live reference to it for the duration of the run (which also keeps its timer alive). It
/// consumes no message types, so the bus keeps that reference without routing any message to it.
/// </remarks>
internal sealed class AbortAtDeadlineExtension : IDataConsumer, IOutputDeviceDataProducer, IDisposable
#if NETCOREAPP
#pragma warning disable SA1001 // Commas should be spaced correctly
    , IAsyncDisposable
#pragma warning restore SA1001 // Commas should be spaced correctly
#endif
{
    private readonly IGracefulStopTestExecutionCapability? _capability;
    private readonly IStopPoliciesService _policiesService;
    private readonly ITestApplicationCancellationTokenSource _cancellationTokenSource;
    private readonly IOutputDevice _outputDevice;
    private readonly ILogger _logger;
    private readonly DateTimeOffset? _stopAt;
    private readonly Timer? _timer;
    private int _handled;
    private volatile bool _disposed;
    private Task? _handleDeadlineTask;

    /// <summary>
    /// Bounded wait applied on disposal to let an in-flight deadline handler finish reporting before
    /// the host tears down, without letting a wedged stop hang disposal forever.
    /// </summary>
    private static readonly TimeSpan DisposeDrainTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// <see cref="Timer"/> throws for due times above ~49.7 days (its internal limit is
    /// <see cref="uint.MaxValue"/> milliseconds). A deadline that far out is effectively "never"
    /// for a test run, so we clamp to this maximum instead of throwing at construction.
    /// </summary>
    private static readonly TimeSpan MaxTimerDueTime = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    public AbortAtDeadlineExtension(
        IEnvironment environment,
        IClock clock,
        IGracefulStopTestExecutionCapability? capability,
        IStopPoliciesService policiesService,
        ITestApplicationCancellationTokenSource cancellationTokenSource,
        IOutputDevice outputDevice,
        ILoggerFactory loggerFactory)
    {
        _capability = capability;
        _policiesService = policiesService;
        _cancellationTokenSource = cancellationTokenSource;
        _outputDevice = outputDevice;
        _logger = loggerFactory.CreateLogger(nameof(AbortAtDeadlineExtension));

        if (!DeadlineHelper.TryGetDeadline(environment, out DateTimeOffset deadline))
        {
            // Distinguish "opt-in is off" (variable unset) from "set but malformed". The former is the
            // normal case and stays silent; the latter is a configuration mistake worth a warning.
            string? raw = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE);
            if (!RoslynString.IsNullOrWhiteSpace(raw))
            {
                TryLog(() => _logger.LogWarning($"Environment variable '{EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE}' is set to '{raw}' but could not be parsed as an absolute ISO 8601 instant. Deadline-aware cancellation is disabled."));
            }

            return;
        }

        TimeSpan stopMargin = DeadlineHelper.GetStopMargin(environment);
        TimeSpan dumpMargin = DeadlineHelper.GetDumpMargin(environment);
        DateTimeOffset stopAt = DeadlineHelper.SubtractSaturating(deadline, stopMargin);

        // A deadline given without an offset is parsed as UTC (AssumeUniversal), which is easy to get
        // wrong. Log the resolved instants and margins so a misconfigured offset is visible.
        TryLog(() =>
        {
            _logger.LogInformation($"Deadline-aware cancellation: deadline={deadline:o}, stopMargin={stopMargin}, dumpMargin={dumpMargin}, graceful stop scheduled at {stopAt:o} (UTC).");

            // stopMargin is meant to be larger than dumpMargin so the graceful stop is attempted before
            // the hang dump. Warn when the ordering is inverted rather than silently misbehaving.
            if (dumpMargin >= stopMargin)
            {
                _logger.LogWarning($"Deadline dump margin ({dumpMargin}) is greater than or equal to the stop margin ({stopMargin}). The graceful stop is meant to run before the hang dump; with these margins the hang dump may fire first.");
            }
        });

        if (capability is null)
        {
            // A deadline is configured but this framework cannot stop gracefully, so nothing is armed.
            // Surface it rather than silently doing nothing.
            TryLog(() => _logger.LogWarning($"Environment variable '{EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE}' is set but the test framework does not support graceful stop ('{nameof(IGracefulStopTestExecutionCapability)}'); the platform cannot stop early at the deadline."));
            return;
        }

        _stopAt = stopAt;

        // The deadline is absolute wall-clock time, so we can arm a one-shot timer now. If the
        // computed instant is already in the past, fire as soon as possible. If it is farther
        // out than a Timer can represent, clamp it: the run (and this timer) will be disposed
        // long before the clamped due time elapses, so the timer never fires early in practice.
        TimeSpan dueTime = stopAt - clock.UtcNow;
        if (dueTime < TimeSpan.Zero)
        {
            dueTime = TimeSpan.Zero;
        }
        else if (dueTime > MaxTimerDueTime)
        {
            dueTime = MaxTimerDueTime;
        }

        _timer = new Timer(static state => ((AbortAtDeadlineExtension)state!).OnDeadlineReached(), this, dueTime, Timeout.InfiniteTimeSpan);
    }

    private static void TryLog(Action logAction)
    {
        try
        {
            logAction();
        }
        catch (Exception)
        {
            // Construction-time diagnostics are best-effort: a logger failure must never break test
            // framework construction.
        }
    }

    // No message types are consumed: this extension implements IDataConsumer only to keep a live
    // reference on the message bus (see the remark on the class). Returning an empty list avoids the
    // bus routing every test result to a no-op ConsumeAsync, which would be O(test-count) overhead.
    public Type[] DataTypesConsumed { get; } = [];

    /// <inheritdoc />
    public string Uid => nameof(AbortAtDeadlineExtension);

    /// <inheritdoc />
    public string Version => PlatformVersion.Version;

    /// <inheritdoc />
    public string DisplayName => nameof(AbortAtDeadlineExtension);

    /// <inheritdoc />
    public string Description { get; } = PlatformResources.AbortAtDeadlineDescription;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(_stopAt.HasValue && _capability is not null);

    /// <inheritdoc />
    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
        => Task.CompletedTask;

    private void OnDeadlineReached()
    {
        // Do not start deadline handling once we are tearing down.
        if (_disposed)
        {
            return;
        }

        // Ensure we react only once.
        if (Interlocked.Exchange(ref _handled, 1) != 0)
        {
            return;
        }

        // The timer callback runs on a runtime that may be single-threaded (browser/WASI), where
        // Task.Run can queue work that never executes. Invoke the async method directly; it is
        // already asynchronous, so it yields at the first await without blocking the timer thread.
        // Store the task so disposal can drain it instead of racing teardown.
        _handleDeadlineTask = HandleDeadlineAsync();
    }

    private async Task HandleDeadlineAsync()
    {
        if (_capability is not { } capability)
        {
            return;
        }

        // Diagnostics are best-effort and must never prevent the graceful stop below: MTP's logger
        // and output device both propagate provider exceptions, so a failure here would otherwise
        // skip the deadline handling entirely.
        try
        {
            await _logger.LogInformationAsync($"Deadline approaching (stop scheduled at {_stopAt:o}). Requesting graceful stop of test execution.").ConfigureAwait(false);
            await _outputDevice.DisplayAsync(
                this,
                new FormattedTextOutputDeviceData(PlatformResources.AbortAtDeadlineMessage),
                _cancellationTokenSource.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Even this failure log is best-effort. If the logger itself is what threw, logging
            // again could re-throw and skip the stop below, so swallow anything here.
            try
            {
                await _logger.LogErrorAsync("Failed to report the approaching deadline.", ex).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Ignore: the graceful stop below is the only thing that must happen.
            }
        }

        try
        {
            // Mark the run as deadline-triggered BEFORE requesting the stop. StopTestExecutionAsync can
            // unblock the framework and let it finalize the session (and compute the process exit code)
            // right away, so setting the flag afterwards would race that finalization and the deadline
            // exit code could be missed. The graceful stop does not cancel the token, so unlike abort
            // this flag is not set by a token-registered callback; set it here first. Setting it is
            // synchronous inside ExecuteDeadlineCallbacksAsync, so it is visible before the stop begins.
            await _policiesService.ExecuteDeadlineCallbacksAsync().ConfigureAwait(false);

            await capability.StopTestExecutionAsync(_cancellationTokenSource.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Best-effort: never let the timer callback crash the process during teardown. The error
            // log is itself best-effort (the logger may be what threw), so guard it too.
            try
            {
                await _logger.LogErrorAsync("Failed to request graceful stop at deadline.", ex).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Ignore: nothing else can be done here.
            }
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _timer?.Dispose();
#if !NETCOREAPP
        // netstandard2.0 has no ValueTask/IAsyncDisposable, so there is no async drain path (see
        // DisposeAsync below, which is netcoreapp-only). Fall back to a bounded blocking drain so an
        // in-flight deadline handler can finish reporting before teardown, without letting a wedged
        // graceful stop hang disposal. HandleDeadlineAsync swallows its own failures, so this wait
        // never observes a fault.
        try
        {
            _handleDeadlineTask?.Wait(DisposeDrainTimeout);
        }
        catch (Exception)
        {
            // Best-effort drain: disposal must never throw.
        }
#endif
    }

#if NETCOREAPP
    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _timer?.Dispose();

        // Drain an in-flight deadline handler so its reporting can finish before the host tears down,
        // but bound the wait so a wedged graceful stop cannot hang disposal. HandleDeadlineAsync
        // swallows its own failures, so awaiting the completed task here never throws.
        Task? handleTask = _handleDeadlineTask;
        if (handleTask is not null)
        {
            Task completed = await Task.WhenAny(handleTask, Task.Delay(DisposeDrainTimeout)).ConfigureAwait(false);
            if (completed == handleTask)
            {
                await handleTask.ConfigureAwait(false);
            }
        }
    }
#endif
}
