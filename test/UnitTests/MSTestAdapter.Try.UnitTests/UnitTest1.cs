// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTestAdapter.Try.UnitTests;

[TestClass]
public class UnitTest1
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void TestMethod1()
    {
    }

    [TestMethod]
    [DataRow(1, 2, 3)]
    public void TestMethod2(int a, int b, int c)
    {
    }
}
