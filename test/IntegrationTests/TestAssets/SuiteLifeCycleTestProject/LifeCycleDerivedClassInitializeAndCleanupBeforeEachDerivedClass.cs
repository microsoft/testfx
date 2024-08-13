// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SuiteLifeCycleTestProject;

[TestClass]
public sealed class LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass : LifeCycleClassInitializeAndCleanupBeforeEachDerivedClass
{
    private static TestContext s_testContext;

    public TestContext DerivedClassTestContext { get; set; }

    public LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass()
    {
        s_testContext.WriteLine("LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called");
        Console.WriteLine("Console: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ctor was called");
    }

    [ClassInitialize]
    public static void DerivedClassInitialize(TestContext testContext)
    {
        s_testContext = testContext;
        s_testContext.WriteLine("LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ClassInitialize was called");
        Console.WriteLine("Console: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ClassInitialize was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ClassInitialize was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ClassInitialize was called");
    }

    [TestInitialize]
    public void DerivedClassTestInitialize()
    {
        TestContext.WriteLine("LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called");
        Console.WriteLine("Console: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestInitialize was called");
    }

    [TestMethod]
    public void DerivedClassTestMethod()
    {
        TestContext.WriteLine("LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called");
        Console.WriteLine("Console: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestMethod was called");
    }

    [TestCleanup]
    public void DerivedClassTestCleanup()
    {
        TestContext.WriteLine("LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called");
        Console.WriteLine("Console: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.TestCleanup was called");
    }

    [ClassCleanup]
    public static void DerivedClassCleanup()
    {
        s_testContext.WriteLine("LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called");
        Console.WriteLine("Console: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassInitializeAndCleanupBeforeEachDerivedClass.ClassCleanup was called");
    }
}
