// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Logging;
#if !NET7_0_OR_GREATER
using Microsoft.Testing.Platform.Resources;
#endif
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.OutputDevice;

internal sealed class PlatformOutputDeviceManager
{
    private Func<IServiceProvider, IPlatformOutputDevice>? _platformOutputDeviceFactory;

    public void SetPlatformOutputDevice(Func<IServiceProvider, IPlatformOutputDevice> platformOutputDeviceFactory)
    {
        Guard.NotNull(platformOutputDeviceFactory);
        _platformOutputDeviceFactory = platformOutputDeviceFactory;
    }

    internal async Task<ProxyOutputDevice> BuildAsync(ServiceProvider serviceProvider, bool useServerModeOutputDevice)
    {
        // TODO: SetPlatformOutputDevice isn't public yet.
        // Before exposing it, do we want to pass the "useServerModeOutputDevice" info to it?
        IPlatformOutputDevice nonServerOutputDevice = _platformOutputDeviceFactory is null
            ? GetDefaultTerminalOutputDevice(serviceProvider)
            : _platformOutputDeviceFactory(serviceProvider);

        // If the externally provided output device is not enabled, we opt-in the default terminal output device.
        if (_platformOutputDeviceFactory is not null && !await nonServerOutputDevice.IsEnabledAsync().ConfigureAwait(false))
        {
            nonServerOutputDevice = GetDefaultTerminalOutputDevice(serviceProvider);
        }

        return new ProxyOutputDevice(
            nonServerOutputDevice,
            useServerModeOutputDevice
                ? new ServerModePerCallOutputDevice(
                    serviceProvider.GetService<FileLoggerProvider>(),
                    serviceProvider.GetRequiredService<IStopPoliciesService>())
                : null);
    }

    public static IPlatformOutputDevice GetDefaultTerminalOutputDevice(ServiceProvider serviceProvider)
    {
        if (OperatingSystem.IsBrowser())
        {
#if NET7_0_OR_GREATER
            return new BrowserOutputDevice(
                serviceProvider.GetConsole(),
                serviceProvider.GetTestApplicationModuleInfo(),
                serviceProvider.GetAsyncMonitorFactory().Create(),
                serviceProvider.GetRuntimeFeature(),
                serviceProvider.GetEnvironment(),
                serviceProvider.GetPlatformInformation(),
                serviceProvider.GetRequiredService<IStopPoliciesService>());
#else
            throw new PlatformNotSupportedException(PlatformResources.BrowserPlatformNotSupportedOnOlderFrameworks);
#endif
        }

        if (OperatingSystem.IsWasi())
        {
            return new WasiOutputDevice(
                serviceProvider.GetConsole(),
                serviceProvider.GetTestApplicationModuleInfo(),
                serviceProvider.GetAsyncMonitorFactory().Create(),
                serviceProvider.GetRuntimeFeature(),
                serviceProvider.GetEnvironment(),
                serviceProvider.GetPlatformInformation(),
                serviceProvider.GetRequiredService<IStopPoliciesService>());
        }

        // Default to terminal output device for other platforms.
        return new TerminalOutputDevice(
            serviceProvider.GetConsole(),
            serviceProvider.GetTestApplicationModuleInfo(),
            serviceProvider.GetTestHostControllerInfo(),
            serviceProvider.GetAsyncMonitorFactory().Create(),
            serviceProvider.GetRuntimeFeature(),
            serviceProvider.GetEnvironment(),
            serviceProvider.GetPlatformInformation(),
            serviceProvider.GetCommandLineOptions(),
            serviceProvider.GetFileLoggerInformation(),
            serviceProvider.GetLoggerFactory(),
            serviceProvider.GetClock(),
            serviceProvider.GetRequiredService<IStopPoliciesService>(),
            serviceProvider.GetTestApplicationCancellationTokenSource());
    }
}
