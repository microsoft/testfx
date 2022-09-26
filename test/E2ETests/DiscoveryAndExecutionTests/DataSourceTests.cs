// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.MSTestV2.Smoke.DiscoveryAndExecutionTests;

using Microsoft.MSTestV2.CLIAutomation;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.IO;
using System.Linq;

public class DataSourceTests : CLITestBase
{
    private const string TestAssembly = "DataSourceTestProject.dll";

    // TODO @haplois | @evangelink: This test fails under CI - will be fixed in a future PR (Marked as private to ignore the test)
    private void ExecuteCsvTestDataSourceTests()
    {
        // Arrange
        var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : GetAssetFullPath(TestAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath, "CsvTestMethod");
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        AssertionExtensions.ContainsTestsPassed(testResults,
            "CsvTestMethod (Data Row 0)",
            "CsvTestMethod (Data Row 2)"
        );
        AssertionExtensions.ContainsTestsFailed(testResults,
            "CsvTestMethod (Data Row 1)",
            "CsvTestMethod (Data Row 3)"
        );
    }
}
