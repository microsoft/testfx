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
    /// Adds  support to the test application builder.
    /// </summary>
    /// <param name="builder">The test application builder.</param>
    public static void AddAzureDevOpsProvider(this ITestApplicationBuilder builder)
    {
        var compositeTestSessionAzDoService =
           new CompositeExtensionFactory<AzureDevOpsReporter>(serviceProvider =>
               new AzureDevOpsReporter(
                   serviceProvider.GetCommandLineOptions(),
                   serviceProvider.GetEnvironment(),
                   serviceProvider.GetFileSystem(),
                   serviceProvider.GetOutputDevice(),
                   serviceProvider.GetLoggerFactory()));

        builder.TestHost.AddDataConsumer(compositeTestSessionAzDoService);

        builder.CommandLine.AddProvider(() => new AzureDevOpsCommandLineProvider());
    }
}
