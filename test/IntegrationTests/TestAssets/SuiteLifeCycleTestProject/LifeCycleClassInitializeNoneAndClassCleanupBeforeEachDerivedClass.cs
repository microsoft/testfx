// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SuiteLifeCycleTestProject;

[SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "For test purposes")]
[TestClass]
public class LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass
#if NET6_0_OR_GREATER
    : IDisposable, IAsyncDisposable
#else
    : IDisposable
#endif
{
    private static TestContext s_testContext;

    public TestContext TestContext { get; set; }

    public LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass()
    {
        s_testContext.WriteLine("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called");
        Console.WriteLine("Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called");
    }

    [ClassInitialize(InheritanceBehavior.None)]
    public static void ClassInitialize(TestContext testContext)
    {
        s_testContext = testContext;
        s_testContext.WriteLine("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassInitialize was called");
        Console.WriteLine("Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassInitialize was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassInitialize was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassInitialize was called");
    }

    [TestInitialize]
    public void TestInitialize()
    {
        TestContext.WriteLine("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called");
        Console.WriteLine("Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called");
    }

    [TestMethod]
    public void TestMethod()
    {
        TestContext.WriteLine("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod was called");
        Console.WriteLine("Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod was called");
    }

    [TestCleanup]
    public void TestCleanup()
    {
        TestContext.WriteLine("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called");
        Console.WriteLine("Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called");
    }

    public void Dispose()
    {
        TestContext.WriteLine("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called");
        Console.WriteLine("Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.Dispose was called");
    }

#if NET6_0_OR_GREATER
    public ValueTask DisposeAsync()
    {
        TestContext.WriteLine("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DisposeAsync was called");
        Console.WriteLine("Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DisposeAsync was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DisposeAsync was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.DisposeAsync was called");
        return ValueTask.CompletedTask;
    }
#endif

    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void ClassCleanup()
    {
        s_testContext.WriteLine("LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassCleanup was called");
        Console.WriteLine("Console: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassCleanup was called");
        Trace.WriteLine("Trace: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassCleanup was called");
        Debug.WriteLine("Debug: LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassCleanup was called");
    }
}
