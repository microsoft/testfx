// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Extensions.AzureDevOpsReport.Helpers;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Provides extension methods for adding Azure DevOps reporting support to the test application builder.
/// </summary>
public static class AzureDevOpsExtensions
{
    /// <summary>
    /// Adds support to the test application builder.
    /// </summary>
    /// <param name="builder">The test application builder.</param>
    public static void AddAzureDevOpsProvider(this ITestApplicationBuilder builder)
    {
        AzureDevOpsHistoryService? historyService = null;

        var compositeArtifactUploader =
            new CompositeExtensionFactory<AzureDevOpsArtifactUploader>(serviceProvider =>
                new AzureDevOpsArtifactUploader(
                    serviceProvider.GetCommandLineOptions(),
                    serviceProvider.GetConfiguration(),
                    new SystemEnvironment(),
                    new SystemFileSystem(),
                    serviceProvider.GetOutputDevice(),
                    new SystemTestApplicationModuleInfo(),
                    serviceProvider.GetLoggerFactory()));

        var compositeTestResultsPublisher =
            new CompositeExtensionFactory<AzureDevOpsTestResultsPublisher>(serviceProvider =>
               new AzureDevOpsTestResultsPublisher(
                   serviceProvider.GetCommandLineOptions(),
                   serviceProvider.GetConfiguration(),
                   serviceProvider.GetEnvironment(),
                   serviceProvider.GetFileSystem(),
                   serviceProvider.GetTestApplicationModuleInfo(),
                   serviceProvider.GetTestApplicationProcessExitCode(),
                   new AzureDevOpsTestResultsClient(serviceProvider.GetTask(), serviceProvider.GetClock()),
                   serviceProvider.GetTask(),
                   serviceProvider.GetClock(),
                   serviceProvider.GetLoggerFactory()));

        builder.TestHost.AddDataConsumer(serviceProvider =>
        {
            historyService ??= CreateHistoryService(serviceProvider);

            return new AzureDevOpsReporter(
                serviceProvider.GetCommandLineOptions(),
                new SystemEnvironment(),
                new SystemFileSystem(),
                serviceProvider.GetOutputDevice(),
                serviceProvider.GetLoggerFactory(),
                historyService);
        });
        builder.TestHost.AddDataConsumer(compositeArtifactUploader);
        builder.TestHost.AddDataConsumer(compositeTestResultsPublisher);
        builder.TestHost.AddTestSessionLifetimeHandler(serviceProvider =>
            historyService ??= CreateHistoryService(serviceProvider));
        builder.TestHost.AddTestSessionLifetimeHandler(compositeArtifactUploader);
        builder.TestHost.AddTestSessionLifetimeHandler(compositeTestResultsPublisher);
        builder.CommandLine.AddProvider(() => new AzureDevOpsCommandLineProvider());
    }

    private static AzureDevOpsHistoryService CreateHistoryService(IServiceProvider serviceProvider)
    {
        var systemTask = new SystemTask();
        var systemClock = new SystemClock();
        return new(
            serviceProvider.GetCommandLineOptions(),
            new SystemEnvironment(),
            systemClock,
            new AzureDevOpsHistoryClient(systemTask, systemClock),
            systemTask,
            serviceProvider.GetLoggerFactory());
    }
}
