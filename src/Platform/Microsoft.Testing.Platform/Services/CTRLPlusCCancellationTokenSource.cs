// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Platform.Services;

internal sealed class CTRLPlusCCancellationTokenSource : ITestApplicationCancellationTokenSource, IDisposable
{
    private const int StateIdle = 0;
    private const int StateCancelling = 1;
    private const int StateForcing = 2;

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly IEnvironment _environment;
    private readonly ILogger? _logger;
    private readonly IConsole? _subscribedConsole;
    private int _state = StateIdle;
    private int _disposed;

    public CTRLPlusCCancellationTokenSource(IConsole? console = null, ILogger? logger = null, IEnvironment? environment = null)
    {
        if (console is not null && !IsCancelKeyPressNotSupported())
        {
            console.CancelKeyPress += OnConsoleCancelKeyPressed;
            _subscribedConsole = console;
        }

        _environment = environment ?? new SystemEnvironment();
        _logger = logger;
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

    public void CancelAfter(TimeSpan timeout) => _cancellationTokenSource.CancelAfter(timeout);

    public CancellationToken CancellationToken
        => _cancellationTokenSource.Token;

    private void OnConsoleCancelKeyPressed(object? sender, ConsoleCancelEventArgs e)
    {
        // Suppress the runtime's default Ctrl+C handling so we control the exit code on
        // both the first (cooperative) and the second (force-exit) press.
        e.Cancel = true;

        // The state machine counts user Ctrl+C presses *independently* of external cancellation
        // sources (timeout, max-failed-tests, explicit Cancel()). This honors the contract
        // advertised next to the "Cancelling..." message ("Press Ctrl+C again to force exit.")
        // regardless of who initiated the cancellation: the user must always press Ctrl+C twice
        // to force-exit.
        if (Interlocked.CompareExchange(ref _state, StateCancelling, StateIdle) == StateIdle)
        {
            // First user Ctrl+C: cooperative cancellation. If the token was already cancelled
            // by an external source this is effectively a no-op, but we still transitioned the
            // state so the next press goes to force-exit.
            try
            {
                _cancellationTokenSource.Cancel();
            }
            catch (AggregateException ex)
            {
                _logger?.LogWarning($"Exception during CTRLPlusCCancellationTokenSource cancel:\n{ex}");
            }

            return;
        }

        // Second user Ctrl+C: force-exit. We intentionally do not print an extra
        // "Forcing exit..." message here because the IConsole abstraction has no stderr channel
        // and writing to stdout would corrupt the JSON document produced by --list-tests json.
        // The user already saw the "Press Ctrl+C again to force exit." hint, so the exit itself
        // is the confirmation. Any subsequent presses are suppressed by the StateForcing guard.
        if (Interlocked.CompareExchange(ref _state, StateForcing, StateCancelling) == StateCancelling)
        {
            _environment.Exit((int)ExitCode.TestSessionAborted);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // We stored the console reference only when subscription was actually performed
        // (i.e. when CancelKeyPress is supported on this platform), so we can safely call -=
        // on the same supported-platform paths.
        if (_subscribedConsole is not null && !IsCancelKeyPressNotSupported())
        {
            _subscribedConsole.CancelKeyPress -= OnConsoleCancelKeyPressed;
        }

        _cancellationTokenSource.Dispose();
    }

    public void Cancel()
        => _cancellationTokenSource.Cancel();
}
