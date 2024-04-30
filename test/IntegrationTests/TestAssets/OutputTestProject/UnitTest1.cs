// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OutputTestProject;

[TestClass]
public class UnitTest1
{
    private static readonly Random Rng = new();

    public TestContext TestContext { get; set; }

    [ClassInitialize]
    public static void ClassInitialize(TestContext testContext) => WriteLines("UnitTest1 - ClassInitialize");

    [TestInitialize]
    public void TestInitialize() => WriteLines("UnitTest1 - TestInitialize");

    [TestCleanup]
    public void TestCleanup() => WriteLines("UnitTest1 - TestCleanup");

    [ClassCleanup]
    public static void ClassCleanup() => WriteLines("UnitTest1 - ClassCleanup");

    [TestMethod]
    public void TestMethod1()
    {
        WriteLines("UnitTest1 - TestMethod1 - Call 1");

        // This makes the outputs more likely to run into each other
        // when running in parallel.
        // It also makes the test longer, because we check in the test
        // that all tests started before any test finished (to make sure
        // they actually run in parallel), and this gives us more leeway
        // on slower machines.
        Thread.Sleep(Rng.Next(20, 50));
        WriteLines("UnitTest1 - TestMethod1 - Call 2");
        Thread.Sleep(Rng.Next(20, 50));
        WriteLines("UnitTest1 - TestMethod1 - Call 3");
    }

    [TestMethod]
    public void TestMethod2()
    {
        WriteLines("UnitTest1 - TestMethod2 - Call 1");
        Thread.Sleep(Rng.Next(20, 50));
        WriteLines("UnitTest1 - TestMethod2 - Call 2");
        Thread.Sleep(Rng.Next(20, 50));
        WriteLines("UnitTest1 - TestMethod2 - Call 3");
    }

    [TestMethod]
    public void TestMethod3()
    {
        WriteLines("UnitTest1 - TestMethod3 - Call 1");
        Thread.Sleep(Rng.Next(20, 50));
        WriteLines("UnitTest1 - TestMethod3 - Call 2");
        Thread.Sleep(Rng.Next(20, 50));
        WriteLines("UnitTest1 - TestMethod3 - Call 3");
    }

    private static void WriteLines(string message)
    {
        Trace.WriteLine(message);
        Console.WriteLine(message);
        Console.Error.WriteLine(message);
    }
}
