// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;

using Microsoft.MSTestV2.CLIAutomation;
using Microsoft.MSTestV2.Smoke.DiscoveryAndExecutionTests;

namespace DiscoveryAndExecutionTests;
public partial class TestId : CLITestBase
{
    private const string LegacyStrategyDll = "TestIdProject.LegacyStrategy.dll";

    #region DataRow tests
    public void TestIdUniqueness_DataRowArray_LegacyStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(LegacyStrategyDll);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowArraysTests");
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        VerifyE2E.FailedTestCount(testResults, 0);
        VerifyE2E.TestsPassed(
            testResults,
            "DataRowArraysTests (0,System.Int32[])",
            "DataRowArraysTests (0,System.Int32[])",
            "DataRowArraysTests (0,System.Int32[])");
        Verify(testResults.All(x => x.TestCase.Id.ToString() == "92814605-fa83-2863-df10-40f669045636"));
    }

    public void TestIdUniqueness_DataRowString_LegacyStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(LegacyStrategyDll);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowStringTests");
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        VerifyE2E.FailedTestCount(testResults, 0);
        VerifyE2E.TestsPassed(
            testResults,
            "DataRowStringTests ()",
            "DataRowStringTests ()",
            "DataRowStringTests ( )",
            "DataRowStringTests (  )");
        Verify(testResults.All(x => x.TestCase.Id.ToString() == "d97bb8fa-5211-c046-b1a6-3003649c01d7"));
    }
    #endregion // DataRow tests

    #region DynamicData tests
    public void TestIdUniqueness_DynamicDataArrays_LegacyStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(LegacyStrategyDll);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DynamicDataArraysTests");
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        VerifyE2E.FailedTestCount(testResults, 0);
        VerifyE2E.TestsPassed(
            testResults,
            "DynamicDataArraysTests (0,System.Int32[])",
            "DynamicDataArraysTests (0,System.Int32[])",
            "DynamicDataArraysTests (0,System.Int32[])");
        Verify(testResults.All(x => x.TestCase.Id.ToString() == "568c8501-26d0-119e-6c67-c697ff07bea7"));
    }

    public void TestIdUniqueness_DynamicDataTuple_LegacyStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(LegacyStrategyDll);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DynamicDataTuplesTests");
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        VerifyE2E.FailedTestCount(testResults, 0);
        VerifyE2E.TestsPassed(
            testResults,
            "DynamicDataTuplesTests ((1, text, True))",
            "DynamicDataTuplesTests ((1, text, False))");
        Verify(testResults.All(x => x.TestCase.Id.ToString() == "983d3193-a5ce-3cfa-74fd-55c628c091e0"));
    }

    public void TestIdUniqueness_DynamicDataGenericCollections_LegacyStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(LegacyStrategyDll);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DynamicDataGenericCollectionsTests");
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        VerifyE2E.FailedTestCount(testResults, 0);
        VerifyE2E.TestsPassed(
            testResults,
            "DynamicDataGenericCollectionsTests (System.Collections.Generic.List`1[System.Int32],System.Collections.Generic.List`1[System.String],System.Collections.Generic.List`1[System.Boolean])",
            "DynamicDataGenericCollectionsTests (System.Collections.Generic.List`1[System.Int32],System.Collections.Generic.List`1[System.String],System.Collections.Generic.List`1[System.Boolean])",
            "DynamicDataGenericCollectionsTests (System.Collections.Generic.List`1[System.Int32],System.Collections.Generic.List`1[System.String],System.Collections.Generic.List`1[System.Boolean])",
            "DynamicDataGenericCollectionsTests (System.Collections.Generic.List`1[System.Int32],System.Collections.Generic.List`1[System.String],System.Collections.Generic.List`1[System.Boolean])");
        Verify(testResults.All(x => x.TestCase.Id.ToString() == "2514f2bc-59b2-052e-b5bc-e9142ec80fa4"));
    }
    #endregion // DynamicData tests

    #region TestDataSource tests
    public void TestIdUniqueness_TestDataSourceArrays_LegacyStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(LegacyStrategyDll);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~TestDataSourceArraysTests");
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        VerifyE2E.FailedTestCount(testResults, 0);
        VerifyE2E.TestsPassed(
            testResults,
            "Custom name",
            "Custom name",
            "Custom name");
        Verify(testResults.All(x => x.TestCase.Id.ToString() == "eb845c3e-042c-943a-db4a-0ecac2f280d4"));
    }

    public void TestIdUniqueness_TestDataSourceTuples_LegacyStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(LegacyStrategyDll);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~TestDataSourceTuplesTests");
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        VerifyE2E.FailedTestCount(testResults, 0);
        VerifyE2E.TestsPassed(
            testResults,
            "Custom name",
            "Custom name");
        Verify(testResults.All(x => x.TestCase.Id.ToString() == "a7c84625-1c62-b6d1-15be-f0034a589b8b"));
    }

    public void TestIdUniqueness_TestDataSourceGenericCollections_LegacyStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(LegacyStrategyDll);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~TestDataSourceGenericCollectionsTests");
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        VerifyE2E.FailedTestCount(testResults, 0);
        VerifyE2E.TestsPassed(
            testResults,
            "Custom name",
            "Custom name",
            "Custom name",
            "Custom name");
        Verify(testResults.All(x => x.TestCase.Id.ToString() == "e6530d79-7dd8-ba6c-4d8b-02edc1448baa"));
    }
    #endregion // TestDataSource tests
}
