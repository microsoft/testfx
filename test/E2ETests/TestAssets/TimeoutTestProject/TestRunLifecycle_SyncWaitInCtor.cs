// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TimeoutTestProject;
[TestClass]
public sealed class TestRunLifecycle_SyncWaitInCtor : IDisposable
{
    private static TestContext s_testContext;

    public TestContext TestContext { get; set; }

    public TestRunLifecycle_SyncWaitInCtor()
    {
        s_testContext.WriteLine("Ctor was called");
        Thread.Sleep(100_000);
        TestContext.WriteLine("Ctor end was reached");
    }

    [ClassInitialize]
    public static void ClassInitialize(TestContext testContext)
    {
        s_testContext = testContext;
    }

    [TestInitialize]
    public void TestInitialize()
    {
        TestContext.WriteLine("TestInitialize was called");
    }

    [TestMethod]
    [Timeout(500)]
    public void TestMethod()
    {
        TestContext.WriteLine("TestMethod was called");
    }

    [TestCleanup]
    public void TestCleanup()
    {
        TestContext.WriteLine("TestCleanup was called");
    }

    public void Dispose()
    {
        TestContext.WriteLine("Dispose was called");
    }
}
