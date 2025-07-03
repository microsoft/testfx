// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Extensions.Policy;
using Microsoft.Testing.Extensions.Policy.Resources;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Extension methods for adding the retry provider to the test application.
/// </summary>
public static class RetryExtensions
{
    /// <summary>
    /// Adds the retry provider to the test application.
    /// </summary>
    /// <param name="builder">The test application builder.</param>
    public static void AddRetryProvider(this ITestApplicationBuilder builder)
    {
        builder.CommandLine.AddProvider(() => new RetryCommandLineOptionsProvider());

        builder.TestHost.AddTestHostApplicationLifetime(serviceProvider
            => new RetryLifecycleCallbacks(serviceProvider));

        CompositeExtensionFactory<RetryDataConsumer> compositeExtensionFactory
            = new(serviceProvider => new RetryDataConsumer(serviceProvider));
        builder.TestHost.AddDataConsumer(compositeExtensionFactory);
        builder.TestHost.AddTestSessionLifetimeHandle(compositeExtensionFactory);

        if (builder is not TestApplicationBuilder testApplicationBuilder)
        {
            throw new InvalidOperationException(ExtensionResources.RetryFailedTestsInvalidTestApplicationBuilderErrorMessage);
        }

        // Net yet exposed extension points
        ((TestHostOrchestratorManager)testApplicationBuilder.TestHostOrchestrator)
            .AddTestHostOrchestrator(serviceProvider => new RetryOrchestrator(serviceProvider));
        ((TestHostManager)builder.TestHost)
            .AddTestExecutionFilterFactory(serviceProvider => new RetryExecutionFilterFactory(serviceProvider));
    }
}
