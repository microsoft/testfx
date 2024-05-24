// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ParallelClassesTestProject;

namespace ParallelClassesTestProject1;

[TestClass]
public class UnitTest3
{
    [TestMethod]
    public void SimpleTest31()
    {
        Thread.Sleep(Constants.WaitTimeInMS);
        Assert.AreEqual(1, 1);
    }

    [TestMethod]
    public void SimpleTest32()
    {
        Thread.Sleep(Constants.WaitTimeInMS);
        Assert.Fail();
    }
}
