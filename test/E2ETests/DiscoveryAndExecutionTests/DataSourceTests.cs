// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.MSTestV2.Smoke.DiscoveryAndExecutionTests;

using Microsoft.MSTestV2.CLIAutomation;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.IO;
using System.Linq;

[TestClass]
public class DataSourceTests : CLITestBase
{
    private const string TestAssembly = "DataSourceTestProject.dll";

    // TODO @haplois | @evangelink: This test fails under CI - will be fixed in a future PR
    [Ignore]
    [TestMethod]
    public void ExecuteCsvTestDataSourceTests()
    {
        // Arrange
        var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : this.GetAssetFullPath(TestAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath, "CsvTestMethod");
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        Assert.That.ContainsTestsPassed(testResults,
            "CsvTestMethod (Data Row 0)",
            "CsvTestMethod (Data Row 2)"
        );

        Assert.That.ContainsTestsFailed(testResults,
            "CsvTestMethod (Data Row 1)",
            "CsvTestMethod (Data Row 3)"
        );
    }
}
