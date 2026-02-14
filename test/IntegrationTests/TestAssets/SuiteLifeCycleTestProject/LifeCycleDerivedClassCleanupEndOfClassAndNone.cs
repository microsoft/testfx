// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SuiteLifeCycleTestProject;

[TestClass]
public sealed class LifeCycleDerivedClassCleanupEndOfClassAndNone : LifeCycleClassCleanupEndOfClassAndNone
{
    public TestContext DerivedClassTestContext { get; set; } = null!;

    public LifeCycleDerivedClassCleanupEndOfClassAndNone(TestContext testContext)
        : base(testContext)
    {
        testContext.WriteLine("LifeCycleDerivedClassCleanupEndOfClassAndNone.ctor was called");
        Console.WriteLine("Console: LifeCycleDerivedClassCleanupEndOfClassAndNone.ctor was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassCleanupEndOfClassAndNone.ctor was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassCleanupEndOfClassAndNone.ctor was called");
    }

    [ClassInitialize]
    public static void DerivedClassInitialize(TestContext testContext)
    {
        testContext.WriteLine("LifeCycleDerivedClassCleanupEndOfClassAndNone.ClassInitialize was called");
        Console.WriteLine("Console: LifeCycleDerivedClassCleanupEndOfClassAndNone.ClassInitialize was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassCleanupEndOfClassAndNone.ClassInitialize was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassCleanupEndOfClassAndNone.ClassInitialize was called");
    }

    [TestInitialize]
    public void DerivedClassTestInitialize()
    {
        TestContext.WriteLine("LifeCycleDerivedClassCleanupEndOfClassAndNone.TestInitialize was called");
        Console.WriteLine("Console: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestInitialize was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestInitialize was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestInitialize was called");
    }

    [TestMethod]
    public void DerivedClassTestMethod()
    {
        TestContext.WriteLine("LifeCycleDerivedClassCleanupEndOfClassAndNone.TestMethod was called");
        Console.WriteLine("Console: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestMethod was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestMethod was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestMethod was called");
    }

    [TestCleanup]
    public void DerivedClassTestCleanup()
    {
        TestContext.WriteLine("LifeCycleDerivedClassCleanupEndOfClassAndNone.TestCleanup was called");
        Console.WriteLine("Console: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestCleanup was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestCleanup was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassCleanupEndOfClassAndNone.TestCleanup was called");
    }

    [ClassCleanup]
    public static void DerivedClassCleanup(TestContext testContext)
    {
        testContext.WriteLine("LifeCycleDerivedClassCleanupEndOfClassAndNone.ClassCleanup was called");
        Console.WriteLine("Console: LifeCycleDerivedClassCleanupEndOfClassAndNone.ClassCleanup was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassCleanupEndOfClassAndNone.ClassCleanup was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassCleanupEndOfClassAndNone.ClassCleanup was called");
    }
}
