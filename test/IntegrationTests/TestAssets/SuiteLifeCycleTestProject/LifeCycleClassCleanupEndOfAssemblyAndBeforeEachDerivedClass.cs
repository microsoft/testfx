// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SuiteLifeCycleTestProject;

[SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "For test purposes")]
[TestClass]
public class LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass
#if NET6_0_OR_GREATER
    : IDisposable, IAsyncDisposable
#else
    : IDisposable
#endif
{
    private static TestContext s_testContext;

    public TestContext TestContext { get; set; }

    public LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass()
    {
        s_testContext.WriteLine("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called");
    }

    [ClassInitialize]
    public static void ClassInitialize(TestContext testContext)
    {
        s_testContext = testContext;
        s_testContext.WriteLine("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassInitialize was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassInitialize was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassInitialize was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassInitialize was called");
    }

    [TestInitialize]
    public void TestInitialize()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called");
    }

    [TestMethod]
    public void TestMethod()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestMethod was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestMethod was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestMethod was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestMethod was called");
    }

    [TestCleanup]
    public void TestCleanup()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called");
    }

    public void Dispose()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.Dispose was called");
    }

#if NET6_0_OR_GREATER
    public ValueTask DisposeAsync()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.DisposeAsync was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.DisposeAsync was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.DisposeAsync was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.DisposeAsync was called");
        return ValueTask.CompletedTask;
    }
#endif

    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass, ClassCleanupBehavior.EndOfAssembly)]
    public static void ClassCleanup()
    {
        s_testContext.WriteLine("LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassCleanup was called");
        Console.WriteLine("Console: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassCleanup was called");
        Trace.WriteLine("Trace: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassCleanup was called");
        Debug.WriteLine("Debug: LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassCleanup was called");
    }
}
