// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Capabilities.TestFramework;

public interface ITestFrameworkCapabilities : ICapabilities<ITestFrameworkCapability>
{
}

public sealed class TestFrameworkCapabilities(params ITestFrameworkCapability[] capabilities) : ITestFrameworkCapabilities
{
    public ITestFrameworkCapability[] Capabilities => capabilities;
}
