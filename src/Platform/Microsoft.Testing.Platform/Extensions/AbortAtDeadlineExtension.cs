// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
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
/// armed at construction). It implements <see cref="IDataConsumer"/> so the message bus keeps a live
/// reference to it for the duration of the run (which also keeps its timer alive); it consumes no
/// message types, so the bus keeps that reference without routing any message to it. It also implements
/// <see cref="ITestSessionLifetimeHandler"/> as a backstop and is registered as a service so the host can call
/// <see cref="NotifyTestExecutionCompleted"/> the moment the test framework invoker returns. That early signal
/// prevents a timer firing while reporters finalize an already-finished run from marking it deadline-truncated.
/// </remarks>
internal sealed partial class AbortAtDeadlineExtension : IDataConsumer, ITestSessionLifetimeHandler, IOutputDeviceDataProducer, IDisposable
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
    private readonly IClock _clock;
    private readonly List<string> _startupWarnings = [];
    private readonly DateTimeOffset? _stopAt;
    private readonly Timer? _timer;

    // How long a single best-effort diagnostic may take before it is abandoned. Injectable only so a test
    // can exercise the bound without waiting DefaultReportTimeout for it; production always uses the default.
    private readonly TimeSpan _reportTimeout;

    // Serializes publishing _handleDeadlineTask against Dispose reading it, so the timer callback and
    // disposal cannot interleave in a way that starts the handler after Dispose has already returned.
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private int _handled;
    private int _startupWarningsDisplayed;
    private volatile bool _disposed;

    // Which of "test execution finished" and "the deadline took the run" happened first. Both transitions are
    // made under _lock and only out of Running, so they are mutually exclusive: whichever takes the lock first
    // wins and the other becomes a no-op. Read without the lock on the timer callback's fast-path, so it is
    // volatile.
    private volatile RunState _state;
    private Task? _handleDeadlineTask;

    /// <summary>
    /// Bounded wait applied on disposal to let an in-flight deadline handler finish reporting before
    /// the host tears down, without letting a wedged stop hang disposal forever.
    /// </summary>
    private static readonly TimeSpan DisposeDrainTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Bounded wait applied to each best-effort diagnostic on the deadline path, so a logger or output
    /// device that never completes cannot hold up the graceful stop it precedes.
    /// </summary>
    /// <remarks>
    /// Generous enough that a healthy provider never hits it, and short relative to the margin the
    /// deadline leaves for the stop to take effect.
    /// </remarks>
    private static readonly TimeSpan DefaultReportTimeout = TimeSpan.FromSeconds(10);

    public AbortAtDeadlineExtension(
        IEnvironment environment,
        IClock clock,
        IGracefulStopTestExecutionCapability? capability,
        IStopPoliciesService policiesService,
        ITestApplicationCancellationTokenSource cancellationTokenSource,
        IOutputDevice outputDevice,
        ILoggerFactory loggerFactory,
        TimeSpan? reportTimeout = null,
        bool isHangDumpEnabled = false)
    {
        _capability = capability;
        _policiesService = policiesService;
        _cancellationTokenSource = cancellationTokenSource;
        _outputDevice = outputDevice;
        _logger = loggerFactory.CreateLogger(nameof(AbortAtDeadlineExtension));
        _clock = clock;
        _reportTimeout = reportTimeout ?? DefaultReportTimeout;

        if (!DeadlineHelper.TryGetDeadline(environment, out DateTimeOffset deadline))
        {
            // Distinguish "opt-in is off" (variable unset) from "set but malformed". The former is the
            // normal case and stays silent; the latter is a configuration mistake worth a warning.
            string? raw = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE);
            if (!RoslynString.IsNullOrWhiteSpace(raw))
            {
                TryLog(() => _logger.LogWarning($"Environment variable '{EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE}' is set to '{raw}' but could not be parsed as an absolute ISO 8601 instant. Deadline-aware cancellation is disabled."));
                _startupWarnings.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.AbortAtDeadlineInvalidDeadlineWarning,
                    EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE,
                    raw));
            }

            return;
        }

        TimeSpan stopMargin = DeadlineHelper.GetStopMargin(environment);
        TimeSpan dumpMargin = DeadlineHelper.GetDumpMargin(environment);
        DateTimeOffset stopAt = DeadlineHelper.SubtractSaturating(deadline, stopMargin);
        bool areMarginsInverted = isHangDumpEnabled && dumpMargin >= stopMargin;
        if (areMarginsInverted)
        {
            _startupWarnings.Add(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.AbortAtDeadlineInvalidMarginOrderWarning,
                dumpMargin,
                stopMargin));
        }

        // Log the resolved instants and margins so any misconfiguration is visible.
        TryLog(() =>
        {
            _logger.LogInformation($"Deadline-aware cancellation: deadline={deadline:o}, stopMargin={stopMargin}, dumpMargin={dumpMargin}, graceful stop scheduled at {stopAt:o} (UTC).");

            // stopMargin is meant to be larger than dumpMargin so the graceful stop is attempted before
            // the hang dump. Warn when the ordering is inverted rather than silently misbehaving.
            if (areMarginsInverted)
            {
                _logger.LogWarning($"Deadline dump margin ({dumpMargin}) is greater than or equal to the stop margin ({stopMargin}). The graceful stop is meant to run before the hang dump; with these margins the hang dump may fire first.");
            }
        });

        if (capability is null)
        {
            // A deadline is configured but this framework cannot stop gracefully, so nothing is armed.
            // Surface it rather than silently doing nothing.
            TryLog(() => _logger.LogWarning($"Environment variable '{EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE}' is set but the test framework does not support graceful stop ('{nameof(IGracefulStopTestExecutionCapability)}'); the platform cannot stop early at the deadline."));
            _startupWarnings.Add(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.AbortAtDeadlineCapabilityUnavailableWarning,
                EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE,
                nameof(IGracefulStopTestExecutionCapability)));
            return;
        }

        _stopAt = stopAt;

        // Timer cannot represent delays above ~49.7 days. Arm it in bounded chunks and re-check the
        // absolute instant on every callback so a far-future deadline never fires early.
        _timer = new Timer(static state => ((AbortAtDeadlineExtension)state!).OnTimerElapsed(), this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _timer.Change(DeadlineHelper.GetTimerDueTime(stopAt, clock.UtcNow), Timeout.InfiniteTimeSpan);
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
    public async Task<bool> IsEnabledAsync()
    {
        if (Interlocked.Exchange(ref _startupWarningsDisplayed, 1) == 0)
        {
            foreach (string warning in _startupWarnings)
            {
                await TryReportAsync(
                    () => _outputDevice.DisplayAsync(
                        this,
                        new WarningMessageOutputDeviceData(warning),
                        _cancellationTokenSource.CancellationToken),
                    "Failed to display a deadline configuration warning.").ConfigureAwait(false);
            }
        }

        return _stopAt.HasValue && _capability is not null;
    }

    /// <inheritdoc />
    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <inheritdoc />
    public Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
        => Task.CompletedTask;

    /// <inheritdoc />
    public Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        NotifyTestExecutionCompleted();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Disarms the deadline because test execution has finished.
    /// </summary>
    /// <remarks>
    /// The host calls this as soon as the test framework invoker returns, before end-of-session draining and
    /// reporting begin. That instant matters: from here on the deadline is moot, because every test that was
    /// going to run has run. Doing it through <see cref="ITestSessionLifetimeHandler"/> instead would be far
    /// too late -- session-end notification first drains the message bus, then runs the non-consumer handlers,
    /// then the consumer handlers in registration order, and this extension is a consumer appended after the
    /// reporters. A deadline reached anywhere in that window would still mark a fully-executed run as
    /// truncated (exit code 15).
    /// The transition is made under the same lock that claims the deadline, and only out of
    /// <see cref="RunState.Running"/>, so it is atomic against claiming the stop. Completion after the deadline
    /// claim is recorded separately so a rejected stop can restore the completed state without invoking
    /// framework code while holding the lock.
    /// The timer itself is left to be disposed at host teardown; the state is what makes any late fire a no-op.
    /// </remarks>
    public void NotifyTestExecutionCompleted()
    {
        lock (_lock)
        {
            if (_state == RunState.Running)
            {
                _state = RunState.Completed;
            }
            else if (_state == RunState.DeadlineClaimed)
            {
                _state = RunState.DeadlineClaimedAndCompleted;
            }
        }
    }
}
