// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
#if NET6_0_OR_GREATER
using System.Threading.Tasks; 
#endif

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SuiteLifeCycleTestProject;

[TestClass]
public class SuiteLifeCycleTestClass_ClassInitializeAndCleanupWithInheritanceBehaviorNone : IDisposable
#if NET6_0_OR_GREATER
        , IAsyncDisposable 
#endif
{
    private static TestContext s_testContext;

    public TestContext TestContext { get; set; }

    public SuiteLifeCycleTestClass_ClassInitializeAndCleanupWithInheritanceBehaviorNone()
    {
        s_testContext.WriteLine("Base Ctor was called");
    }

    [ClassInitialize(InheritanceBehavior.None)]
    public static void ClassInitialize(TestContext testContext)
    {
        s_testContext = testContext;
        s_testContext.WriteLine("Base ClassInitialize.InheritanceBehaviorNone was called");
    }

    [TestInitialize]
    public void TestInitialize()
    {
        TestContext.WriteLine("Base TestInitialize was called");
    }

    [TestMethod]
    public void TestMethod()
    {
        TestContext.WriteLine("Base TestMethod was called");
    }

    [TestCleanup]
    public void TestCleanup()
    {
        TestContext.WriteLine("Base TestCleanup was called");
    }

    public void Dispose()
    {
        TestContext.WriteLine("Base Dispose was called");
    }

#if NET6_0_OR_GREATER
    public ValueTask DisposeAsync()
    {
        TestContext.WriteLine("Base DisposeAsync was called");
        return ValueTask.CompletedTask;
    }
#endif

    [ClassCleanup(InheritanceBehavior.None)]
    public static void ClassCleanup()
    {
        s_testContext.WriteLine("Base ClassCleanup.InheritanceBehaviorNone was called");
    }
}
