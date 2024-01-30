// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.OutputDevice;

internal sealed class PlatformOutputDeviceManager : IPlatformOutputDeviceManager
{
    private Func<IServiceProvider, IPlatformOutputDevice>? _platformOutputDeviceFactory;

    public void SetPlatformOutputDevice(Func<IServiceProvider, IPlatformOutputDevice> platformOutputDeviceFactory)
    {
        ArgumentGuard.IsNotNull(platformOutputDeviceFactory);
        _platformOutputDeviceFactory = platformOutputDeviceFactory;
    }

    internal async Task<IPlatformOutputDevice> BuildAsync(ServiceProvider serviceProvider, ApplicationLoggingState loggingState)
    {
        if (_platformOutputDeviceFactory is not null)
        {
            IPlatformOutputDevice platformOutputDevice = _platformOutputDeviceFactory(serviceProvider);
            if (await platformOutputDevice.IsEnabledAsync())
            {
                if (platformOutputDevice is IAsyncInitializableExtension platformOutputDeviceAsync)
                {
                    await platformOutputDeviceAsync.InitializeAsync();
                }

                return platformOutputDevice;
            }
            else
            {
                return CreateDefault();
            }
        }
        else
        {
            return CreateDefault();
        }

        IPlatformOutputDevice CreateDefault()
        {
            var defaultConsoleOutputDevice = new ConsoleOutputDevice(
               serviceProvider.GetTestApplicationCancellationTokenSource(),
               serviceProvider.GetConsole(),
               serviceProvider.GetTestApplicationModuleInfo(),
               serviceProvider.GetTestHostControllerInfo(),
               serviceProvider.GetAsyncMonitorFactory().Create(),
               serviceProvider.GetRuntimeFeature(),
               serviceProvider.GetEnvironment(),
               serviceProvider.GetProcessHandler(),
               loggingState.CommandLineParseResult.IsOptionSet(PlatformCommandLineProvider.VSTestAdapterModeOptionKey),
               loggingState.CommandLineParseResult.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey),
               loggingState.CommandLineParseResult.IsOptionSet(PlatformCommandLineProvider.ServerOptionKey),
               PlatformCommandLineProvider.GetMinimumExpectedTests(loggingState.CommandLineParseResult),
               loggingState.FileLoggerProvider);

            return defaultConsoleOutputDevice;
        }
    }
}
