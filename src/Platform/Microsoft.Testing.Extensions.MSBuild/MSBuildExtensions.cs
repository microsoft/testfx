// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.MSBuild;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.MSBuild;

/// <summary>
/// Extension methods for adding MSBuild support to the test application builder.
/// </summary>
public static class MSBuildExtensions
{
    /// <summary>
    /// Adds MSBuild support to the test application builder.
    /// </summary>
    /// <param name="builder">The test application builder.</param>
    public static void AddMSBuild(this ITestApplicationBuilder builder)
    {
        builder.CommandLine.AddProvider(() => new MSBuildCommandLineProvider());
        builder.TestHost.AddTestHostApplicationLifetime(
            serviceProvider => new MSBuildTestApplicationLifecycleCallbacks(
                serviceProvider.GetConfiguration(),
                serviceProvider.GetCommandLineOptions(),
                serviceProvider.GetTestApplicationCancellationTokenSource()));

        ((TestApplicationBuilder)builder).TestHostOrchestrator.AddTestHostOrchestratorApplicationLifetime(
            serviceProvider => new MSBuildOrchestratorLifetime(
                serviceProvider.GetConfiguration(),
                serviceProvider.GetCommandLineOptions(),
                serviceProvider.GetTestApplicationCancellationTokenSource()));

        CompositeExtensionFactory<MSBuildConsumer> compositeExtensionFactory
            = new(serviceProvider => new MSBuildConsumer(
                serviceProvider,
                serviceProvider.GetCommandLineOptions()));

        builder.TestHost.AddDataConsumer(compositeExtensionFactory);
        builder.TestHost.AddTestSessionLifetimeHandle(compositeExtensionFactory);
    }
}
