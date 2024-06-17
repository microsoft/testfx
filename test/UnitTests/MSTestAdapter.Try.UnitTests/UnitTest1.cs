// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTestAdapter.Try1.UnitTests;

[TestClass]
public class UnitTest1
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void TestMethod1()
     => Assert.AreEqual(1, 1);

    [TestMethod]
    [DataRow(1, 2, 3)]
    [DataRow(3, 4, 3)]
    public void TestMethod2(int a, int b, int c)
        => Assert.AreEqual(a, b);

    [TestMethod]
    [DynamicData(nameof(Get))]
    public void TestMethod3(MyObject obj)
    => Assert.AreEqual(10, obj.Id);

    public static IEnumerable<object[]> Get { get; } = [
            new[] { new MyObject { Id = 10 } }
        ];
}

public class MyObject
{
    public int Id { get; set; }
}
