// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ParallelClassesTestProject;

namespace ParallelClassesTestProject1;

[TestClass]
public class UnitTest1
{
    private static bool s_classInitCalled;
    private static bool s_classCleanCalled;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        Assert.IsFalse(s_classInitCalled);
        s_classInitCalled = true;
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        Assert.IsFalse(s_classCleanCalled);
        s_classCleanCalled = true;
    }

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
}
