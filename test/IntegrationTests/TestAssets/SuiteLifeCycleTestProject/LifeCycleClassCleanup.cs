// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SuiteLifeCycleTestProject;

[TestClass]
public sealed class LifeCycleClassCleanup
#if NET6_0_OR_GREATER
    : IDisposable, IAsyncDisposable
#else
    : IDisposable
#endif
{
    private static TestContext s_testContext;

    public TestContext TestContext { get; set; }

    public LifeCycleClassCleanup()
    {
        s_testContext.WriteLine("LifeCycleClassCleanup.ctor was called");
        Console.WriteLine("Console: LifeCycleClassCleanup.ctor was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanup.ctor was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanup.ctor was called");
    }

    [ClassInitialize]
    public static void ClassInitialize(TestContext testContext)
    {
        s_testContext = testContext;
        s_testContext.WriteLine("LifeCycleClassCleanup.ClassInitialize was called");
        Console.WriteLine("Console: LifeCycleClassCleanup.ClassInitialize was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanup.ClassInitialize was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanup.ClassInitialize was called");
    }

    [TestInitialize]
    public void TestInitialize()
    {
        TestContext.WriteLine("LifeCycleClassCleanup.TestInitialize was called");
        Console.WriteLine("Console: LifeCycleClassCleanup.TestInitialize was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanup.TestInitialize was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanup.TestInitialize was called");
    }

    [TestMethod]
    public void TestMethod()
    {
        TestContext.WriteLine("LifeCycleClassCleanup.TestMethod was called");
        Console.WriteLine("Console: LifeCycleClassCleanup.TestMethod was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanup.TestMethod was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanup.TestMethod was called");
    }

    [TestCleanup]
    public void TestCleanup()
    {
        TestContext.WriteLine("LifeCycleClassCleanup.TestCleanup was called");
        Console.WriteLine("Console: LifeCycleClassCleanup.TestCleanup was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanup.TestCleanup was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanup.TestCleanup was called");
    }

    public void Dispose()
    {
        TestContext.WriteLine("LifeCycleClassCleanup.Dispose was called");
        Console.WriteLine("Console: LifeCycleClassCleanup.Dispose was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanup.Dispose was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanup.Dispose was called");
    }

#if NET6_0_OR_GREATER
    public ValueTask DisposeAsync()
    {
        TestContext.WriteLine("LifeCycleClassCleanup.DisposeAsync was called");
        Console.WriteLine("Console: LifeCycleClassCleanup.DisposeAsync was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanup.DisposeAsync was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanup.DisposeAsync was called");
        return ValueTask.CompletedTask;
    }
#endif

    [ClassCleanup]
    public static void ClassCleanup()
    {
        s_testContext.WriteLine("LifeCycleClassCleanup.ClassCleanup was called");
        Console.WriteLine("Console: LifeCycleClassCleanup.ClassCleanup was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanup.ClassCleanup was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanup.ClassCleanup was called");
    }
}
