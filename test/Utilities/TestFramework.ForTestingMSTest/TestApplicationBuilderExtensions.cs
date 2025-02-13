// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Services;

namespace TestFramework.ForTestingMSTest;

public static class TestApplicationBuilderExtensions
{
    public static void AddInternalTestFramework(this ITestApplicationBuilder testApplicationBuilder)
    {
        TestFrameworkExtension extension = new();
        testApplicationBuilder.RegisterTestFramework(
            _ => new TestFrameworkCapabilities(),
            (capabilities, serviceProvider) => new TestFramework(extension, serviceProvider.GetLoggerFactory()));
    }
}
