﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FxExtensibilityTestProject;
[TestClass]
public class DynamicDataExTests
{
    private static IEnumerable<object[]> ReusableTestDataProperty
    {
        get
        {
            return new[] { new object[] { "string", 2, true } };
        }
    }

    private static IEnumerable<object[]> ReusableTestDataMethod()
    {
        return new[] { new object[] { "string", 4, true } };
    }

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
}
