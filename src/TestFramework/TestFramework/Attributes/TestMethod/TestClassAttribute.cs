// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// This attribute is used to mark test classes.
/// </summary>
/// <remarks>
/// Test classes must be:
/// <list type="bullet">
/// <item><description>public, or if <see cref="DiscoverInternalsAttribute"/> is used then it can be internal.</description></item>
/// <item><description>not static</description></item>
/// <item><description>not generic</description></item>
/// </list>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class TestClassAttribute : Attribute
{
    /// <summary>
    /// Gets a test method attribute that enables running this test.
    /// </summary>
    /// <param name="testMethodAttribute">The test method attribute instance defined on this method.</param>
    /// <returns>The <see cref="TestMethodAttribute"/> to be used to run this test.</returns>
    /// <remarks>Extensions can override this method to customize how all methods in a class are run.</remarks>
    public virtual TestMethodAttribute? GetTestMethodAttribute(TestMethodAttribute? testMethodAttribute) =>
        // If TestMethod is not extended by derived class then return back the original TestMethodAttribute
        testMethodAttribute;
}
