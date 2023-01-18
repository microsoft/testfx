// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OutputTestProject;

[TestClass]
public class UnitTest3
{
    private static readonly Random Rng = new();

    private static TestContext s_testContext;

    public TestContext TestContext { get; set; }

    [ClassInitialize]
    public static void ClassInitialize(TestContext testContext)
    {
        s_testContext = testContext;
        s_testContext.WriteLine("UnitTest3 - ClassInitialize");
    }

    [TestInitialize]
    public void TestInitialize()
    {
        TestContext.WriteLine("UnitTest3 - TestInitialize");
    }

    [TestCleanup]
    public void TestCleanup()
    {
        TestContext.WriteLine("UnitTest3 - TestCleanup");
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        s_testContext.WriteLine("UnitTest3 - ClassCleanup");
    }

    [TestMethod]
    public void TestMethod1()
    {
        TestContext.WriteLine("UnitTest3 - TestMethod1 - Call 1");

        // This makes the outputs more likely to run into each other
        // when running in parallel.
        // It also makes the test longer, because we check in the test
        // that all tests started before any test finished (to make sure
        // they actually run in parallel), and this gives us more leeway
        // on slower machines.
        Thread.Sleep(Rng.Next(20, 50));
        TestContext.WriteLine("UnitTest3 - TestMethod1 - Call 2");
        Thread.Sleep(Rng.Next(20, 50));
        TestContext.WriteLine("UnitTest3 - TestMethod1 - Call 3");
    }

    [TestMethod]
    public void TestMethod2()
    {
        TestContext.WriteLine("UnitTest3 - TestMethod2 - Call 1");
        Thread.Sleep(Rng.Next(20, 50));
        TestContext.WriteLine("UnitTest3 - TestMethod2 - Call 2");
        Thread.Sleep(Rng.Next(20, 50));
        TestContext.WriteLine("UnitTest3 - TestMethod2 - Call 3");
    }

    [TestMethod]
    public void TestMethod3()
    {
        TestContext.WriteLine("UnitTest3 - TestMethod3 - Call 1");
        Thread.Sleep(Rng.Next(20, 50));
        TestContext.WriteLine("UnitTest3 - TestMethod3 - Call 2");
        Thread.Sleep(Rng.Next(20, 50));
        TestContext.WriteLine("UnitTest3 - TestMethod3 - Call 3");
    }
}
