// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Platform.Builder;

namespace TestingPlatformExplorer.TestingFramework;

public static class TestingFrameworkExtensions
{
    public static void AddTestingFramework(this ITestApplicationBuilder builder, Assembly[] assemblies)
    {
        builder.RegisterTestFramework(
            _ => new TestingFrameworkCapabilities(),
            (capabilities, serviceProvider) => new TestingFramework(capabilities, serviceProvider, assemblies));
    }
}
