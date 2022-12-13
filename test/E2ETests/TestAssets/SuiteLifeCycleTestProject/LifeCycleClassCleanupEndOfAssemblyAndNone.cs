// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
#if NET6_0_OR_GREATER
using System.Threading.Tasks;
#endif

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SuiteLifeCycleTestProject;

[TestClass]
public class LifeCycleClassCleanupEndOfAssemblyAndNone : IDisposable
#if NET6_0_OR_GREATER
        , IAsyncDisposable 
#endif
{
    private static TestContext s_testContext;

    public TestContext TestContext { get; set; }

    public LifeCycleClassCleanupEndOfAssemblyAndNone()
    {
        s_testContext.WriteLine("LifeCycleClassCleanupEndOfAssemblyAndNone.ctor was called");
    }

    [ClassInitialize]
    public static void ClassInitialize(TestContext testContext)
    {
        s_testContext = testContext;
        s_testContext.WriteLine("LifeCycleClassCleanupEndOfAssemblyAndNone.ClassInitialize was called");
    }

    [TestInitialize]
    public void TestInitialize()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfAssemblyAndNone.TestInitialize was called");
    }

    [TestMethod]
    public void TestMethod()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfAssemblyAndNone.TestMethod was called");
    }

    [TestCleanup]
    public void TestCleanup()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfAssemblyAndNone.TestCleanup was called");
    }

    public void Dispose()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfAssemblyAndNone.Dispose was called");
    }

#if NET6_0_OR_GREATER
    public ValueTask DisposeAsync()
    {
        TestContext.WriteLine("LifeCycleClassCleanupEndOfAssemblyAndNone.DisposeAsync was called");
        return ValueTask.CompletedTask;
    }
#endif

    [ClassCleanup(ClassCleanupBehavior.EndOfAssembly)]
    public static void ClassCleanup()
    {
        s_testContext.WriteLine("LifeCycleClassCleanupEndOfAssemblyAndNone.ClassCleanup was called");
    }
}
