// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
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
/// a live reference to it for the duration of the run (which also keeps its timer alive); it does
/// not act on any consumed message.
/// </remarks>
internal sealed class AbortAtDeadlineExtension : IDataConsumer, IOutputDeviceDataProducer, IDisposable
{
    private readonly IGracefulStopTestExecutionCapability? _capability;
    private readonly ITestApplicationCancellationTokenSource _cancellationTokenSource;
    private readonly IOutputDevice _outputDevice;
    private readonly ILogger _logger;
    private readonly DateTimeOffset? _stopAt;
    private readonly Timer? _timer;
    private int _handled;

    public AbortAtDeadlineExtension(
        IEnvironment environment,
        IClock clock,
        IGracefulStopTestExecutionCapability? capability,
        ITestApplicationCancellationTokenSource cancellationTokenSource,
        IOutputDevice outputDevice,
        ILoggerFactory loggerFactory)
    {
        _capability = capability;
        _cancellationTokenSource = cancellationTokenSource;
        _outputDevice = outputDevice;
        _logger = loggerFactory.CreateLogger(nameof(AbortAtDeadlineExtension));

        if (DeadlineHelper.TryGetDeadline(environment, out DateTimeOffset deadline) && capability is not null)
        {
            DateTimeOffset stopAt = DeadlineHelper.SubtractSaturating(deadline, DeadlineHelper.GetStopMargin(environment));
            _stopAt = stopAt;

            // The deadline is absolute wall-clock time, so we can arm a one-shot timer now. If the
            // computed instant is already in the past, fire as soon as possible.
            TimeSpan dueTime = stopAt - clock.UtcNow;
            if (dueTime < TimeSpan.Zero)
            {
                dueTime = TimeSpan.Zero;
            }

            _timer = new Timer(static state => ((AbortAtDeadlineExtension)state!).OnDeadlineReached(), this, dueTime, Timeout.InfiniteTimeSpan);
        }
    }

    public Type[] DataTypesConsumed { get; } = [typeof(TestNodeUpdateMessage)];

    /// <inheritdoc />
    public string Uid => nameof(AbortAtDeadlineExtension);

    /// <inheritdoc />
    public string Version => PlatformVersion.Version;

    /// <inheritdoc />
    public string DisplayName => nameof(AbortAtDeadlineExtension);

    /// <inheritdoc />
    public string Description => "Gracefully stops the test run shortly before a CI-imposed deadline so reports can be finalized.";

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(_stopAt.HasValue && _capability is not null);

    /// <inheritdoc />
    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
        => Task.CompletedTask;

    private void OnDeadlineReached()
    {
        // Ensure we react only once.
        if (Interlocked.Exchange(ref _handled, 1) != 0)
        {
            return;
        }

        _ = Task.Run(HandleDeadlineAsync);
    }

    private async Task HandleDeadlineAsync()
    {
        try
        {
            if (_capability is not { } capability)
            {
                return;
            }

            await _logger.LogInformationAsync($"Deadline approaching (stop scheduled at {_stopAt:o}). Requesting graceful stop of test execution.").ConfigureAwait(false);

            CancellationToken cancellationToken = _cancellationTokenSource.CancellationToken;
            await _outputDevice.DisplayAsync(
                this,
                new FormattedTextOutputDeviceData("Deadline approaching: gracefully stopping the test run so reports can be finalized before the CI hard-cancel."),
                cancellationToken).ConfigureAwait(false);

            await capability.StopTestExecutionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Best-effort: never let the timer callback crash the process during teardown.
            await _logger.LogErrorAsync("Failed to request graceful stop at deadline.", ex).ConfigureAwait(false);
        }
    }

    public void Dispose() => _timer?.Dispose();
}
