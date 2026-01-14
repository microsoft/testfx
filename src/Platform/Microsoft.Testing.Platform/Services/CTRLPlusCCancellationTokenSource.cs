// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Platform.Services;

internal sealed class CTRLPlusCCancellationTokenSource : ITestApplicationCancellationTokenSource, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly ILogger? _logger;

    public CTRLPlusCCancellationTokenSource(IConsole? console = null, ILogger? logger = null)
    {
        if (console is not null && !IsCancelKeyPressNotSupported())
        {
            console.CancelKeyPress += OnConsoleCancelKeyPressed;
        }

        _logger = logger;
    }

    [SupportedOSPlatformGuard("android")]
    [SupportedOSPlatformGuard("ios")]
    [SupportedOSPlatformGuard("tvos")]
    [SupportedOSPlatformGuard("wasi")]
    [SupportedOSPlatformGuard("browser")]
    private static bool IsCancelKeyPressNotSupported()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Create("ANDROID")) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Create("IOS")) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Create("TVOS")) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Create("WASI")) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Create("BROWSER"));

    public void CancelAfter(TimeSpan timeout) => _cancellationTokenSource.CancelAfter(timeout);

    public CancellationToken CancellationToken
        => _cancellationTokenSource.Token;

    private void OnConsoleCancelKeyPressed(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
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
