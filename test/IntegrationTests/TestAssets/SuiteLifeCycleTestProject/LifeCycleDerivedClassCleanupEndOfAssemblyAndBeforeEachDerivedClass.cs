// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SuiteLifeCycleTestProject;

[TestClass]
public sealed class LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass : LifeCycleClassCleanupEndOfAssemblyAndBeforeEachDerivedClass
{
    private static TestContext s_testContext;

    public TestContext DerivedClassTestContext { get; set; }

    public LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass()
    {
        s_testContext.WriteLine("LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called");
        Console.WriteLine("Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ctor was called");
    }

    [ClassInitialize]
    public static void DerivedClassInitialize(TestContext testContext)
    {
        s_testContext = testContext;
        s_testContext.WriteLine("LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassInitialize was called");
        Console.WriteLine("Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassInitialize was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassInitialize was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassInitialize was called");
    }

    [TestInitialize]
    public void DerivedClassTestInitialize()
    {
        TestContext.WriteLine("LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called");
        Console.WriteLine("Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestInitialize was called");
    }

    [TestMethod]
    public void DerivedClassTestMethod()
    {
        TestContext.WriteLine("LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestMethod was called");
        Console.WriteLine("Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestMethod was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestMethod was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestMethod was called");
    }

    [TestCleanup]
    public void DerivedClassTestCleanup()
    {
        TestContext.WriteLine("LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called");
        Console.WriteLine("Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.TestCleanup was called");
    }

    [ClassCleanup]
    public static void DerivedClassCleanup()
    {
        s_testContext.WriteLine("LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassCleanup was called");
        Console.WriteLine("Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassCleanup was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassCleanup was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassCleanupEndOfAssemblyAndBeforeEachDerivedClass.ClassCleanup was called");
    }
}
