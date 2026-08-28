// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Provides extension methods for adding GitHub Actions reporting support to the test application builder.
/// </summary>
public static class GitHubActionsExtensions
{
    // Must match Microsoft.Testing.Extensions.Retry's hidden child-host option. Referencing the retry assembly
    // directly would create a package dependency solely for this internal orchestration handshake.
    private const string RetryPipeOptionName = "internal-retry-pipename";

    /// <summary>
    /// Adds support to the test application builder.
    /// </summary>
    /// <param name="builder">The test application builder.</param>
    public static void AddGitHubActionsProvider(this ITestApplicationBuilder builder)
    {
        Lazy<GitHubActionsHistoryService>? historyService = null;
        object historyServiceLock = new();
        Func<IServiceProvider, GitHubActionsHistoryService> getHistoryService = serviceProvider =>
        {
            lock (historyServiceLock)
            {
                historyService ??= new Lazy<GitHubActionsHistoryService>(
                    () => CreateHistoryService(serviceProvider),
                    LazyThreadSafetyMode.ExecutionAndPublication);
                return historyService.Value;
            }
        };

        var compositeSummaryReporter = new CompositeExtensionFactory<GitHubActionsSummaryReporter>(serviceProvider =>
            new GitHubActionsSummaryReporter(
                serviceProvider.GetCommandLineOptions(),
                serviceProvider.GetConfiguration(),
                serviceProvider.GetEnvironment(),
                serviceProvider.GetFileSystem(),
                serviceProvider.GetMessageBus(),
                serviceProvider.GetOutputDevice(),
                serviceProvider.GetTestApplicationModuleInfo(),
                serviceProvider.GetTestApplicationProcessExitCode(),
                serviceProvider.GetRequiredService<ITestCoverageResult>(),
                serviceProvider.GetLoggerFactory(),
                () => ShouldDeferToArtifactPostProcessing(serviceProvider),
                getHistoryService(serviceProvider)));

        var compositeSlowTestReporter = new CompositeExtensionFactory<GitHubActionsSlowTestReporter>(serviceProvider =>
            new GitHubActionsSlowTestReporter(
                serviceProvider.GetCommandLineOptions(),
                serviceProvider.GetEnvironment(),
                serviceProvider.GetOutputDevice(),
                serviceProvider.GetTask(),
                serviceProvider.GetClock(),
                serviceProvider.GetLoggerFactory()));

        var compositeReporter = new CompositeExtensionFactory<GitHubActionsReporter>(serviceProvider =>
            new GitHubActionsReporter(
                serviceProvider.GetCommandLineOptions(),
                serviceProvider.GetEnvironment(),
                serviceProvider.GetOutputDevice(),
                serviceProvider.GetTestApplicationModuleInfo(),
                serviceProvider.GetLoggerFactory()));

        var compositeAnnotationReporter = new CompositeExtensionFactory<GitHubActionsAnnotationReporter>(serviceProvider =>
            new GitHubActionsAnnotationReporter(
                serviceProvider.GetCommandLineOptions(),
                serviceProvider.GetEnvironment(),
                serviceProvider.GetFileSystem(),
                serviceProvider.GetOutputDevice(),
                serviceProvider.GetTestApplicationProcessExitCode(),
                serviceProvider.GetLoggerFactory(),
                getHistoryService(serviceProvider)));

        builder.TestHost.AddTestSessionLifetimeHandler(serviceProvider =>
            getHistoryService(serviceProvider));
        builder.TestHost.AddDataConsumer(compositeAnnotationReporter);
        builder.TestHost.AddTestSessionLifetimeHandler(compositeAnnotationReporter);

        builder.TestHost.AddDataConsumer(compositeSummaryReporter);
        builder.TestHost.AddTestSessionLifetimeHandler(compositeSummaryReporter);
        builder.TestHost.AddDataConsumer(compositeSlowTestReporter);
        builder.TestHost.AddTestSessionLifetimeHandler(compositeSlowTestReporter);

        // Register the group reporter last, as both a data consumer (no-op) and a session-lifetime handler, so its
        // closing '::endgroup::' is ordered into the consumer phase after every other reporter's final output.
        builder.TestHost.AddDataConsumer(compositeReporter);
        builder.TestHost.AddTestSessionLifetimeHandler(compositeReporter);
        builder.CommandLine.AddProvider(() => new GitHubActionsCommandLineProvider());

        if (builder is IArtifactPostProcessingApplicationBuilder artifactPostProcessingBuilder)
        {
            artifactPostProcessingBuilder.ArtifactPostProcessing.AddArtifactPostProcessor(serviceProvider =>
                new GitHubActionsSummaryArtifactPostProcessor(
                    serviceProvider.GetCommandLineOptions(),
                    serviceProvider.GetEnvironment(),
                    serviceProvider.GetFileSystem(),
                    serviceProvider.GetLoggerFactory(),
                    () => serviceProvider.GetService<IPushOnlyProtocol>() is DotnetTestConnection
                    {
                        IsRequiredArtifactPostProcessingSupported: true,
                    },
                    CreateHistoryService(serviceProvider)));
        }
    }

    private static GitHubActionsHistoryService CreateHistoryService(IServiceProvider serviceProvider)
        => new(
           serviceProvider.GetCommandLineOptions(),
           serviceProvider.GetEnvironment(),
           serviceProvider.GetClock(),
           serviceProvider.GetLoggerFactory(),
           new GitHubActionsHistoryScope(
               serviceProvider.GetTestApplicationModuleInfo().TryGetAssemblyName() ?? "unknown assembly name",
               TargetFrameworkMonikerHelper.GetTargetFrameworkMonikerIncludingPlatform(),
               RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
               serviceProvider.GetEnvironment().GetEnvironmentVariable("RUNNER_OS") ?? string.Empty));

    private static bool ShouldDeferToArtifactPostProcessing(IServiceProvider serviceProvider)
    {
        bool dotnetTestRequiresPostProcessing =
            serviceProvider.GetService<IPushOnlyProtocol>() is DotnetTestConnection connection
            && connection.IsRequiredArtifactPostProcessingSupported;
        return dotnetTestRequiresPostProcessing
            || serviceProvider.GetCommandLineOptions().IsOptionSet(RetryPipeOptionName);
    }
}
