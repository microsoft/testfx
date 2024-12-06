// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.OutputDevice;

internal sealed class PlatformOutputDeviceManager : IPlatformOutputDeviceManager
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
        if (_platformOutputDeviceFactory is not null && !await nonServerOutputDevice.IsEnabledAsync())
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

    public static TerminalOutputDevice GetDefaultTerminalOutputDevice(ServiceProvider serviceProvider)
        => new(
            serviceProvider.GetTestApplicationCancellationTokenSource(),
            serviceProvider.GetConsole(),
            serviceProvider.GetTestApplicationModuleInfo(),
            serviceProvider.GetTestHostControllerInfo(),
            serviceProvider.GetAsyncMonitorFactory().Create(),
            serviceProvider.GetRuntimeFeature(),
            serviceProvider.GetEnvironment(),
            serviceProvider.GetProcessHandler(),
            serviceProvider.GetPlatformInformation(),
            serviceProvider.GetCommandLineOptions(),
            serviceProvider.GetFileLoggerInformation(),
            serviceProvider.GetLoggerFactory(),
            serviceProvider.GetClock(),
            serviceProvider.GetRequiredService<IStopPoliciesService>());
}
