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
public class SuiteLifeCycleTestClass_ClassInitializeAndCleanupWithInheritanceBehaviorBeforeEachDerivedClass : IDisposable
#if NET6_0_OR_GREATER
        , IAsyncDisposable 
#endif
{
    private static TestContext s_testContext;
    protected static string s_messagePrefix = "";

    public TestContext TestContext { get; set; }

    public SuiteLifeCycleTestClass_ClassInitializeAndCleanupWithInheritanceBehaviorBeforeEachDerivedClass()
    {
        s_testContext.WriteLine($"{s_messagePrefix}ctor was called");
    }

    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void ClassInitialize(TestContext testContext)
    {
        s_testContext = testContext;
        s_testContext.WriteLine($"{s_messagePrefix}ClassInitialize.InheritanceBehaviorBeforeEachDerivedClass was called");
    }

    [TestInitialize]
    public void TestInitialize()
    {
        TestContext.WriteLine($"{s_messagePrefix}TestInitialize was called");
    }

    [TestMethod]
    public void TestMethod()
    {
        TestContext.WriteLine($"{s_messagePrefix}TestMethod was called");
    }

    [TestCleanup]
    public void TestCleanup()
    {
        TestContext.WriteLine($"{s_messagePrefix}TestCleanup was called");
    }

    public void Dispose()
    {
        TestContext.WriteLine($"{s_messagePrefix}Dispose was called");
    }

#if NET6_0_OR_GREATER
    public ValueTask DisposeAsync()
    {
        TestContext.WriteLine($"{s_messagePrefix}DisposeAsync was called");
        return ValueTask.CompletedTask;
    }
#endif

    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void ClassCleanup()
    {
        s_testContext.WriteLine($"{s_messagePrefix}ClassCleanup.InheritanceBehaviorBeforeEachDerivedClass was called");
    }
}
