// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SuiteLifeCycleTestProject;

[TestClass]
public sealed class LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass :
    LifeCycleClassInitializeNoneAndClassCleanupBeforeEachDerivedClass
{
    private static TestContext s_testContext;

    public TestContext DerivedClassTestContext { get; set; }

    public LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass()
    {
        s_testContext.WriteLine("LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called");
        Console.WriteLine("Console: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ctor was called");
    }

    [ClassInitialize]
    public static void DerivedClassInitialize(TestContext testContext)
    {
        s_testContext = testContext;
        s_testContext.WriteLine("LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassInitialize was called");
        Console.WriteLine("Console: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassInitialize was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassInitialize was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassInitialize was called");
    }

    [TestInitialize]
    public void DerivedClassTestInitialize()
    {
        TestContext.WriteLine("LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called");
        Console.WriteLine("Console: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestInitialize was called");
    }

    [TestMethod]
    public void DerivedClassTestMethod()
    {
        TestContext.WriteLine("LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod was called");
        Console.WriteLine("Console: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestMethod was called");
    }

    [TestCleanup]
    public void DerivedClassTestCleanup()
    {
        TestContext.WriteLine("LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called");
        Console.WriteLine("Console: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.TestCleanup was called");
    }

    [ClassCleanup]
    public static void DerivedClassCleanup()
    {
        s_testContext.WriteLine("LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassCleanup was called");
        Console.WriteLine("Console: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassCleanup was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassCleanup was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassInitializeNoneAndClassCleanupBeforeEachDerivedClass.ClassCleanup was called");
    }
}
