// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.MSTestV2.Smoke.DiscoveryAndExecutionTests;

using Microsoft.MSTestV2.CLIAutomation;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.IO;

public class FSharpTestProjectTests : CLITestBase
{
    private const string TestAssembly = "FSharpTestProject.dll";

    public void TestFSharpTestsWithSpaceAndDotInName()
    {
        // Arrange
        var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : GetAssetFullPath(TestAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath);
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        VerifyE2E.TestsPassed(testResults, "Test method passing with a . in it");
        VerifyE2E.PassedTestCount(testResults, 1);
        VerifyE2E.FailedTestCount(testResults, 0);
    }
}
