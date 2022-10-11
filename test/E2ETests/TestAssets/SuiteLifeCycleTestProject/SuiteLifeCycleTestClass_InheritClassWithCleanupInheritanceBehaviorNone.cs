// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SuiteLifeCycleTestProject;

[TestClass]
public sealed class SuiteLifeCycleTestClass_InheritClassWithCleanupInheritanceBehaviorNone : SuiteLifeCycleTestClass_ClassInitializeAndCleanupWithInheritanceBehaviorNone
{
    private static TestContext s_testContext;

    public TestContext DerivedClassTestContext { get; set; }

    public SuiteLifeCycleTestClass_InheritClassWithCleanupInheritanceBehaviorNone()
    {
        s_testContext.WriteLine("Derived class Ctor was called");
    }

    [ClassInitialize]
    public static void DerivedClassInitialize(TestContext testContext)
    {
        s_testContext = testContext;
        s_testContext.WriteLine("Derived ClassInitialize was called");
    }

    [TestInitialize]
    public void DerivedClassTestInitialize()
    {
        TestContext.WriteLine("Derived class TestInitialize was called");
    }

    [TestMethod]
    public void DerivedClassTestMethod()
    {
        TestContext.WriteLine("Derived class TestMethod was called");
    }

    [TestCleanup]
    public void DerivedClassTestCleanup()
    {
        TestContext.WriteLine("Derived class TestCleanup was called");
    }

    [ClassCleanup]
    public static void DerivedClassCleanup()
    {
        s_testContext.WriteLine("Derived ClassCleanup was called");
    }
}
