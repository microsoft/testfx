// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The test method attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class TestMethodAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestMethodAttribute"/> class.
    /// </summary>
    public TestMethodAttribute()
    : this(null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestMethodAttribute"/> class.
    /// </summary>
    /// <param name="displayName">
    /// Display name for the test.
    /// </param>
    public TestMethodAttribute(string? displayName) => DisplayName = displayName;

    /// <summary>
    /// Gets display name for the test.
    /// </summary>
    public string? DisplayName { get; }

    /// <summary>
    /// Executes a test method.
    /// </summary>
    /// <param name="testMethod">The test method to execute.</param>
    /// <returns>An array of TestResult objects that represent the outcome(s) of the test.</returns>
    /// <remarks>Extensions can override this method to customize running a TestMethod.</remarks>
    public virtual TestResult[] Execute(ITestMethod testMethod) => [testMethod.Invoke(null)];
}
