// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace TestingPlatformExplorer.TestingFramework;

internal sealed class TestingFrameworkCapabilities : ITestFrameworkCapabilities
{
    public IReadOnlyCollection<ITestFrameworkCapability> Capabilities => [];
}
