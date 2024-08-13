// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SuiteLifeCycleTestProject;

[TestClass]
public sealed class LifeCycleClassCleanupEndOfAssembly
#if NET6_0_OR_GREATER
    : IDisposable, IAsyncDisposable
#else
    : IDisposable
#endif
{
    private static TestContext s_testContext;

    public TestContext TestContext { get; set; }

    public LifeCycleClassCleanupEndOfAssembly()
    {
        s_testContext.WriteLine("LifeCycleClassCleanupEndOfAssembly.ctor was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfAssembly.ctor was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfAssembly.ctor was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfAssembly.ctor was called");
    }

    [ClassInitialize]
    public static void ClassInitialize(TestContext testContext)
    {
        s_testContext = testContext;
        s_testContext.WriteLine("LifeCycleClassCleanupEndOfAssembly.ClassInitialize was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfAssembly.ClassInitialize was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfAssembly.ClassInitialize was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfAssembly.ClassInitialize was called");
    }

    [TestInitialize]
    public void TestInitialize()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfAssembly.TestInitialize was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfAssembly.TestInitialize was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfAssembly.TestInitialize was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfAssembly.TestInitialize was called");
    }

    [TestMethod]
    public void TestMethod()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfAssembly.TestMethod was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfAssembly.TestMethod was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfAssembly.TestMethod was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfAssembly.TestMethod was called");
    }

    [TestCleanup]
    public void TestCleanup()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfAssembly.TestCleanup was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfAssembly.TestCleanup was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfAssembly.TestCleanup was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfAssembly.TestCleanup was called");
    }

    public void Dispose()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfAssembly.Dispose was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfAssembly.Dispose was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfAssembly.Dispose was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfAssembly.Dispose was called");
    }

#if NET6_0_OR_GREATER
    public ValueTask DisposeAsync()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfAssembly.DisposeAsync was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfAssembly.DisposeAsync was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfAssembly.DisposeAsync was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfAssembly.DisposeAsync was called");
        return ValueTask.CompletedTask;
    }
#endif

    [ClassCleanup(ClassCleanupBehavior.EndOfAssembly)]
    public static void ClassCleanup()
    {
        s_testContext.WriteLine("LifeCycleClassCleanupEndOfAssembly.ClassCleanup was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfAssembly.ClassCleanup was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfAssembly.ClassCleanup was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfAssembly.ClassCleanup was called");
    }
}
