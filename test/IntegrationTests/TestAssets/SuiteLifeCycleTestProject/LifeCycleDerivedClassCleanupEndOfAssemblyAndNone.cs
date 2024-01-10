// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SuiteLifeCycleTestProject;

[TestClass]
public sealed class LifeCycleDerivedClassCleanupEndOfAssemblyAndNone : LifeCycleClassCleanupEndOfAssemblyAndNone
{
    private static TestContext s_testContext;

    public TestContext DerivedClassTestContext { get; set; }

    public LifeCycleDerivedClassCleanupEndOfAssemblyAndNone()
    {
        s_testContext.WriteLine("LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ctor was called");
        Console.WriteLine("Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ctor was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ctor was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ctor was called");
    }

    [ClassInitialize]
    public static void DerivedClassInitialize(TestContext testContext)
    {
        s_testContext = testContext;
        s_testContext.WriteLine("LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ClassInitialize was called");
        Console.WriteLine("Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ClassInitialize was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ClassInitialize was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ClassInitialize was called");
    }

    [TestInitialize]
    public void DerivedClassTestInitialize()
    {
        TestContext.WriteLine("LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestInitialize was called");
        Console.WriteLine("Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestInitialize was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestInitialize was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestInitialize was called");
    }

    [TestMethod]
    public void DerivedClassTestMethod()
    {
        TestContext.WriteLine("LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestMethod was called");
        Console.WriteLine("Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestMethod was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestMethod was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestMethod was called");
    }

    [TestCleanup]
    public void DerivedClassTestCleanup()
    {
        TestContext.WriteLine("LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestCleanup was called");
        Console.WriteLine("Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestCleanup was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestCleanup was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.TestCleanup was called");
    }

    [ClassCleanup]
    public static void DerivedClassCleanup()
    {
        s_testContext.WriteLine("LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ClassCleanup was called");
        Console.WriteLine("Console: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ClassCleanup was called");
        Trace.WriteLine("Trace: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ClassCleanup was called");
        Debug.WriteLine("Debug: LifeCycleDerivedClassCleanupEndOfAssemblyAndNone.ClassCleanup was called");
    }
}
