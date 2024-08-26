// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTest.IntegrationTests;

[TestClass]
public class FSharpTestProjectTests : CLITestBase
{
    private const string TestAssetName = "FSharpTestProject";

    [TestMethod]
    public void TestFSharpTestsWithSpaceAndDotInName()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName, targetFramework: "net472");

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath);
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = RunTests(testCases);

        // Assert
        VerifyE2E.TestsPassed(testResults, "Test method passing with a . in it");
        VerifyE2E.PassedTestCount(testResults, 1);
        VerifyE2E.FailedTestCount(testResults, 0);
    }
}
