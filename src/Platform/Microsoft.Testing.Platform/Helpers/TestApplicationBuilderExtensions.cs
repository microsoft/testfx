// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;

namespace Microsoft.Testing.Platform.Helpers;

/// <summary>
/// A collection of extension methods for <see cref="ITestApplicationBuilder"/>.
/// </summary>
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
    /// Registers the command-line options provider for '--maximum-failed-tests'.
    /// </summary>
    /// <param name="builder">The test application builder.</param>
    /// <param name="extension">The extension owner of the maximum failed tests service.</param>
    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    public static void AddMaximumFailedTestsService(this ITestApplicationBuilder builder, IExtension extension)
        => builder.CommandLine.AddProvider(serviceProvider => new MaxFailedTestsCommandLineOptionsProvider(extension, serviceProvider));
}
