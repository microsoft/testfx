// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DoNotParallelizeTestProject;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void SimpleTest11()
    {
        Thread.Sleep(Constants.WaitTimeInMS);
        Assert.AreEqual(1, 1);
    }

    [TestMethod]
    public void SimpleTest12()
    {
        Thread.Sleep(Constants.WaitTimeInMS);
        Assert.Fail();
    }

    [TestMethod]
    public void SimpleTest13()
    {
        Thread.Sleep(Constants.WaitTimeInMS);
        Assert.AreEqual(1, 1);
    }
}
