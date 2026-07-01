// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Provides extension methods for adding GitHub Actions reporting support to the test application builder.
/// </summary>
public static class GitHubActionsExtensions
{
    /// <summary>
    /// Adds support to the test application builder.
    /// </summary>
    /// <param name="builder">The test application builder.</param>
    public static void AddGitHubActionsProvider(this ITestApplicationBuilder builder)
    {
        var compositeSummaryReporter = new CompositeExtensionFactory<GitHubActionsSummaryReporter>(serviceProvider =>
            new GitHubActionsSummaryReporter(
                serviceProvider.GetCommandLineOptions(),
                serviceProvider.GetEnvironment(),
                serviceProvider.GetFileSystem(),
                serviceProvider.GetOutputDevice(),
                serviceProvider.GetTestApplicationModuleInfo(),
                serviceProvider.GetLoggerFactory()));

        var compositeSlowTestReporter = new CompositeExtensionFactory<GitHubActionsSlowTestReporter>(serviceProvider =>
            new GitHubActionsSlowTestReporter(
                serviceProvider.GetCommandLineOptions(),
                serviceProvider.GetEnvironment(),
                serviceProvider.GetOutputDevice(),
                serviceProvider.GetTask(),
                serviceProvider.GetClock(),
                serviceProvider.GetLoggerFactory()));

        builder.TestHost.AddDataConsumer(serviceProvider =>
            new GitHubActionsAnnotationReporter(
                serviceProvider.GetCommandLineOptions(),
                serviceProvider.GetEnvironment(),
                serviceProvider.GetFileSystem(),
                serviceProvider.GetOutputDevice(),
                serviceProvider.GetLoggerFactory()));

        builder.TestHost.AddDataConsumer(compositeSummaryReporter);
        builder.TestHost.AddTestSessionLifetimeHandler(compositeSummaryReporter);
        builder.TestHost.AddDataConsumer(compositeSlowTestReporter);
        builder.TestHost.AddTestSessionLifetimeHandler(compositeSlowTestReporter);
        builder.TestHost.AddTestSessionLifetimeHandler(serviceProvider =>
            new GitHubActionsReporter(
                serviceProvider.GetCommandLineOptions(),
                serviceProvider.GetEnvironment(),
                serviceProvider.GetOutputDevice(),
                serviceProvider.GetTestApplicationModuleInfo(),
                serviceProvider.GetLoggerFactory()));
        builder.CommandLine.AddProvider(() => new GitHubActionsCommandLineProvider());
    }
}
