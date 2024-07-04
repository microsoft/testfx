// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.MSBuild.TestPlatformExtensions;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.MSBuild;

public static class MSBuildExtensions
{
    public static void AddMSBuild(this ITestApplicationBuilder builder)
    {
        builder.CommandLine.AddProvider(() => new MSBuildCommandLineProvider());
        builder.TestHost.AddTestApplicationLifecycleCallbacks(
            serviceProvider => new MSBuildTestApplicationLifecycleCallbacks(
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
