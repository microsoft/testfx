// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Extensions.Hosting;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Extension methods for <see cref="ITestApplicationBuilder"/>.
/// </summary>
public static class HotReloadExtensions
{
    /// <summary>
    /// Adds a hot reload provider to the test application builder.
    /// </summary>
    /// <param name="builder">The test application builder.</param>
    public static void AddHotReloadProvider(this ITestApplicationBuilder builder)
        => ((TestHostManager)builder.TestHost).AddTestFrameworkInvoker(serviceProvider =>
            new HotReloadTestHostTestFrameworkInvoker(
                serviceProvider.GetEnvironment(),
                serviceProvider.GetRuntimeFeature(),
                serviceProvider));
}
