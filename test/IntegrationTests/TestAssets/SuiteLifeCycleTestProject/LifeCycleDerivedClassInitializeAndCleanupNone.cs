// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SuiteLifeCycleTestProject;

[TestClass]
public sealed class LifeCycleDerivedClassInitializeAndCleanupNone : LifeCycleClassInitializeAndCleanupNone
{
    public TestContext DerivedClassTestContext { get; set; } = null!;

    public LifeCycleDerivedClassInitializeAndCleanupNone(TestContext testContext)
        : base(testContext)
    {
        testContext.WriteLine("LifeCycleDerivedClassInitializeAndCleanupNone.ctor was called");
        Console.WriteLine("Console: LifeCycleDerivedClassInitializeAndCleanupNone.ctor was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassInitializeAndCleanupNone.ctor was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassInitializeAndCleanupNone.ctor was called");
    }

    [ClassInitialize]
    public static void DerivedClassInitialize(TestContext testContext)
    {
        testContext.WriteLine("LifeCycleDerivedClassInitializeAndCleanupNone.ClassInitialize was called");
        Console.WriteLine("Console: LifeCycleDerivedClassInitializeAndCleanupNone.ClassInitialize was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassInitializeAndCleanupNone.ClassInitialize was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassInitializeAndCleanupNone.ClassInitialize was called");
    }

    [TestInitialize]
    public void DerivedClassTestInitialize()
    {
        TestContext.WriteLine("LifeCycleDerivedClassInitializeAndCleanupNone.TestInitialize was called");
        Console.WriteLine("Console: LifeCycleDerivedClassInitializeAndCleanupNone.TestInitialize was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassInitializeAndCleanupNone.TestInitialize was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassInitializeAndCleanupNone.TestInitialize was called");
    }

    [TestMethod]
    public void DerivedClassTestMethod()
    {
        TestContext.WriteLine("LifeCycleDerivedClassInitializeAndCleanupNone.TestMethod was called");
        Console.WriteLine("Console: LifeCycleDerivedClassInitializeAndCleanupNone.TestMethod was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassInitializeAndCleanupNone.TestMethod was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassInitializeAndCleanupNone.TestMethod was called");
    }

    [TestCleanup]
    public void DerivedClassTestCleanup()
    {
        TestContext.WriteLine("LifeCycleDerivedClassInitializeAndCleanupNone.TestCleanup was called");
        Console.WriteLine("Console: LifeCycleDerivedClassInitializeAndCleanupNone.TestCleanup was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassInitializeAndCleanupNone.TestCleanup was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassInitializeAndCleanupNone.TestCleanup was called");
    }

    [ClassCleanup]
    public static void DerivedClassCleanup(TestContext testContext)
    {
        testContext.WriteLine("LifeCycleDerivedClassInitializeAndCleanupNone.ClassCleanup was called");
        Console.WriteLine("Console: LifeCycleDerivedClassInitializeAndCleanupNone.ClassCleanup was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassInitializeAndCleanupNone.ClassCleanup was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassInitializeAndCleanupNone.ClassCleanup was called");
    }
}
