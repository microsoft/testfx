// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SuiteLifeCycleTestProject;

[TestClass]
public sealed class LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass : LifeCycleClassCleanupEndOfClassAndBeforeEachDerivedClass
{
    private static TestContext s_testContext;

    public TestContext DerivedClassTestContext { get; set; }

    public LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass()
    {
        s_testContext.WriteLine("LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called");
        Console.WriteLine("Console: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ctor was called");
    }

    [ClassInitialize]
    public static void DerivedClassInitialize(TestContext testContext)
    {
        s_testContext = testContext;
        s_testContext.WriteLine("LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassInitialize was called");
        Console.WriteLine("Console: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassInitialize was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassInitialize was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassInitialize was called");
    }

    [TestInitialize]
    public void DerivedClassTestInitialize()
    {
        TestContext.WriteLine("LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called");
        Console.WriteLine("Console: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestInitialize was called");
    }

    [TestMethod]
    public void DerivedClassTestMethod()
    {
        TestContext.WriteLine("LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called");
        Console.WriteLine("Console: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestMethod was called");
    }

    [TestCleanup]
    public void DerivedClassTestCleanup()
    {
        TestContext.WriteLine("LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called");
        Console.WriteLine("Console: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.TestCleanup was called");
    }

    [ClassCleanup]
    public static void DerivedClassCleanup()
    {
        s_testContext.WriteLine("LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassCleanup was called");
        Console.WriteLine("Console: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassCleanup was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassCleanup was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassCleanupEndOfClassAndBeforeEachDerivedClass.ClassCleanup was called");
    }
}
