// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SuiteLifeCycleTestProject;

[TestClass]
public sealed class LifeCycleDerivedClassCleanupEndOfClassAndNone : LifeCycleClassCleanupEndOfClassAndNone
{
    private static TestContext s_testContext;

    public TestContext DerivedClassTestContext { get; set; }

    public LifeCycleDerivedClassCleanupEndOfClassAndNone()
    {
        s_testContext.WriteLine("LifeCycleDerivedClassCleanupEndOfClassAndNone.ctor was called");
    }

    [ClassInitialize]
    public static void DerivedClassInitialize(TestContext testContext)
    {
        s_testContext = testContext;
        s_testContext.WriteLine("LifeCycleDerivedClassCleanupEndOfClassAndNone.ClassInitialize was called");
    }

    [TestInitialize]
    public void DerivedClassTestInitialize()
    {
        TestContext.WriteLine("LifeCycleDerivedClassCleanupEndOfClassAndNone.TestInitialize was called");
    }

    [TestMethod]
    public void DerivedClassTestMethod()
    {
        TestContext.WriteLine("LifeCycleDerivedClassCleanupEndOfClassAndNone.TestMethod was called");
    }

    [TestCleanup]
    public void DerivedClassTestCleanup()
    {
        TestContext.WriteLine("LifeCycleDerivedClassCleanupEndOfClassAndNone.TestCleanup was called");
    }

    [ClassCleanup]
    public static void DerivedClassCleanup()
    {
        s_testContext.WriteLine("LifeCycleDerivedClassCleanupEndOfClassAndNone.ClassCleanup was called");
    }
}
