// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTest.IntegrationTests;

public class ClsTests : CLITestBase
{
    private const string TestAssetName = "ClsTestProject";

    // This test in itself is not so important. What matters is that the asset gets build. If we regress and start having
    // the [DataRow] attribute no longer CLS compliant, the build will raise a warning in VS (and the build will fail in CI).
    public void TestsAreRun()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath);
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = RunTests(testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "TestMethod",
            "IntDataRow (10)",
            "StringDataRow (some string)",
            "StringDataRow2 (some string)",
            "StringDataRow2 (some other string)");
    }
}
