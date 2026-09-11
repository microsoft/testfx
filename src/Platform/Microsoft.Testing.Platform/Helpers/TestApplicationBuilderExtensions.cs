// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Helpers;

/// <summary>
/// A collection of extension methods for <see cref="ITestApplicationBuilder"/>.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public static class TestApplicationBuilderExtensions
{
    /// <summary>
    /// Registers the command-line options provider for '--treenode-filter'.
    /// </summary>
    /// <param name="testApplicationBuilder">The test application builder.</param>
    /// <param name="extension">The extension owner of the tree node filter service.</param>
    public static void AddTreeNodeFilterService(this ITestApplicationBuilder testApplicationBuilder, IExtension extension)
        => testApplicationBuilder.CommandLine.AddProvider(() => new TreeNodeFilterCommandLineOptionsProvider(extension));

    /// <summary>
    /// Registers a provider that can contribute an additional test execution filter constraint.
    /// </summary>
    /// <param name="builder">The test application builder.</param>
    /// <param name="providerFactory">The factory method for creating the provider.</param>
    public static void AddTestExecutionFilterProvider(
        this ITestApplicationBuilder builder,
        Func<IServiceProvider, ITestExecutionFilterProvider> providerFactory)
        => ((TestHostManager)builder.TestHost).AddTestExecutionFilterProvider(providerFactory);

    /// <summary>
    /// Registers the command-line options provider for '--maximum-failed-tests'.
    /// </summary>
    /// <param name="builder">The test application builder.</param>
    /// <param name="extension">The extension owner of the maximum failed tests service.</param>
    /// <remarks>
    /// This API is experimental. It may change, break, or be removed at any time without notice.
    /// </remarks>
    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    public static void AddMaximumFailedTestsService(this ITestApplicationBuilder builder, IExtension extension)
        => builder.CommandLine.AddProvider(serviceProvider => new MaxFailedTestsCommandLineOptionsProvider(extension, serviceProvider));
}
