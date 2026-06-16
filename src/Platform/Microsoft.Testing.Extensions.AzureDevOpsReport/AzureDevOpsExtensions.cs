// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport;
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
                    serviceProvider.GetEnvironment(),
                    serviceProvider.GetFileSystem(),
                    serviceProvider.GetOutputDevice(),
                    serviceProvider.GetTestApplicationModuleInfo(),
                    serviceProvider.GetLoggerFactory()));

        var compositeSummaryReporter =
            new CompositeExtensionFactory<AzureDevOpsSummaryReporter>(serviceProvider =>
                new AzureDevOpsSummaryReporter(
                    serviceProvider.GetCommandLineOptions(),
                    serviceProvider.GetConfiguration(),
                    serviceProvider.GetEnvironment(),
                    serviceProvider.GetFileSystem(),
                    serviceProvider.GetOutputDevice(),
                    serviceProvider.GetTestApplicationModuleInfo(),
                    serviceProvider.GetLoggerFactory()));

        var compositeLogGroupReporter =
            new CompositeExtensionFactory<AzureDevOpsLogGroupReporter>(serviceProvider =>
                new AzureDevOpsLogGroupReporter(
                    serviceProvider.GetCommandLineOptions(),
                    serviceProvider.GetEnvironment(),
                    serviceProvider.GetOutputDevice(),
                    serviceProvider.GetTestApplicationModuleInfo(),
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
                serviceProvider.GetEnvironment(),
                serviceProvider.GetFileSystem(),
                serviceProvider.GetOutputDevice(),
                serviceProvider.GetLoggerFactory(),
                historyService);
        });
        builder.TestHost.AddDataConsumer(compositeArtifactUploader);
        builder.TestHost.AddDataConsumer(compositeSummaryReporter);
        builder.TestHost.AddDataConsumer(compositeTestResultsPublisher);
        builder.TestHost.AddDataConsumer(compositeLogGroupReporter);
        builder.TestHost.AddTestSessionLifetimeHandler(serviceProvider =>
            historyService ??= CreateHistoryService(serviceProvider));
        builder.TestHost.AddTestSessionLifetimeHandler(compositeArtifactUploader);
        builder.TestHost.AddTestSessionLifetimeHandler(compositeSummaryReporter);
        builder.TestHost.AddTestSessionLifetimeHandler(compositeTestResultsPublisher);

        // Registered last so its OnTestSessionFinishingAsync (the closing ##[endgroup]) runs after
        // the other AzDO handlers' finishing callbacks, ensuring the group wraps all their output.
        builder.TestHost.AddTestSessionLifetimeHandler(compositeLogGroupReporter);
        builder.CommandLine.AddProvider(() => new AzureDevOpsCommandLineProvider());
    }

    private static AzureDevOpsHistoryService CreateHistoryService(IServiceProvider serviceProvider)
        => new(
            serviceProvider.GetCommandLineOptions(),
            serviceProvider.GetEnvironment(),
            serviceProvider.GetClock(),
            new AzureDevOpsHistoryClient(serviceProvider.GetTask(), serviceProvider.GetClock()),
            serviceProvider.GetTask(),
            serviceProvider.GetLoggerFactory());
}
