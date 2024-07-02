// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The test class attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class STATestClassAttribute : TestClassAttribute
{
    /// <summary>
    /// Gets a test method attribute that enables running this test.
    /// </summary>
    /// <param name="testMethodAttribute">The test method attribute instance defined on this method.</param>
    /// <returns>The <see cref="TestMethodAttribute"/> to be used to run this test.</returns>
    public override TestMethodAttribute? GetTestMethodAttribute(TestMethodAttribute? testMethodAttribute) => testMethodAttribute;
}
