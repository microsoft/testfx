// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CtrfPlayground.Mtp;

[TestClass]
public class SampleTests
{
    [TestMethod]
    public void PassingTest()
        => Assert.AreEqual(2, 1 + 1);

    [TestMethod]
    public void FailingTest()
        => Assert.AreEqual(3, 1 + 1, "intentional failure to exercise CTRF status mapping");

    [TestMethod]
    [Ignore("intentionally skipped to exercise CTRF status mapping")]
    public void SkippedTest()
    {
    }

    [TestMethod]
    public void ThrowingTest()
        => throw new InvalidOperationException("intentional exception to exercise CTRF error fields");

    [DataTestMethod]
    [DataRow(1, 1, 2)]
    [DataRow(2, 3, 5)]
    [DataRow(2, 2, 5)] // intentional failure
    public void DataDrivenTest(int a, int b, int expected)
        => Assert.AreEqual(expected, a + b);
}
