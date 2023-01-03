// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
#if NET6_0_OR_GREATER
using System.Threading.Tasks;
#endif

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SuiteLifeCycleTestProject;

[TestClass]
public class LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone : IDisposable
#if NET6_0_OR_GREATER
        , IAsyncDisposable 
#endif
{
    private static TestContext s_testContext;
    
    public TestContext TestContext { get; set; }

    public LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone()
    {
        s_testContext.WriteLine("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called");
        Console.WriteLine("Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called");
    }

    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void ClassInitialize(TestContext testContext)
    {
        s_testContext = testContext;
        s_testContext.WriteLine("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassInitialize was called");
        Console.WriteLine("Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassInitialize was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassInitialize was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassInitialize was called");
    }

    [TestInitialize]
    public void TestInitialize()
    {
        TestContext.WriteLine("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called");
        Console.WriteLine("Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called");
    }

    [TestMethod]
    public void TestMethod()
    {
        TestContext.WriteLine("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called");
        Console.WriteLine("Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called");
    }

    [TestCleanup]
    public void TestCleanup()
    {
        TestContext.WriteLine("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called");
        Console.WriteLine("Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called");
    }

    public void Dispose()
    {
        TestContext.WriteLine("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called");
        Console.WriteLine("Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.Dispose was called");
    }

#if NET6_0_OR_GREATER
    public ValueTask DisposeAsync()
    {
        TestContext.WriteLine("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DisposeAsync was called");
        Console.WriteLine("Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DisposeAsync was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DisposeAsync was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.DisposeAsync was called");
        return ValueTask.CompletedTask;
    }
#endif

    [ClassCleanup(InheritanceBehavior.None)]
    public static void ClassCleanup()
    {
        s_testContext.WriteLine("LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassCleanup was called");
        Console.WriteLine("Console: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassCleanup was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassCleanup was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassCleanup was called");
    }
}
