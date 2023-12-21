// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SuiteLifeCycleTestProject;

[TestClass]
public class LifeCycleAssemblyInitializeAndCleanup
{
    private static TestContext s_testContext;

    [AssemblyInitialize]
    public static void AssemblyInit(TestContext testContext)
    {
        s_testContext = testContext;
        s_testContext.WriteLine("AssemblyInit was called");
        Console.WriteLine("Console: AssemblyInit was called");
        Trace.WriteLine("Trace: AssemblyInit was called");
        Debug.WriteLine("Debug: AssemblyInit was called");
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
        s_testContext.WriteLine("AssemblyCleanup was called");
        Console.WriteLine("Console: AssemblyCleanup was called");
        Trace.WriteLine("Trace: AssemblyCleanup was called");
        Debug.WriteLine("Debug: AssemblyCleanup was called");
    }
}
