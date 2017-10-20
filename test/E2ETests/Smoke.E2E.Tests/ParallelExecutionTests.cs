// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.Smoke.E2ETests
{
    using Microsoft.MSTestV2.CLIAutomation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ParallelExecutionTests : CLITestBase
    {
        private const string ClassParallelTestAssembly = "ParallelClassesTestProject.dll";
        private const string MethodParallelTestAssembly = "ParallelMethodsTestProject.dll";
        private const int TestMethodWaitTimeInMS = 1000;
        private const int OverheadTimeInMS = 2500;

        [TestMethod]
        public void AllMethodsShouldRunInParallel()
        {
            this.InvokeVsTestForExecution(new string[] { MethodParallelTestAssembly });

            // Parallel level of 2
            // There are a total of 6 methods each with a sleep of TestMethodWaitTimeInMS.
            // 5 of them are parallelizable and 1 is not. So this should not exceed 4 * TestMethodWaitTimeInMS seconds + 2.5 seconds overhead.
            this.ValidateTestRunTime((4 * TestMethodWaitTimeInMS) + OverheadTimeInMS);

            this.ValidatePassedTestsContain(
                "ParallelMethodsTestProject.UnitTest1.SimpleTest11",
                "ParallelMethodsTestProject.UnitTest1.SimpleTest13",
                "ParallelMethodsTestProject.UnitTest2.SimpleTest21",
                "ParallelMethodsTestProject.UnitTest2.IsolatedTest");

            this.ValidateFailedTests(
                MethodParallelTestAssembly,
                "ParallelMethodsTestProject.UnitTest1.SimpleTest12",
                "ParallelMethodsTestProject.UnitTest2.SimpleTest22");
        }

        [TestMethod]
        public void AllClassesShouldRunInParallel()
        {
            this.InvokeVsTestForExecution(new string[] { ClassParallelTestAssembly });

            // Parallel level of 2
            // There are a total of 3 classes - C1 (2 tests), C2(3 tests), C3(2 tests) with a sleep of TestMethodWaitTimeInMS.
            // 1 tests in C2 is non-parallelizable. So this should not exceed 5 * TestMethodWaitTimeInMS seconds + 2.5 seconds overhead.
            this.ValidateTestRunTime((5 * TestMethodWaitTimeInMS) + OverheadTimeInMS);

            this.ValidatePassedTestsContain(
                "ParallelClassesTestProject.UnitTest1.SimpleTest11",
                "ParallelClassesTestProject.UnitTest2.SimpleTest21",
                "ParallelClassesTestProject.UnitTest3.SimpleTest31",
                "ParallelClassesTestProject.UnitTest2.IsolatedTest");

            this.ValidateFailedTests(
                MethodParallelTestAssembly,
                "ParallelClassesTestProject.UnitTest1.SimpleTest12",
                "ParallelClassesTestProject.UnitTest2.SimpleTest22",
                "ParallelClassesTestProject.UnitTest3.SimpleTest32");
        }
    }
}
