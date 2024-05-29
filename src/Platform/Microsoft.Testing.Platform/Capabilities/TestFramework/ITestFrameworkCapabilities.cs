// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Capabilities.TestFramework;

/// <summary>
/// Represents the capabilities of a test framework.
/// </summary>
public interface ITestFrameworkCapabilities : ICapabilities<ITestFrameworkCapability>;

/// <summary>
/// Represents the capabilities of a test framework.
/// </summary>
public sealed class TestFrameworkCapabilities(IReadOnlyCollection<ITestFrameworkCapability> capabilities)
    : ITestFrameworkCapabilities
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestFrameworkCapabilities"/> class.
    /// </summary>
    /// <param name="capabilities">The test framework capabilities.</param>
    public TestFrameworkCapabilities(params ITestFrameworkCapability[] capabilities)
        : this((IReadOnlyCollection<ITestFrameworkCapability>)capabilities)
    {
    }

    /// <summary>
    /// Gets the test framework capabilities.
    /// </summary>
    public IReadOnlyCollection<ITestFrameworkCapability> Capabilities => capabilities;
}
