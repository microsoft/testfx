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
    private int _state = StateIdle;

    public CTRLPlusCCancellationTokenSource(IConsole? console = null, ILogger? logger = null, IEnvironment? environment = null)
    {
        if (console is not null && !IsCancelKeyPressNotSupported())
        {
            console.CancelKeyPress += OnConsoleCancelKeyPressed;
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

        // If cancellation is already in progress (from a previous Ctrl+C or from another source
        // like a timeout), treat this Ctrl+C as a request to force-exit the process. This honors
        // the contract advertised next to the "Cancelling..." message: "Press Ctrl+C again to
        // force exit.".
        if (_cancellationTokenSource.IsCancellationRequested)
        {
            if (Interlocked.Exchange(ref _state, StateForcing) == StateForcing)
            {
                // Another Ctrl+C already triggered the force-exit; suppress the duplicate.
                return;
            }

            // We intentionally do not print an extra "Forcing exit..." message here because the
            // IConsole abstraction has no stderr channel and writing to stdout would corrupt the
            // JSON document produced by --list-tests json. The user already saw the
            // "Press Ctrl+C again to force exit." hint, so the exit itself is the confirmation.
            _environment.Exit((int)ExitCode.TestSessionAborted);
            return;
        }

        if (Interlocked.CompareExchange(ref _state, StateCancelling, StateIdle) != StateIdle)
        {
            // Another thread already transitioned the state; nothing to do.
            return;
        }

        try
        {
            _cancellationTokenSource.Cancel();
        }
        catch (AggregateException ex)
        {
            _logger?.LogWarning($"Exception during CTRLPlusCCancellationTokenSource cancel:\n{ex}");
        }
    }

    public void Dispose()
        => _cancellationTokenSource.Dispose();

    public void Cancel()
        => _cancellationTokenSource.Cancel();
}
