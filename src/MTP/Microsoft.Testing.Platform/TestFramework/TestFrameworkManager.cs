// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;

namespace Microsoft.Testing.Internal.Framework;

internal sealed class TestFrameworkManager(
    Func<ITestFrameworkCapabilities, IServiceProvider, ITestFramework> testFrameworkFactory,
    Func<IServiceProvider, ITestFrameworkCapabilities> testFrameworkCapabilitiesFactory)
    : ITestFrameworkManager
{
    public Func<ITestFrameworkCapabilities, IServiceProvider, ITestFramework> TestFrameworkFactory { get; } = testFrameworkFactory;

    public Func<IServiceProvider, ITestFrameworkCapabilities> TestFrameworkCapabilitiesFactory { get; } = testFrameworkCapabilitiesFactory;
}
