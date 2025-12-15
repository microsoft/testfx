// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Extensions.Hosting.Resources;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;

#if NETCOREAPP
using System.Reflection.Metadata;

using Microsoft.Testing.Extensions.Hosting;

[assembly: MetadataUpdateHandler(typeof(HotReloadHandler))]
#endif

namespace Microsoft.Testing.Extensions.Hosting;

internal sealed class HotReloadHandler
{
    private static readonly SemaphoreSlim SemaphoreSlim = new(1, 1);
    private static bool s_shutdownProcess;
    private readonly IConsole _console;
    private readonly IOutputDevice _outputDevice;
    private readonly IOutputDeviceDataProducer _outputDeviceDataProducer;

    public HotReloadHandler(IConsole console, IOutputDevice outputDevice, IOutputDeviceDataProducer outputDeviceDataProducer)
    {
        _console = console;
        _outputDevice = outputDevice;
        _outputDeviceDataProducer = outputDeviceDataProducer;

        if (!IsCancelKeyPressNotSupported())
        {
            _console.CancelKeyPress += (_, _) =>
            {
                if (!s_shutdownProcess)
                {
                    s_shutdownProcess = true;
                    SemaphoreSlim.Release();
                }
            };
        }
    }

    [SupportedOSPlatformGuard("android")]
    [SupportedOSPlatformGuard("ios")]
    [SupportedOSPlatformGuard("tvos")]
    [SupportedOSPlatformGuard("browser")]
    private static bool IsCancelKeyPressNotSupported()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Create("ANDROID")) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Create("IOS")) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Create("TVOS")) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Create("WASI")) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Create("BROWSER"));

    // Called automatically by the runtime through the MetadataUpdateHandlerAttribute
    public static void ClearCache(Type[]? _)
    {
    }

    // Called automatically by the runtime through the MetadataUpdateHandlerAttribute
    public static void UpdateApplication(Type[]? _) => SemaphoreSlim.Release();

#if !NET6_0_OR_GREATER
    public Task<bool> ShouldRunAsync(Task? waitExecutionCompletion, CancellationToken cancellationToken)
    {
        // Avoid warnings about unused parameters and fields.
        _ = _outputDevice;
        _ = _outputDeviceDataProducer;
        _ = waitExecutionCompletion;
        _ = cancellationToken;
        throw new NotSupportedException(ExtensionResources.HotReloadHandlerUnsupportedFrameworkErrorMessage);
    }
#else
    public async Task<bool> ShouldRunAsync(Task? waitExecutionCompletion, CancellationToken cancellationToken)
    {
        if (s_shutdownProcess)
        {
            return false;
        }

        cancellationToken.Register(() => s_shutdownProcess = true);

        if (waitExecutionCompletion is not null)
        {
            await waitExecutionCompletion.ConfigureAwait(false);
            await _outputDevice!.DisplayAsync(_outputDeviceDataProducer, new TextOutputDeviceData(ExtensionResources.HotReloadSessionCompleted), cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await SemaphoreSlim.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // We're closing
        }

        if (!IsClearNotSupported())
        {
            _console!.Clear();
        }

        await _outputDevice.DisplayAsync(_outputDeviceDataProducer, new TextOutputDeviceData(ExtensionResources.HotReloadSessionStarted), cancellationToken).ConfigureAwait(false);

        return !s_shutdownProcess;
    }

    [SupportedOSPlatformGuard("android")]
    [SupportedOSPlatformGuard("ios")]
    [SupportedOSPlatformGuard("tvos")]
    private static bool IsClearNotSupported()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Create("ANDROID")) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Create("IOS")) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Create("TVOS"));
#endif
}
