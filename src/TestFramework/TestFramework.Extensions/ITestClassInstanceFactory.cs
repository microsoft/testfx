// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Creates one MSTest test-class instance for a single test invocation.
/// </summary>
/// <remarks>A factory is invoked once for each test invocation, including each data row and retry attempt.</remarks>
internal interface ITestClassInstanceFactory
{
    /// <summary>
    /// Creates a test-class instance and returns the lease that owns its cleanup.
    /// </summary>
    /// <param name="testClassType">The concrete test-class type.</param>
    /// <param name="testContext">The test context for the current invocation.</param>
    /// <returns>A lease containing the created test-class instance.</returns>
    ITestClassInstanceLease CreateInstance(Type testClassType, TestContext testContext);
}

/// <summary>
/// Owns one test-class instance and all resources associated with its activation.
/// </summary>
internal interface ITestClassInstanceLease
{
    /// <summary>
    /// Gets the test-class instance.
    /// </summary>
    object Instance { get; }

    /// <summary>
    /// Releases the test-class instance and all resources associated with its activation.
    /// </summary>
    /// <returns>A task that represents the asynchronous cleanup operation.</returns>
    Task DisposeAsync();
}
