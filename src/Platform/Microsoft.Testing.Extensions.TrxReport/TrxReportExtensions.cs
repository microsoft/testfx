// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TestReports.Resources;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHostControllers;

namespace Microsoft.Testing.Extensions;

public static class TrxReportExtensions
{
    public static void AddTrxReportProvider(this ITestApplicationBuilder builder)
    {
        if (builder is not TestApplicationBuilder testApplicationBuilder)
        {
            throw new InvalidOperationException(ExtensionResources.InvalidTestApplicationBuilderType);
        }

        PipeNameDescription pipeNameDescription = NamedPipeServer.GetPipeName($"trxpipename.{Guid.NewGuid():N}");
        var commandLine = new TrxReportGeneratorCommandLine();

        var compositeTestSessionTrxService =
            new CompositeExtensionFactory<TrxReportGenerator>(serviceProvider =>
                new TrxReportGenerator(
                serviceProvider.GetConfiguration(),
                serviceProvider.GetCommandLineOptions(),
                serviceProvider.GetTestApplicationModuleInfo(),
                serviceProvider.GetMessageBus(),
                serviceProvider.GetSystemClock(),
                serviceProvider.GetEnvironment(),
                serviceProvider.GetOutputDevice(),
                serviceProvider.GetTestFramework(),
                serviceProvider.GetTestFrameworkCapabilities(),
                commandLine,
                serviceProvider.GetService<TrxTestApplicationLifecycleCallbacks>(),
                serviceProvider.GetLoggerFactory().CreateLogger<TrxReportGenerator>()));

        builder.TestHost.AddTestApplicationLifecycleCallbacks(serviceProvider =>
        new TrxTestApplicationLifecycleCallbacks(
            serviceProvider.GetCommandLineOptions(),
            serviceProvider.GetEnvironment()));
        builder.TestHost.AddDataConsumer(compositeTestSessionTrxService);
        builder.TestHost.AddTestSessionLifetimeHandle(compositeTestSessionTrxService);

        builder.CommandLine.AddProvider(() => commandLine);

        var compositeLifeTimeHandler =
            new CompositeExtensionFactory<TrxProcessLifetimeHandler>(serviceProvider =>
                new TrxProcessLifetimeHandler(
                serviceProvider.GetCommandLineOptions(),
                serviceProvider.GetEnvironment(),
                serviceProvider.GetLoggerFactory(),
                serviceProvider.GetMessageBus(),
                serviceProvider.GetTestApplicationModuleInfo(),
                serviceProvider.GetConfiguration(),
                serviceProvider.GetSystemClock(),
                serviceProvider.GetTask(),
                pipeNameDescription));
        ((TestHostControllersManager)builder.TestHostControllers).AddDataConsumer(compositeLifeTimeHandler);
        builder.TestHostControllers.AddProcessLifetimeHandler(compositeLifeTimeHandler);
        builder.TestHostControllers.AddEnvironmentVariableProvider(serviceProvider => new TrxEnvironmentVariableProvider(serviceProvider.GetCommandLineOptions(), pipeNameDescription.Name));

        ToolTrxCompareFactory toolTrxCompareFactory = new();
        TrxCompareToolCommandLine createTrxCompareToolCommandLine = toolTrxCompareFactory.CreateTrxCompareToolCommandLine();
        builder.CommandLine.AddProvider(() => createTrxCompareToolCommandLine);

        testApplicationBuilder.Tools.AddTool((serviceProvider) => toolTrxCompareFactory.CreateTrxCompareTool(
            serviceProvider.GetCommandLineOptions(),
            serviceProvider.GetOutputDevice(),
            serviceProvider.GetRequiredService<ITask>()));
    }
}
