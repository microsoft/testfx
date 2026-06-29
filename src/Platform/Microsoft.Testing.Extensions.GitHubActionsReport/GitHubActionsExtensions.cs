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
        var compositeReporter = new CompositeExtensionFactory<GitHubActionsReporter>(serviceProvider =>
            new GitHubActionsReporter(
                serviceProvider.GetCommandLineOptions(),
                serviceProvider.GetEnvironment(),
                serviceProvider.GetOutputDevice(),
                serviceProvider.GetTestApplicationModuleInfo(),
                serviceProvider.GetLoggerFactory()));

        builder.TestHost.AddDataConsumer(serviceProvider =>
            new GitHubActionsAnnotationReporter(
                serviceProvider.GetCommandLineOptions(),
                serviceProvider.GetEnvironment(),
                serviceProvider.GetFileSystem(),
                serviceProvider.GetOutputDevice(),
                serviceProvider.GetLoggerFactory()));

        builder.TestHost.AddTestSessionLifetimeHandler(compositeReporter);
        builder.CommandLine.AddProvider(() => new GitHubActionsCommandLineProvider());
    }
}
