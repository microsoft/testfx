// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FxExtensibilityTestProject;

[TestClass]
public class DynamicDataExTests
{
#pragma warning disable IDE0051 // Remove unused private members
    private static IEnumerable<object[]> ReusableTestDataProperty => [["string", 2, true]];

    private static IEnumerable<object[]> EmptyTestDataProperty => [];

#pragma warning disable CA1859 // Use concrete types when possible for improved performance
#pragma warning disable IDE0051 // Remove unused private members
    private static IEnumerable<object[]> ReusableTestDataMethod() => [["string", 4, true]];

    private static IEnumerable<object[]> EmptyTestDataMethod() => [];

    // Property ReusableTestDataProperty can be used as data source for test data with data driven test case.
    [TestMethod]
    [DynamicData("ReusableTestDataProperty")]
    public void DynamicDataTestMethod1(string a, int b, bool c)
    {
        Assert.AreEqual("string", a);
        Assert.AreEqual(0, b % 2);
        Assert.IsTrue(c);
    }

    // Method ReusableTestDataMethod can be used as data source for test data with data driven test case.
    [TestMethod]
    [DynamicData("ReusableTestDataMethod", DynamicDataSourceType.Method)]
    public void DynamicDataTestMethod2(string a, int b, bool c)
    {
        Assert.AreEqual("string", a);
        Assert.AreEqual(0, b % 2);
        Assert.IsTrue(c);
    }

    [TestMethod]
    [DynamicData("ReusableTestDataProperty")]
    [DynamicData("ReusableTestDataMethod", DynamicDataSourceType.Method)]
    public void DynamicDataTestMethod3(string a, int b, bool c)
    {
        Assert.AreEqual("string", a);
        Assert.AreEqual(0, b % 2);
        Assert.IsTrue(c);
    }

    [TestMethod]
    [DynamicData("EmptyTestDataProperty")]
    public void DynamicEmptyDataTestMethod1(string a, int b, bool c)
    {
        Assert.AreEqual("string", a);
        Assert.AreEqual(0, b % 2);
        Assert.IsTrue(c);
    }

    [TestMethod]
    [DynamicData("EmptyTestDataMethod", DynamicDataSourceType.Method)]
    public void DynamicEmptyDataTestMethod2(string a, int b, bool c)
    {
        Assert.AreEqual("string", a);
        Assert.AreEqual(0, b % 2);
        Assert.IsTrue(c);
    }

    [TestMethod]
    [DynamicData("EmptyTestDataProperty")]
    [DynamicData("EmptyTestDataMethod", DynamicDataSourceType.Method)]
    public void DynamicEmptyDataTestMethod3(string a, int b, bool c)
    {
        Assert.AreEqual("string", a);
        Assert.AreEqual(0, b % 2);
        Assert.IsTrue(c);
    }
}
