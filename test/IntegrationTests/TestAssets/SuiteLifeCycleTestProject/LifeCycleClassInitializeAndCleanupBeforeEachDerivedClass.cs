// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SuiteLifeCycleTestProject;

[SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "For test purposes")]
[TestClass]
public class LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass
#if NET6_0_OR_GREATER
    : IDisposable, IAsyncDisposable
#else
    : IDisposable
#endif
{
    private static TestContext s_testContext;

    public TestContext TestContext { get; set; }

    public LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass()
    {
        s_testContext.WriteLine("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called");
        Console.WriteLine("Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called");
    }

    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void ClassInitialize(TestContext testContext)
    {
        s_testContext = testContext;
        s_testContext.WriteLine("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassInitialize was called");
        Console.WriteLine("Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassInitialize was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassInitialize was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassInitialize was called");
    }

    [TestInitialize]
    public void TestInitialize()
    {
        TestContext.WriteLine("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called");
        Console.WriteLine("Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called");
    }

    [TestMethod]
    public void TestMethod()
    {
        TestContext.WriteLine("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called");
        Console.WriteLine("Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called");
    }

    [TestCleanup]
    public void TestCleanup()
    {
        TestContext.WriteLine("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called");
        Console.WriteLine("Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called");
    }

    public void Dispose()
    {
        TestContext.WriteLine("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called");
        Console.WriteLine("Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.Dispose was called");
    }

#if NET6_0_OR_GREATER
    public ValueTask DisposeAsync()
    {
        TestContext.WriteLine("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.DisposeAsync was called");
        Console.WriteLine("Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.DisposeAsync was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.DisposeAsync was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.DisposeAsync was called");
        return ValueTask.CompletedTask;
    }
#endif

    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void ClassCleanup()
    {
        s_testContext.WriteLine("LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called");
        Console.WriteLine("Console: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called");
    }
}
