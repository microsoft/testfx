// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;

namespace Microsoft.Testing.Framework;

internal class TestFrameworkManager : ITestFrameworkManager
{
    public Func<ITestFrameworkCapabilities, IServiceProvider, ITestFramework> TestFrameworkAdapterFactory { get; }

    public Func<IServiceProvider, ITestFrameworkCapabilities> TestFrameworkCapabilitiesFactory { get; }

    public TestFrameworkManager(
        Func<ITestFrameworkCapabilities, IServiceProvider, ITestFramework> testFrameworkAdapterFactory,
        Func<IServiceProvider, ITestFrameworkCapabilities> testFrameworkCapabilitiesFactory)
    {
        TestFrameworkAdapterFactory = testFrameworkAdapterFactory;
        TestFrameworkCapabilitiesFactory = testFrameworkCapabilitiesFactory;
    }
}

internal interface ITestFrameworkManager
{
    Func<ITestFrameworkCapabilities, IServiceProvider, ITestFramework> TestFrameworkAdapterFactory { get; }

    Func<IServiceProvider, ITestFrameworkCapabilities> TestFrameworkCapabilitiesFactory { get; }
}
