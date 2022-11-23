// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SuiteLifeCycleTestProject;

[TestClass]
public sealed class SuiteLifeCycleTestClass_InheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone
    : SuiteLifeCycleTestClass_ClassInitializeWithInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupWithInheritanceBehaviorNone
{
    private static TestContext s_testContext;

    public TestContext DerivedClassTestContext { get; set; }

    static SuiteLifeCycleTestClass_InheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone()
    {
        s_messagePrefix = "Base ";
    }

    public SuiteLifeCycleTestClass_InheritClassWithClassInitializeInheritanceBehaviorBeforeEachDerivedClassAndClassCleanupInheritanceBehaviorNone()
    {
        s_testContext.WriteLine("Current Ctor was called");
    }

    [ClassInitialize]
    public static void DerivedClassInitialize(TestContext testContext)
    {
        s_testContext = testContext;
        s_testContext.WriteLine("Current ClassInitialize was called");
    }

    [TestInitialize]
    public void DerivedClassTestInitialize()
    {
        TestContext.WriteLine("Current TestInitialize was called");
    }

    [TestMethod]
    public void DerivedClassTestMethod()
    {
        TestContext.WriteLine("Current TestMethod was called");
    }

    [TestCleanup]
    public void DerivedClassTestCleanup()
    {
        TestContext.WriteLine("Current TestCleanup was called");
    }

    [ClassCleanup]
    public static void DerivedClassCleanup()
    {
        s_testContext.WriteLine("Current ClassCleanup was called");
    }
}
