// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SuiteLifeCycleTestProject;

[SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "For test purposes")]
[TestClass]
public class LifeCycleClassCleanupEndOfAssemblyAndNone
#if NET6_0_OR_GREATER
    : IDisposable, IAsyncDisposable
#else
    : IDisposable
#endif
{
    private static TestContext s_testContext;

    public TestContext TestContext { get; set; }

    public LifeCycleClassCleanupEndOfAssemblyAndNone()
    {
        s_testContext.WriteLine("LifeCycleClassCleanupEndOfAssemblyAndNone.ctor was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfAssemblyAndNone.ctor was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfAssemblyAndNone.ctor was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfAssemblyAndNone.ctor was called");
    }

    [ClassInitialize]
    public static void ClassInitialize(TestContext testContext)
    {
        s_testContext = testContext;
        s_testContext.WriteLine("LifeCycleClassCleanupEndOfAssemblyAndNone.ClassInitialize was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfAssemblyAndNone.ClassInitialize was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfAssemblyAndNone.ClassInitialize was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfAssemblyAndNone.ClassInitialize was called");
    }

    [TestInitialize]
    public void TestInitialize()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfAssemblyAndNone.TestInitialize was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfAssemblyAndNone.TestInitialize was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfAssemblyAndNone.TestInitialize was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfAssemblyAndNone.TestInitialize was called");
    }

    [TestMethod]
    public void TestMethod()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfAssemblyAndNone.TestMethod was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfAssemblyAndNone.TestMethod was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfAssemblyAndNone.TestMethod was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfAssemblyAndNone.TestMethod was called");
    }

    [TestCleanup]
    public void TestCleanup()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfAssemblyAndNone.TestCleanup was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfAssemblyAndNone.TestCleanup was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfAssemblyAndNone.TestCleanup was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfAssemblyAndNone.TestCleanup was called");
    }

    public void Dispose()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called");
    }

#if NET6_0_OR_GREATER
    public ValueTask DisposeAsync()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfAssemblyAndNone.DisposeAsync was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfAssemblyAndNone.DisposeAsync was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfAssemblyAndNone.DisposeAsync was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfAssemblyAndNone.DisposeAsync was called");
        return ValueTask.CompletedTask;
    }
#endif

    [ClassCleanup(ClassCleanupBehavior.EndOfAssembly)]
    public static void ClassCleanup()
    {
        s_testContext.WriteLine("LifeCycleClassCleanupEndOfAssemblyAndNone.ClassCleanup was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfAssemblyAndNone.ClassCleanup was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfAssemblyAndNone.ClassCleanup was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfAssemblyAndNone.ClassCleanup was called");
    }
}
