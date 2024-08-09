// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Logging;
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

    internal async Task<IPlatformOutputDevice> BuildAsync(ServiceProvider serviceProvider, ApplicationLoggingState loggingState)
    {
        if (_platformOutputDeviceFactory is not null)
        {
            IPlatformOutputDevice platformOutputDevice = _platformOutputDeviceFactory(serviceProvider);
            if (await platformOutputDevice.IsEnabledAsync())
            {
                await platformOutputDevice.TryInitializeAsync();

                return platformOutputDevice;
            }
        }

        return new ConsoleOutputDevice(
            serviceProvider.GetTestApplicationCancellationTokenSource(),
            serviceProvider.GetConsole(),
            serviceProvider.GetTestApplicationModuleInfo(),
            serviceProvider.GetTestHostControllerInfo(),
            serviceProvider.GetAsyncMonitorFactory().Create(),
            serviceProvider.GetRuntimeFeature(),
            serviceProvider.GetEnvironment(),
            serviceProvider.GetProcessHandler(),
            serviceProvider.GetPlatformInformation(),
            loggingState.CommandLineParseResult.IsOptionSet(PlatformCommandLineProvider.VSTestAdapterModeOptionKey),
            loggingState.CommandLineParseResult.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey),
            loggingState.CommandLineParseResult.IsOptionSet(PlatformCommandLineProvider.ServerOptionKey),
            PlatformCommandLineProvider.GetMinimumExpectedTests(loggingState.CommandLineParseResult),
            loggingState.FileLoggerProvider,
            serviceProvider.GetClock());
    }
}
