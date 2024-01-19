// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using System.Reflection;

using Microsoft.Testing.Extensions.VSTestBridge.Capabilities;
using Microsoft.Testing.Extensions.VSTestBridge.Helpers;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public static class TestApplicationBuilderExtensions
{
    public static void AddMSTest(this ITestApplicationBuilder testApplicationBuilder, Func<IEnumerable<Assembly>> getTestAssemblies)
    {
        MSTestExtension extension = new();
        testApplicationBuilder.AddRunSettingsService(extension);
        testApplicationBuilder.AddTestCaseFilterService(extension);
        testApplicationBuilder.RegisterTestFramework(
            _ => new TestFrameworkCapabilities(new VSTestBridgeExtensionBaseCapabilities()),
            (capabilities, serviceProvider) => new MSTestBridgedTestFramework(extension, getTestAssemblies, serviceProvider, capabilities));
    }
}
#endif
