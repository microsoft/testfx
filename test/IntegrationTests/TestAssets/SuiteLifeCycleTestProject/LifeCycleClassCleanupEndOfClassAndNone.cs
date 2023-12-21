// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SuiteLifeCycleTestProject;

[SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "For test purposes")]
[TestClass]
public class LifeCycleClassCleanupEndOfClassAndNone
#if NET6_0_OR_GREATER
    : IDisposable, IAsyncDisposable
#else
    : IDisposable
#endif
{
    private static TestContext s_testContext;

    public TestContext TestContext { get; set; }

    public LifeCycleClassCleanupEndOfClassAndNone()
    {
        s_testContext.WriteLine("LifeCycleClassCleanupEndOfClassAndNone.ctor was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfClassAndNone.ctor was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfClassAndNone.ctor was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfClassAndNone.ctor was called");
    }

    [ClassInitialize]
    public static void ClassInitialize(TestContext testContext)
    {
        s_testContext = testContext;
        s_testContext.WriteLine("LifeCycleClassCleanupEndOfClassAndNone.ClassInitialize was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfClassAndNone.ClassInitialize was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfClassAndNone.ClassInitialize was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfClassAndNone.ClassInitialize was called");
    }

    [TestInitialize]
    public void TestInitialize()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfClassAndNone.TestInitialize was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfClassAndNone.TestInitialize was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfClassAndNone.TestInitialize was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfClassAndNone.TestInitialize was called");
    }

    [TestMethod]
    public void TestMethod()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfClassAndNone.TestMethod was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfClassAndNone.TestMethod was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfClassAndNone.TestMethod was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfClassAndNone.TestMethod was called");
    }

    [TestCleanup]
    public void TestCleanup()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfClassAndNone.TestCleanup was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfClassAndNone.TestCleanup was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfClassAndNone.TestCleanup was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfClassAndNone.TestCleanup was called");
    }

    public void Dispose()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfClassAndNone.Dispose was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfClassAndNone.Dispose was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfClassAndNone.Dispose was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfClassAndNone.Dispose was called");
    }

#if NET6_0_OR_GREATER
    public ValueTask DisposeAsync()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfClassAndNone.DisposeAsync was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfClassAndNone.DisposeAsync was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfClassAndNone.DisposeAsync was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfClassAndNone.DisposeAsync was called");
        return ValueTask.CompletedTask;
    }
#endif

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static void ClassCleanup()
    {
        s_testContext.WriteLine("LifeCycleClassCleanupEndOfClassAndNone.ClassCleanup was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfClassAndNone.ClassCleanup was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfClassAndNone.ClassCleanup was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfClassAndNone.ClassCleanup was called");
    }
}
