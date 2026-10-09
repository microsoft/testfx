// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHostControllers;

namespace Microsoft.Testing.Extensions;

#pragma warning disable RS0051 // Shared implementation details are compiled into multiple extension assemblies.

internal static class CiCoverageSummaryControllerHandlerRegistration
{
    public static void Register(
        ITestHostControllersManager controllers,
        string provider,
        string fragmentKind,
        Func<IServiceProvider, ILogger> createLogger,
        Func<IServiceProvider, IArtifactPostProcessor> getProcessor,
        Func<IServiceProvider, bool> deferToDownstream,
        Func<string, Exception, string> formatWarning)
    {
        if (controllers is not TestHostControllersManager controllerManager)
        {
            return;
        }

        controllerManager.AddRunCompletionHandler(serviceProvider => new CiCoverageSummaryControllerHandler(
            provider,
            fragmentKind,
            serviceProvider.GetCommandLineOptions(),
            serviceProvider.GetConfiguration(),
            serviceProvider.GetRequiredService<ITestCoverageResult>(),
            serviceProvider.GetOutputDevice(),
            createLogger(serviceProvider),
            () => getProcessor(serviceProvider),
            () => deferToDownstream(serviceProvider),
            formatWarning));
    }
}

#pragma warning restore RS0051
