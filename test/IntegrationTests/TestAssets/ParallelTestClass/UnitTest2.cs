// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ParallelClassesTestProject;

namespace ParallelClassesTestProject2;

[TestClass]
public class UnitTest2
{
    private static bool s_assemblyInitCalled;
    private static bool s_assemblyCleanCalled;
    private static bool s_classInitCalled;
    private static bool s_classCleanCalled;

    [AssemblyInitialize]
    public static void AssemblyInit(TestContext context)
    {
        Assert.IsFalse(s_assemblyInitCalled);
        s_assemblyInitCalled = true;
    }

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        Assert.IsFalse(s_classInitCalled);
        s_classInitCalled = true;
    }

    [TestInitialize]
    public void Initialize()
    {
    }

    [TestCleanup]
    public void Cleanup()
    {
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        Assert.IsFalse(s_classCleanCalled);
        s_classCleanCalled = true;
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
        Assert.IsFalse(s_assemblyCleanCalled);
        s_assemblyCleanCalled = true;
    }

    [TestMethod]
    public void SimpleTest21()
    {
        Thread.Sleep(Constants.WaitTimeInMS);
        Assert.AreEqual(0, 0);
    }

    [TestMethod]
    public void SimpleTest22()
    {
        Thread.Sleep(Constants.WaitTimeInMS);
        Assert.Fail();
    }

    [TestMethod]
    [DoNotParallelize]
    public void IsolatedTest()
    {
        Thread.Sleep(Constants.WaitTimeInMS);
        Assert.IsTrue(true);
    }
}
