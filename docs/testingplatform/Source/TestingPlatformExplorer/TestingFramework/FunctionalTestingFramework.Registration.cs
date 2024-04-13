// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;

using TestingPlatformExplorer.UnitTests;

namespace TestingPlatformExplorer.FunctionalTestingFramework;

public static class TestingFrameworkExtensions
{
    public static void AddFunctionalTestingFramework(this ITestApplicationBuilder builder, Func<(TestOutCome, string)>[] actions)
    {
        builder.RegisterTestFramework(
            _ => new FunctionalTestingFrameworkCapabilities(),
            (capabilities, serviceProvider) => new FunctionalTestingFramework(capabilities, serviceProvider, actions));
    }
}
