// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Capabilities.TestFramework;

public interface ITestFrameworkCapabilities : ICapabilities<ITestFrameworkCapability>
{
}

public sealed class TestFrameworkCapabilities(IReadOnlyCollection<ITestFrameworkCapability> capabilities) : ITestFrameworkCapabilities
{
    public TestFrameworkCapabilities(params ITestFrameworkCapability[] capabilities)
        : this((IReadOnlyCollection<ITestFrameworkCapability>)capabilities)
    {
    }

    public IReadOnlyCollection<ITestFrameworkCapability> Capabilities => capabilities;
}
