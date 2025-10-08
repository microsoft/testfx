// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Framework.Adapter;
using Microsoft.Testing.Framework.Configurations;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Framework;

public static class TestApplicationBuilderExtensions
{
    public static void AddTestFramework(this ITestApplicationBuilder testApplicationBuilder, params ITestNodesBuilder[] testNodesBuilder)
        => testApplicationBuilder.AddTestFramework(new(), testNodesBuilder);

    public static void AddTestFramework(
        this ITestApplicationBuilder testApplicationBuilder,
        TestFrameworkConfiguration? testFrameworkConfiguration = null,
        params ITestNodesBuilder[] testNodesBuilder)
    {
        Guard.NotNull(testApplicationBuilder);
        Guard.NotNull(testNodesBuilder);
        ArgumentGuard.Ensure(testNodesBuilder.Length != 0, nameof(testNodesBuilder),
            "At least one test node builder must be provided.");

        testFrameworkConfiguration ??= new TestFrameworkConfiguration();
        TestingFrameworkExtension extension = new();
        testApplicationBuilder.AddTreeNodeFilterService(extension);
        testApplicationBuilder.RegisterTestFramework(
            serviceProvider => new TestFrameworkCapabilities(testNodesBuilder, new MSTestEngineBannerCapability(serviceProvider.GetRequiredService<IPlatformInformation>())),
            (capabilities, serviceProvider) =>
            new TestFramework(testFrameworkConfiguration, testNodesBuilder, extension, serviceProvider.GetSystemClock(),
                serviceProvider.GetTask(), serviceProvider.GetConfiguration(), capabilities));
    }
}
