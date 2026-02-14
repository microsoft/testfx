// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SuiteLifeCycleTestProject;

[TestClass]
public sealed class LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone
    : LifeCycleClassInitializeBeforeEachDerivedClassAndClassCleanupNone
{
    public TestContext DerivedClassTestContext { get; set; } = null!;

    public LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone(TestContext testContext)
        : base(testContext)
    {
        testContext.WriteLine("LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called");
        Console.WriteLine("Console: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ctor was called");
    }

    [ClassInitialize]
    public static void DerivedClassInitialize(TestContext testContext)
    {
        testContext.WriteLine("LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassInitialize was called");
        Console.WriteLine("Console: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassInitialize was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassInitialize was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassInitialize was called");
    }

    [TestInitialize]
    public void DerivedClassTestInitialize()
    {
        TestContext.WriteLine("LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called");
        Console.WriteLine("Console: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestInitialize was called");
    }

    [TestMethod]
    public void DerivedClassTestMethod()
    {
        TestContext.WriteLine("LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called");
        Console.WriteLine("Console: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestMethod was called");
    }

    [TestCleanup]
    public void DerivedClassTestCleanup()
    {
        TestContext.WriteLine("LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called");
        Console.WriteLine("Console: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.TestCleanup was called");
    }

    [ClassCleanup]
    public static void DerivedClassCleanup(TestContext testContext)
    {
        testContext.WriteLine("LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassCleanup was called");
        Console.WriteLine("Console: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassCleanup was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassCleanup was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassInitializeBeforeEachDerivedClassAndClassCleanupNone.ClassCleanup was called");
    }
}
