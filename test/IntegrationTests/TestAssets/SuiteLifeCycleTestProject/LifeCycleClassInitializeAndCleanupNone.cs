// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SuiteLifeCycleTestProject;

[SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "For test purposes")]
[TestClass]
public class LifeCycleClassInitializeAndCleanupNone
#if NET6_0_OR_GREATER
    : IDisposable, IAsyncDisposable
#else
    : IDisposable
#endif
{
    private static TestContext s_testContext;

    public TestContext TestContext { get; set; }

    public LifeCycleClassInitializeAndCleanupNone()
    {
        s_testContext.WriteLine("LifeCycleClassInitializeAndCleanupNone.ctor was called");
        Console.WriteLine("Console: LifeCycleClassInitializeAndCleanupNone.ctor was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeAndCleanupNone.ctor was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeAndCleanupNone.ctor was called");
    }

    [ClassInitialize(InheritanceBehavior.None)]
    public static void ClassInitialize(TestContext testContext)
    {
        s_testContext = testContext;
        s_testContext.WriteLine("LifeCycleClassInitializeAndCleanupNone.ClassInitialize was called");
        Console.WriteLine("Console: LifeCycleClassInitializeAndCleanupNone.ClassInitialize was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeAndCleanupNone.ClassInitialize was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeAndCleanupNone.ClassInitialize was called");
    }

    [TestInitialize]
    public void TestInitialize()
    {
        TestContext.WriteLine("LifeCycleClassInitializeAndCleanupNone.TestInitialize was called");
        Console.WriteLine("Console: LifeCycleClassInitializeAndCleanupNone.TestInitialize was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeAndCleanupNone.TestInitialize was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeAndCleanupNone.TestInitialize was called");
    }

    [TestMethod]
    public void TestMethod()
    {
        TestContext.WriteLine("LifeCycleClassInitializeAndCleanupNone.TestMethod was called");
        Console.WriteLine("Console: LifeCycleClassInitializeAndCleanupNone.TestMethod was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeAndCleanupNone.TestMethod was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeAndCleanupNone.TestMethod was called");
    }

    [TestCleanup]
    public void TestCleanup()
    {
        TestContext.WriteLine("LifeCycleClassInitializeAndCleanupNone.TestCleanup was called");
        Console.WriteLine("Console: LifeCycleClassInitializeAndCleanupNone.TestCleanup was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeAndCleanupNone.TestCleanup was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeAndCleanupNone.TestCleanup was called");
    }

    public void Dispose()
    {
        TestContext.WriteLine("LifeCycleClassInitializeAndCleanupNone.Dispose was called");
        Console.WriteLine("Console: LifeCycleClassInitializeAndCleanupNone.Dispose was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeAndCleanupNone.Dispose was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeAndCleanupNone.Dispose was called");
    }

#if NET6_0_OR_GREATER
    public ValueTask DisposeAsync()
    {
        TestContext.WriteLine("LifeCycleClassInitializeAndCleanupNone.DisposeAsync was called");
        Console.WriteLine("Console: LifeCycleClassInitializeAndCleanupNone.DisposeAsync was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeAndCleanupNone.DisposeAsync was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeAndCleanupNone.DisposeAsync was called");
        return ValueTask.CompletedTask;
    }
#endif

    [ClassCleanup(InheritanceBehavior.None)]
    public static void ClassCleanup()
    {
        s_testContext.WriteLine("LifeCycleClassInitializeAndCleanupNone.ClassCleanup was called");
        Console.WriteLine("Console: LifeCycleClassInitializeAndCleanupNone.ClassCleanup was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeAndCleanupNone.ClassCleanup was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeAndCleanupNone.ClassCleanup was called");
    }
}
