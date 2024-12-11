// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using System.Reflection;

using Microsoft.Testing.Extensions.VSTestBridge.Capabilities;
using Microsoft.Testing.Extensions.VSTestBridge.Helpers;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public static class TestApplicationBuilderExtensions
{
    public static void AddMSTest(this ITestApplicationBuilder testApplicationBuilder, Func<IEnumerable<Assembly>> getTestAssemblies)
    {
        MSTestExtension extension = new();
        testApplicationBuilder.AddRunSettingsService(extension);
        testApplicationBuilder.AddTestCaseFilterService(extension);
        testApplicationBuilder.AddTestRunParametersService(extension);
#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        testApplicationBuilder.AddMaximumFailedTestsService(extension);
#pragma warning restore TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        testApplicationBuilder.AddRunSettingsEnvironmentVariableProvider(extension);
        testApplicationBuilder.RegisterTestFramework(
            serviceProvider => new TestFrameworkCapabilities(
                new VSTestBridgeExtensionBaseCapabilities(),
#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                new MSTestBannerCapability(serviceProvider.GetRequiredService<IPlatformInformation>()),
                MSTestGracefulStopTestExecutionCapability.Instance),
#pragma warning restore TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            (capabilities, serviceProvider) => new MSTestBridgedTestFramework(extension, getTestAssemblies, serviceProvider, capabilities));
    }
}
#endif
