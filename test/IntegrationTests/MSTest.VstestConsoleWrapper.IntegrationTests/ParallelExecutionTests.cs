// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions.Execution;

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTest.VstestConsoleWrapper.IntegrationTests;

public class ParallelExecutionTests : CLITestBase
{
    private const string ClassParallelTestAssetName = "ParallelClassesTestProject";
    private const string MethodParallelTestAssetName = "ParallelMethodsTestProject";
    private const string DoNotParallelizeTestAssetName = "DoNotParallelizeTestProject";
    private const int TestMethodWaitTimeInMS = 1000;
    private const int OverheadTimeInMS = 4000;

    public async Task AllMethodsShouldRunInParallel()
    {
        const int maxAttempts = 5;
        for (int i = 0; i <= maxAttempts; i++)
        {
            try
            {
                InvokeVsTestForExecution([MethodParallelTestAssetName]);

                // Parallel level of 2
                // There are a total of 6 methods each with a sleep of TestMethodWaitTimeInMS.
                // 5 of them are parallelizable and 1 is not..
                ValidateTestRunTime((4 * TestMethodWaitTimeInMS) + OverheadTimeInMS);
            }

            // Timer validation sometimes get flacky. So retrying the test if it fails.
            catch (AssertionFailedException ex) when (i != maxAttempts && ex.Message.Contains("Test Run was expected to not exceed"))
            {
                await Task.Delay(2000);
            }
        }

        ValidatePassedTestsContain(
            "ParallelMethodsTestProject.UnitTest1.SimpleTest11",
            "ParallelMethodsTestProject.UnitTest1.SimpleTest13",
            "ParallelMethodsTestProject.UnitTest2.SimpleTest21",
            "ParallelMethodsTestProject.UnitTest2.IsolatedTest");

        ValidateFailedTests(
            "ParallelMethodsTestProject.UnitTest1.SimpleTest12",
            "ParallelMethodsTestProject.UnitTest2.SimpleTest22");
    }

    public void AllClassesShouldRunInParallel()
    {
        InvokeVsTestForExecution([ClassParallelTestAssetName]);

        // Time validation was disabled because of flakiness. It's hard to predict the time taken for this test to run when
        // the test suite start to be large. The time taken depends on the machine and the load on the machine.
        // Parallel level of 2
        // There are a total of 3 classes - C1 (2 tests), C2(3 tests), C3(2 tests) with a sleep of TestMethodWaitTimeInMS.
        // 1 tests in C2 is non-parallelizable. So this should not exceed 5 * TestMethodWaitTimeInMS seconds + 2.5 seconds overhead.
        // ValidateTestRunTime((5 * TestMethodWaitTimeInMS) + OverheadTimeInMS);
        ValidatePassedTestsContain(
            "ParallelClassesTestProject.UnitTest1.SimpleTest11",
            "ParallelClassesTestProject.UnitTest2.SimpleTest21",
            "ParallelClassesTestProject.UnitTest3.SimpleTest31",
            "ParallelClassesTestProject.UnitTest2.IsolatedTest");

        ValidateFailedTests(
            "ParallelClassesTestProject.UnitTest1.SimpleTest12",
            "ParallelClassesTestProject.UnitTest2.SimpleTest22",
            "ParallelClassesTestProject.UnitTest3.SimpleTest32");
    }

    public void NothingShouldRunInParallel()
    {
        const string RunSetting =
            """
            <RunSettings>
              <MSTest>
                <Parallelize>
                  <Workers>4</Workers>
                  <Scope>ClassLevel</Scope>
                </Parallelize>
              </MSTest>
            </RunSettings>
            """;

        InvokeVsTestForExecution([DoNotParallelizeTestAssetName], RunSetting);

        // Time validation was disabled because of flakiness. It's hard to predict the time taken for this test to run when
        // the test suite start to be large. The time taken depends on the machine and the load on the machine.
        // DoNotParallelize set for TestAssetName
        // There are a total of 2 classes - C1 (3 tests), C2 (3 tests) with a sleep of TestMethodWaitTimeInMS.
        // So this should not exceed 5 * TestMethodWaitTimeInMS seconds + 2.5 seconds overhead.
        // ValidateTestRunTime((5 * TestMethodWaitTimeInMS) + OverheadTimeInMS);
        ValidatePassedTestsContain(
            "DoNotParallelizeTestProject.UnitTest1.SimpleTest11",
            "DoNotParallelizeTestProject.UnitTest1.SimpleTest13",
            "DoNotParallelizeTestProject.UnitTest2.SimpleTest21");

        ValidateFailedTestsContain(
            true,
            "DoNotParallelizeTestProject.UnitTest1.SimpleTest12",
            "DoNotParallelizeTestProject.UnitTest2.SimpleTest22");
    }
}
