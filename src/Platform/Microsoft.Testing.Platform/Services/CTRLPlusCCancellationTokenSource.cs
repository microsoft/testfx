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
    private readonly IConsole? _console;
    private readonly object _escalationLock = new();

    // Fire-and-forget escalation timers, retained so we can dispose them in
    // Dispose() and prevent callbacks from firing after the host has shut down.
    private List<CancellationTokenSource>? _escalations;

    private int _phase = PhaseRunning;
    private int _ctrlCCount;
    private int _disposed;

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
            _console = console;
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
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // Detach from Console.CancelKeyPress so we don't keep this instance alive
        // through the static event and don't invoke the handler after disposal.
        if (_console is not null && !IsCancelKeyPressNotSupported())
        {
            _console.CancelKeyPress -= OnConsoleCancelKeyPressed;
        }

        List<CancellationTokenSource>? escalations;
        lock (_escalationLock)
        {
            escalations = _escalations;
            _escalations = null;
        }

        if (escalations is not null)
        {
            foreach (CancellationTokenSource escalation in escalations)
            {
                escalation.Dispose();
            }
        }

        _drainingCts.Dispose();
        _abortingCts.Dispose();
    }

    private void OnConsoleCancelKeyPressed(object? sender, ConsoleCancelEventArgs e)
    {
        // Guard against handlers that may race with Dispose (the unsubscribe in
        // Dispose() is best-effort: the event invocation list could already have
        // captured this delegate before we removed it).
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        int count = Interlocked.Increment(ref _ctrlCCount);

        switch (count)
        {
            case 1:
                // 1st Ctrl+C: cooperative cancel.
                e.Cancel = true;
                EnterDraining();
                break;
            case 2:
                // 2nd Ctrl+C: escalate to abort. Go through Abort() (which calls
                // EnterDraining() then EnterAborting()) so the
                // Running → Draining → Aborting invariant holds even if a future
                // refactor lets a 2nd Ctrl+C arrive before the 1st has been
                // processed (or before EnterDraining was reached at all).
                e.Cancel = true;
                Abort();
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
        // Aborting implies Draining: ensure the legacy CancellationToken /
        // DrainingToken are always signaled when Aborting is signaled, even if
        // EnterAborting is invoked from a Running state (e.g., via Abort() or a
        // future direct trigger). EnterDraining is idempotent.
        EnterDraining();

        if (Interlocked.CompareExchange(ref _phase, PhaseAborting, PhaseDraining) != PhaseDraining)
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

    private void ScheduleEscalation(TimeSpan delay, Action action)
    {
        var timerCts = new CancellationTokenSource(delay);
        timerCts.Token.Register(action);

        // Retain the CTS so Dispose() can cancel pending escalations and
        // release the underlying timer. Without this, the timer would run to
        // completion and could invoke the callback after the source is disposed.
        lock (_escalationLock)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                timerCts.Dispose();
                return;
            }

            (_escalations ??= []).Add(timerCts);
        }
    }

    private void ForceTerminate()
    {
        _logger?.LogWarning(
            $"Shutdown grace exhausted ({_gracePeriod} + {_abortTimeout}); terminating host.");
        _environment.FailFast("Test platform shutdown grace period exhausted.");
    }
}
