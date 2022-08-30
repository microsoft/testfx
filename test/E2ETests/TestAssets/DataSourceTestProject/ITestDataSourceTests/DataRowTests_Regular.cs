// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DataSourceTestProject.ITestDataSourceTests;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;

[TestClass]
public class Regular_DataRowTests
{
    [TestMethod]
    [DataRow(10)]
    [DataRow(20)]
    [DataRow(30)]
    [DataRow(40)]
    public void DataRow1(int i) => Assert.IsTrue(i != 0);

    [TestMethod]
    [DataRow(10, "String parameter", true, false)]
    [DataRow(20, "String parameter", true, false)]
    [DataRow(30, "String parameter", true, false)]
    [DataRow(40, "String parameter", true, false)]
    public void DataRow2(int i, string s, bool b1, bool b2) => Assert.IsTrue(i != 200);

    [TestCategory("DataRowOptionalInvalidArguments")]
    [TestMethod]
    [ExpectedException(typeof(System.Reflection.TargetParameterCountException))]
    [DataRow()]
    [DataRow(2)]
    [DataRow(2, "DerivedRequiredArgument", "DerivedOptionalArgument", "DerivedExtraArgument")]
    public void DataRowTestMethodFailsWithInvalidArguments(int i1, string requiredString, string s1 = null) => Assert.Fail();


    [TestMethod]
    [DataRow(10.01d, 20.01d)]
    [DataRow(10.02d, 20.02d)]
    public void DataRowTestDouble(double value1, double value2)
    {
        Assert.IsTrue(value1 > 10d);
        Assert.IsTrue(value2 > 20d);
    }

    [TestMethod]
    [DataRow(1, (byte)10, 10, 10U, 10L, 10UL, 10F, 10D, "10")]
    [DataRow(2, (byte)10, 10, 10U, 10L, 10UL, 10F, 10D, "10")]
    [DataRow(3, (byte)10, 10, 10U, 10L, 10UL, 10F, 10D, "10")]
    [DataRow(4, (byte)10, 10, 10U, 10L, 10UL, 10F, 10D, "10")]
    public void DataRowTestMixed(int index, byte b, int i, uint u, long l, ulong ul, float f, double d, string s)
    {
        Assert.AreEqual<byte>(10, b);
        Assert.AreEqual(10, i);
        Assert.AreEqual(10U, u);
        Assert.AreEqual(10L, l);
        Assert.AreEqual(10UL, ul);
        Assert.AreEqual(10F, f);
        Assert.AreEqual(10D, d);
        Assert.AreEqual("10", s);
    }

    [TestMethod]
    [DataRow("john.doe@example.com", "abc123", null)]
    [DataRow("john.doe@example.com", "abc123", "/unit/test")]
    public void NullValueInData(string email, string password, string returnUrl)
    {

    }
}
