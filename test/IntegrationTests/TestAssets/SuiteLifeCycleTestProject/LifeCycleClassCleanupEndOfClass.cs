// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SuiteLifeCycleTestProject;

[TestClass]
public sealed class LifeCycleClassCleanupEndOfClass
#if NET6_0_OR_GREATER
    : IDisposable, IAsyncDisposable
#else
    : IDisposable
#endif
{
    private static TestContext s_testContext;

    public TestContext TestContext { get; set; }

    public LifeCycleClassCleanupEndOfClass()
    {
        s_testContext.WriteLine("LifeCycleClassCleanupEndOfClass.ctor was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfClass.ctor was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfClass.ctor was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfClass.ctor was called");
    }

    [ClassInitialize]
    public static void ClassInitialize(TestContext testContext)
    {
        s_testContext = testContext;
        s_testContext.WriteLine("LifeCycleClassCleanupEndOfClass.ClassInitialize was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfClass.ClassInitialize was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfClass.ClassInitialize was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfClass.ClassInitialize was called");
    }

    [TestInitialize]
    public void TestInitialize()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfClass.TestInitialize was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfClass.TestInitialize was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfClass.TestInitialize was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfClass.TestInitialize was called");
    }

    [TestMethod]
    public void TestMethod()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfClass.TestMethod was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfClass.TestMethod was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfClass.TestMethod was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfClass.TestMethod was called");
    }

    [TestCleanup]
    public void TestCleanup()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfClass.TestCleanup was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfClass.TestCleanup was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfClass.TestCleanup was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfClass.TestCleanup was called");
    }

    public void Dispose()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfClass.Dispose was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfClass.Dispose was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfClass.Dispose was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfClass.Dispose was called");
    }

#if NET6_0_OR_GREATER
    public ValueTask DisposeAsync()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfClass.DisposeAsync was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfClass.DisposeAsync was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfClass.DisposeAsync was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfClass.DisposeAsync was called");
        return ValueTask.CompletedTask;
    }
#endif

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static void ClassCleanup()
    {
        s_testContext.WriteLine("LifeCycleClassCleanupEndOfClass.ClassCleanup was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfClass.ClassCleanup was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfClass.ClassCleanup was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfClass.ClassCleanup was called");
    }
}
