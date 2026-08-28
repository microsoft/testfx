// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.TrxReport.Resources;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHostControllers;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Provides extension methods for adding TRX report generation to a test application.
/// </summary>
public static class TrxReportExtensions
{
    /// <summary>
    /// Adds TRX report generation to a test application.
    /// </summary>
    /// <param name="builder">The test application builder.</param>
    public static void AddTrxReportProvider(this ITestApplicationBuilder builder)
    {
        if (builder is not IArtifactPostProcessingApplicationBuilder artifactPostProcessingBuilder)
        {
            throw new InvalidOperationException(ExtensionResources.InvalidTestApplicationBuilderType);
        }

        var commandLine = new TrxReportGeneratorCommandLine();

        var compositeTestSessionTrxService =
            new CompositeExtensionFactory<TrxReportGenerator>(serviceProvider =>
                new TrxReportGenerator(
                serviceProvider.GetConfiguration(),
                serviceProvider.GetCommandLineOptions(),
                serviceProvider.GetRequiredService<IFileSystem>(),
                serviceProvider.GetTestApplicationModuleInfo(),
                serviceProvider.GetMessageBus(),
                serviceProvider.GetSystemClock(),
                serviceProvider.GetEnvironment(),
                serviceProvider.GetOutputDevice(),
                serviceProvider.GetTestFramework(),
                serviceProvider.GetTestFrameworkCapabilities(),
                serviceProvider.GetTestApplicationProcessExitCode(),
                serviceProvider.GetTask(),
                serviceProvider.GetService<TrxTestApplicationLifecycleCallbacks>(),
                serviceProvider.GetLoggerFactory().CreateLogger<TrxReportGenerator>()));

        if (TrxModeHelpers.IsTestHostControllerSupported)
        {
            ControllerBackedRegistrations(builder);
        }

        builder.TestHost.AddDataConsumer(compositeTestSessionTrxService);
        builder.TestHost.AddTestSessionLifetimeHandler(compositeTestSessionTrxService);

        builder.CommandLine.AddProvider(() => commandLine);

        ToolTrxCompareFactory toolTrxCompareFactory = new();
        TrxCompareToolCommandLine createTrxCompareToolCommandLine = toolTrxCompareFactory.CreateTrxCompareToolCommandLine();
        builder.CommandLine.AddProvider(() => createTrxCompareToolCommandLine);

        artifactPostProcessingBuilder.Tools.AddTool(serviceProvider => toolTrxCompareFactory.CreateTrxCompareTool(
            serviceProvider.GetCommandLineOptions(),
            serviceProvider.GetOutputDevice(),
            serviceProvider.GetRequiredService<ITask>()));

        artifactPostProcessingBuilder.ArtifactPostProcessing.AddArtifactPostProcessor(_ => new TrxArtifactPostProcessor());

        ToolTrxMergeFactory toolTrxMergeFactory = new();
        builder.CommandLine.AddProvider(toolTrxMergeFactory.CreateCommandLine);
        artifactPostProcessingBuilder.Tools.AddTool(serviceProvider => toolTrxMergeFactory.CreateTool(serviceProvider.GetCommandLineOptions()));
    }

    [UnsupportedOSPlatform("android")]
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    [UnsupportedOSPlatform("wasi")]
    private static void ControllerBackedRegistrations(ITestApplicationBuilder builder)
    {
        builder.TestHost.AddTestHostApplicationLifetime(serviceProvider =>
            new TrxTestApplicationLifecycleCallbacks(
                serviceProvider.GetCommandLineOptions(),
                serviceProvider.GetEnvironment()));

        var endpoint = new NamedPipeServerEndpoint(NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N")).Name);
        var compositeLifeTimeHandler =
            new CompositeExtensionFactory<TrxProcessLifetimeHandler>(serviceProvider =>
            {
                ILoggerFactory loggerFactory = serviceProvider.GetLoggerFactory();
                loggerFactory.CreateLogger<TrxProcessLifetimeHandler>().LogTrace($"TRX pipe name: '{endpoint.PipeName}'");
                return new TrxProcessLifetimeHandler(
                    serviceProvider.GetCommandLineOptions(),
                    serviceProvider.GetEnvironment(),
                    loggerFactory,
                    serviceProvider.GetMessageBus(),
                    serviceProvider.GetRequiredService<IFileSystem>(),
                    serviceProvider.GetTestApplicationModuleInfo(),
                    serviceProvider.GetConfiguration(),
                    serviceProvider.GetSystemClock(),
                    serviceProvider.GetTask(),
                    serviceProvider.GetOutputDevice(),
                    serviceProvider,
                    endpoint);
            });
        ((TestHostControllersManager)builder.TestHostControllers).AddDataConsumer(compositeLifeTimeHandler);
        builder.TestHostControllers.AddProcessLifetimeHandler(compositeLifeTimeHandler);
        builder.TestHostControllers.AddEnvironmentVariableProvider(serviceProvider =>
        {
            serviceProvider.GetLoggerFactory().CreateLogger<TrxEnvironmentVariableProvider>().LogTrace($"TRX pipe name: '{endpoint.PipeName}'");
            return new TrxEnvironmentVariableProvider(serviceProvider.GetCommandLineOptions(), endpoint);
        });
    }
}
