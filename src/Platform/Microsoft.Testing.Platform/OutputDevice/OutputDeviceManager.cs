// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

    public IPlatformOutputDevice Build(ServiceProvider serviceProvider)
    {
        if (_platformOutputDeviceFactory is not null)
        {
            IPlatformOutputDevice platformOutputDevice = _platformOutputDeviceFactory(serviceProvider);
            return platformOutputDevice;
        }

        return GetDefaultTerminalOutputDevice(serviceProvider);
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
            serviceProvider.GetClock());
}
