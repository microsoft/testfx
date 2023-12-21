// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: CLSCompliant(true)]

namespace DataRowTestProject;

[TestClass]
public class ClsTests
{
    [TestMethod]
    public void TestMethod()
    {
        Assert.IsTrue(true);
    }

    [TestMethod]
    [DataRow(10)]
    public void IntDataRow(int i)
    {
        Assert.IsTrue(i != 0);
    }

    [TestMethod]
    [DataRow("some string")]
    public void StringDataRow(string s)
    {
        Assert.IsNotNull(s);
    }

    [TestMethod]
    [DataRow("some string")]
    [DataRow("some other string")]
    public void StringDataRow2(string s)
    {
        Assert.IsNotNull(s);
    }
}
