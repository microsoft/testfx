// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

using Microsoft.MSTestV2.CLIAutomation;

namespace Microsoft.MSTestV2.Smoke.DiscoveryAndExecutionTests;
public class FSharpTestProjectTests : CLITestBase
{
    private const string TestAssembly = "FSharpTestProject.dll";

    public void TestFSharpTestsWithSpaceAndDotInName()
    {
        // Arrange
        var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : GetAssetFullPath(TestAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath);
        var testResults = RunTests(testCases);

        // Assert
        VerifyE2E.TestsPassed(testResults, "Test method passing with a . in it");
        VerifyE2E.PassedTestCount(testResults, 1);
        VerifyE2E.FailedTestCount(testResults, 0);
    }
}
