// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTest.IntegrationTests;

public class ClsTests : CLITestBase
{
    private const string TestAssetName = "ClsTestProject";

    public void TestsAreRun()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        var testCases = DiscoverTests(assemblyPath);
        var testResults = RunTests(testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "TestMethod",
            "IntDataRow (10)",
            "StringDataRow (some string)");
    }
}
