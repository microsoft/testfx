// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Provides extension methods for adding crash dump support to the test application builder.
/// </summary>
public static class CrashDumpExtensions
{
    /// <summary>
    /// Adds crash dump support to the test application builder.
    /// </summary>
    /// <param name="builder">The test application builder.</param>
    /// <param name="ignoreIfNotSupported">Determines if the extension should not be enabled on netfx.</param>
    public static void AddCrashDumpProvider(this ITestApplicationBuilder builder, bool ignoreIfNotSupported = false)
    {
        CrashDumpConfiguration crashDumpGeneratorConfiguration = new();

        if (ignoreIfNotSupported)
        {
#if !NETCOREAPP
            crashDumpGeneratorConfiguration.Enable = false;
#endif
        }

        builder.TestHostControllers.AddEnvironmentVariableProvider(serviceProvider
            => new CrashDumpEnvironmentVariableProvider(
                serviceProvider.GetConfiguration(),
                serviceProvider.GetCommandLineOptions(),
                serviceProvider.GetTestApplicationModuleInfo(),
                crashDumpGeneratorConfiguration,
                serviceProvider.GetLoggerFactory()));

        builder.TestHostControllers.AddProcessLifetimeHandler(serviceProvider
            => new CrashDumpProcessLifetimeHandler(
                serviceProvider.GetCommandLineOptions(),
                serviceProvider.GetMessageBus(),
                serviceProvider.GetOutputDevice(),
                crashDumpGeneratorConfiguration));

        builder.CommandLine.AddProvider(() => new CrashDumpCommandLineProvider());
    }
}
