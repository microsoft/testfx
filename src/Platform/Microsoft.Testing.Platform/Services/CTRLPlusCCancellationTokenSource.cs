// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Platform.Services;

/// <summary>
/// Two-phase, Ctrl+C-aware <see cref="ITestApplicationCancellationTokenSource"/>.
/// </summary>
/// <remarks>
/// Phase machine (see RFC "Phased graceful shutdown for MTP", issue #5345):
/// <code>
/// RUNNING ──Ctrl+C / Cancel()──▶ DRAINING ──grace elapsed / 2nd Ctrl+C / Abort()──▶ ABORTING
///                                                                              ──3rd Ctrl+C──▶ (process terminated by runtime)
/// </code>
/// <para>
/// Transitions are idempotent and one-way. Existing consumers reading
/// <see cref="CancellationToken"/> automatically observe Draining (back-compat).
/// </para>
/// </remarks>
internal sealed class CTRLPlusCCancellationTokenSource : ITestApplicationCancellationTokenSource, IDisposable
{
    // Conservative defaults inspired by .NET HostOptions.ShutdownTimeout (30s) and
    // Vitest's teardownTimeout (10s). Will become CLI options in a follow-up
    // (--shutdown-grace-period, --shutdown-abort-timeout).
    // TODO(#5345): wire to PlatformCommandLineProvider.
    internal static readonly TimeSpan DefaultGracePeriod = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan DefaultAbortTimeout = TimeSpan.FromSeconds(10);

    private const int PhaseRunning = 0;
    private const int PhaseDraining = 1;
    private const int PhaseAborting = 2;

    private readonly CancellationTokenSource _drainingCts = new();
    private readonly CancellationTokenSource _abortingCts = new();
    private readonly TimeSpan _gracePeriod;
    private readonly TimeSpan _abortTimeout;
    private readonly IEnvironment _environment;
    private readonly ILogger? _logger;

    private int _phase = PhaseRunning;
    private int _ctrlCCount;

    public CTRLPlusCCancellationTokenSource(IConsole? console = null, ILogger? logger = null)
        : this(console, logger, DefaultGracePeriod, DefaultAbortTimeout, environment: null)
    {
    }

    // Test-friendly overload so we can exercise the phase machine without waiting 30s.
    internal CTRLPlusCCancellationTokenSource(
        IConsole? console,
        ILogger? logger,
        TimeSpan gracePeriod,
        TimeSpan abortTimeout,
        IEnvironment? environment = null)
    {
        _gracePeriod = gracePeriod;
        _abortTimeout = abortTimeout;
        _environment = environment ?? new SystemEnvironment();
        _logger = logger;

        if (console is not null && !IsCancelKeyPressNotSupported())
        {
            console.CancelKeyPress += OnConsoleCancelKeyPressed;
        }
    }

    [SupportedOSPlatformGuard("android")]
    [SupportedOSPlatformGuard("ios")]
    [SupportedOSPlatformGuard("tvos")]
    [SupportedOSPlatformGuard("wasi")]
    [SupportedOSPlatformGuard("browser")]
    private static bool IsCancelKeyPressNotSupported()
        => OperatingSystem.IsAndroid() ||
            OperatingSystem.IsIOS() ||
            OperatingSystem.IsTvOS() ||
            OperatingSystem.IsWasi() ||
            OperatingSystem.IsBrowser();

    /// <inheritdoc />
    public CancellationToken CancellationToken => _drainingCts.Token;

    /// <inheritdoc />
    public CancellationToken DrainingToken => _drainingCts.Token;

    /// <inheritdoc />
    public CancellationToken AbortingToken => _abortingCts.Token;

    internal int CurrentPhase => Volatile.Read(ref _phase);

    public void CancelAfter(TimeSpan timeout) => _drainingCts.CancelAfter(timeout);

    /// <inheritdoc />
    public void Cancel() => EnterDraining();

    /// <inheritdoc />
    public void Abort()
    {
        EnterDraining();
        EnterAborting();
    }

    public void Dispose()
    {
        _drainingCts.Dispose();
        _abortingCts.Dispose();
    }

    private void OnConsoleCancelKeyPressed(object? sender, ConsoleCancelEventArgs e)
    {
        int count = Interlocked.Increment(ref _ctrlCCount);

        switch (count)
        {
            case 1:
                // 1st Ctrl+C: cooperative cancel.
                e.Cancel = true;
                EnterDraining();
                break;
            case 2:
                // 2nd Ctrl+C: escalate to abort.
                e.Cancel = true;
                EnterAborting();
                break;
            default:
                // 3rd+ Ctrl+C: stop intercepting and let the runtime terminate
                // the process. This matches docker compose / kubectl / npm UX:
                // the user has explicitly asked us to die.
                e.Cancel = false;
                break;
        }
    }

    private void EnterDraining()
    {
        if (Interlocked.CompareExchange(ref _phase, PhaseDraining, PhaseRunning) != PhaseRunning)
        {
            return;
        }

        try
        {
            _drainingCts.Cancel();
        }
        catch (AggregateException ex)
        {
            _logger?.LogWarning($"Exception during shutdown (Draining):\n{ex}");
        }

        // Auto-escalate to Aborting after the grace period.
        if (_gracePeriod > TimeSpan.Zero && _gracePeriod != Timeout.InfiniteTimeSpan)
        {
            ScheduleEscalation(_gracePeriod, EnterAborting);
        }
        else if (_gracePeriod == TimeSpan.Zero)
        {
            EnterAborting();
        }
    }

    private void EnterAborting()
    {
        if (Interlocked.Exchange(ref _phase, PhaseAborting) == PhaseAborting)
        {
            return;
        }

        try
        {
            _abortingCts.Cancel();
        }
        catch (AggregateException ex)
        {
            _logger?.LogWarning($"Exception during shutdown (Aborting):\n{ex}");
        }

        // After abort timeout, if the host is still alive, hard-terminate.
        // FailFast is intentional: at this point we asked twice and waited; any
        // remaining work has had its chance. This is the safety net that breaks
        // hangs in non-cooperative frameworks (issue #5345).
        if (_abortTimeout > TimeSpan.Zero && _abortTimeout != Timeout.InfiniteTimeSpan)
        {
            ScheduleEscalation(_abortTimeout, ForceTerminate);
        }
    }

    private static void ScheduleEscalation(TimeSpan delay, Action action)
    {
        // Fire-and-forget timer. We don't dispose: the host is shutting down anyway,
        // and a short-lived CTS is cheaper than holding a Timer reference we'd need
        // to manage across the phase machine.
        var timerCts = new CancellationTokenSource(delay);
        timerCts.Token.Register(action);
    }

    private void ForceTerminate()
    {
        _logger?.LogWarning(
            $"Shutdown grace exhausted ({_gracePeriod} + {_abortTimeout}); terminating host.");
        _environment.FailFast("Test platform shutdown grace period exhausted.");
    }
}
