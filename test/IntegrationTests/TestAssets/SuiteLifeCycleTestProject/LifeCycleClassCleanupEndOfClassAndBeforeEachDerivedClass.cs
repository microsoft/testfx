// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SuiteLifeCycleTestProject;

[SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "For test purposes")]
[TestClass]
public class LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass
#if NET6_0_OR_GREATER
    : IDisposable, IAsyncDisposable
#else
    : IDisposable
#endif
{
    private static TestContext s_testContext;

    public TestContext TestContext { get; set; }

    public LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass()
    {
        s_testContext.WriteLine("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called");
    }

    [ClassInitialize]
    public static void ClassInitialize(TestContext testContext)
    {
        s_testContext = testContext;
        s_testContext.WriteLine("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassInitialize was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassInitialize was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassInitialize was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassInitialize was called");
    }

    [TestInitialize]
    public void TestInitialize()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called");
    }

    [TestMethod]
    public void TestMethod()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called");
    }

    [TestCleanup]
    public void TestCleanup()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called");
    }

    public void Dispose()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.Dispose was called");
    }

#if NET6_0_OR_GREATER
    public ValueTask DisposeAsync()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.DisposeAsync was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.DisposeAsync was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.DisposeAsync was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.DisposeAsync was called");
        return ValueTask.CompletedTask;
    }
#endif

    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass, ClassCleanupBehavior.EndOfClass)]
    public static void ClassCleanup()
    {
        s_testContext.WriteLine("LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassCleanup was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassCleanup was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassCleanup was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassCleanup was called");
    }
}
