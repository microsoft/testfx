// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.OutputDevice;

internal sealed class PlatformOutputDeviceManager : IPlatformOutputDeviceManager
{
#pragma warning disable CA1822 // Mark members as static
    internal IPlatformOutputDevice Build(ServiceProvider serviceProvider, ApplicationLoggingState loggingState)
#pragma warning restore CA1822 // Mark members as static
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
