// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TestFramework;
using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace Microsoft.Testing.Framework;

internal class TestFrameworkManager(
    Func<ITestFrameworkCapabilities, IServiceProvider, ITestFramework> testFrameworkAdapterFactory,
    Func<IServiceProvider, ITestFrameworkCapabilities> testFrameworkCapabilitiesFactory)
    : ITestFrameworkManager
{
    public Func<ITestFrameworkCapabilities, IServiceProvider, ITestFramework> TestFrameworkAdapterFactory { get; } = testFrameworkAdapterFactory;

    public Func<IServiceProvider, ITestFrameworkCapabilities> TestFrameworkCapabilitiesFactory { get; } = testFrameworkCapabilitiesFactory;
}
