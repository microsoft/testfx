// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SuiteLifeCycleTestProject;

[TestClass]
public class SuiteLifeCycleTestClass_AssemblyInitializeAndCleanup
{
    private static TestContext s_testContext;

    [AssemblyInitialize]
    public static void AssemblyInit(TestContext testContext)
    {
        s_testContext = testContext;
        s_testContext.WriteLine("AssemblyInit was called");
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
        s_testContext.WriteLine("AssemblyCleanup was called");
    }
}
