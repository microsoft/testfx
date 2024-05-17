// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FxExtensibilityTestProject;

[TestClass]
public class DynamicDataExMoreTests
{
    // Property ReusableTestDataProperty can be used as data source for test data with data driven test case.
    [TestMethod]
    [DynamicData("ReusableTestDataProperty", typeof(DynamicDataExTests))]
    public void DynamicDataTestMethod4(string a, int b, bool c)
    {
        Assert.AreEqual("string", a);
        Assert.AreEqual(0, b % 2);
        Assert.IsTrue(c);
    }

    // Method ReusableTestDataMethod can be used as data source for test data with data driven test case.
    [TestMethod]
    [DynamicData("ReusableTestDataMethod", typeof(DynamicDataExTests), DynamicDataSourceType.Method)]
    public void DynamicDataTestMethod5(string a, int b, bool c)
    {
        Assert.AreEqual("string", a);
        Assert.AreEqual(0, b % 2);
        Assert.IsTrue(c);
    }

    [TestMethod]
    [DynamicData("ReusableTestDataProperty", typeof(DynamicDataExTests))]
    [DynamicData("ReusableTestDataMethod", typeof(DynamicDataExTests), DynamicDataSourceType.Method)]
    public void DynamicDataTestMethod6(string a, int b, bool c)
    {
        Assert.AreEqual("string", a);
        Assert.AreEqual(0, b % 2);
        Assert.IsTrue(c);
    }

    [TestMethod]
    [DynamicData("EmptyTestDataProperty", typeof(DynamicDataExTests))]
    public void DynamicEmptyDataTestMethod4(string a, int b, bool c)
    {
        Assert.AreEqual("string", a);
        Assert.AreEqual(0, b % 2);
        Assert.IsTrue(c);
    }

    [TestMethod]
    [DynamicData("EmptyTestDataMethod", typeof(DynamicDataExTests), DynamicDataSourceType.Method)]
    public void DynamicEmptyDataTestMethod5(string a, int b, bool c)
    {
        Assert.AreEqual("string", a);
        Assert.AreEqual(0, b % 2);
        Assert.IsTrue(c);
    }

    [TestMethod]
    [DynamicData("EmptyTestDataProperty", typeof(DynamicDataExTests))]
    [DynamicData("EmptyTestDataMethod", typeof(DynamicDataExTests), DynamicDataSourceType.Method)]
    public void DynamicEmptyDataTestMethod6(string a, int b, bool c)
    {
        Assert.AreEqual("string", a);
        Assert.AreEqual(0, b % 2);
        Assert.IsTrue(c);
    }
}
