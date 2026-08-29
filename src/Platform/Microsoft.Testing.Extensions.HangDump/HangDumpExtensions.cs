// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHostControllers;

#if !NETCOREAPP
using Polyfills;
#endif

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Provides extension methods for adding hang dump support to the test application.
/// </summary>
public static class HangDumpExtensions
{
    /// <summary>
    /// Adds hang dump support to the test application.
    /// </summary>
    /// <param name="builder">The test application builder.</param>
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    [UnsupportedOSPlatform("wasi")]
    public static void AddHangDumpProvider(this ITestApplicationBuilder builder)
    {
        if (OperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException("Hang dump extension is not available on browser");
        }

        if (OperatingSystem.IsIOS() || OperatingSystem.IsTvOS())
        {
            throw new PlatformNotSupportedException("Hang dump extension is not available on ios nor tvos");
        }

        if (OperatingSystem.IsWasi())
        {
            throw new PlatformNotSupportedException("Hang dump extension is not available on wasi");
        }

        var endpoint = new NamedPipeServerEndpoint(NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N")).Name);

        builder.TestHostControllers.AddProcessLifetimeHandler(serviceProvider
            => new HangDumpProcessLifetimeHandler(
                endpoint,
                serviceProvider.GetMessageBus(),
                serviceProvider.GetOutputDevice(),
                serviceProvider.GetCommandLineOptions(),
                serviceProvider.GetTask(),
                serviceProvider.GetEnvironment(),
                serviceProvider.GetLoggerFactory(),
                serviceProvider.GetConfiguration(),
                serviceProvider.GetProcessHandler(),
                serviceProvider.GetClock(),
                serviceProvider));

        builder.TestHostControllers.AddEnvironmentVariableProvider(serviceProvider
            => new HangDumpEnvironmentVariableProvider(serviceProvider.GetCommandLineOptions(), endpoint));

        builder.CommandLine.AddProvider(()
            => new HangDumpCommandLineProvider());

        var hangDumpActivityIndicatorComposite
            = new CompositeExtensionFactory<HangDumpActivityIndicator>(serviceProvider => new HangDumpActivityIndicator(
                serviceProvider.GetCommandLineOptions(),
                serviceProvider.GetEnvironment(),
                serviceProvider.GetTask(),
                serviceProvider.GetLoggerFactory(),
                serviceProvider.GetClock()));

        builder.TestHost.AddDataConsumer(hangDumpActivityIndicatorComposite);
        builder.TestHost.AddTestSessionLifetimeHandler(hangDumpActivityIndicatorComposite);
    }
}
