// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;

using Microsoft.MSTestV2.CLIAutomation;
using Microsoft.MSTestV2.Smoke.DiscoveryAndExecutionTests;

namespace DiscoveryAndExecutionTests;
public partial class TestId : CLITestBase
{
    private const string DisplayNameStrategyDll = "TestIdProject.DisplayNameStrategy.dll";

    #region DataRow tests
    public void TestIdUniqueness_DataRowArray_DisplayNameStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(DisplayNameStrategyDll);

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
        Verify(testResults.All(x => x.TestCase.Id.ToString() == "693508a2-b678-55e9-5e15-9d4999d7dd1f"));
    }

    public void TestIdUniqueness_DataRowString_DisplayNameStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(DisplayNameStrategyDll);

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
        var expectedTestIds = new[]
        {
            "6efe619f-4f82-57d2-201b-299111dd78a3",
            "d8bbcca2-64ee-26ff-23d3-5a715eabb814",
            "3bf86c48-252b-edc3-290e-67867e1e1c2b",
            "d8bbcca2-64ee-26ff-23d3-5a715eabb814",
        };
        var unionOfIds = testResults.Select(x => x.TestCase.Id.ToString()).Union(expectedTestIds).ToList();
        Verify(unionOfIds.Count == 3);
    }
    #endregion // DataRow tests

    #region DynamicData tests
    public void TestIdUniqueness_DynamicDataArrays_DisplayNameStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(DisplayNameStrategyDll);

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
        Verify(testResults.All(x => x.TestCase.Id.ToString() == "f082cd61-c63f-c62f-8ee4-25521f65b4cc"));
    }

    public void TestIdUniqueness_DynamicDataTuple_DisplayNameStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(DisplayNameStrategyDll);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DynamicDataTuplesTests");
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        VerifyE2E.FailedTestCount(testResults, 0);
        VerifyE2E.TestsPassed(
            testResults,
            "DynamicDataTuplesTests ((1, text, True))",
            "DynamicDataTuplesTests ((1, text, False))");
        var expectedTestIds = new[]
        {
            "f67bc110-9e34-6035-3f9b-967dcf628fc4",
            "69f33ebc-b11c-76c7-d029-73b73ed6f9aa",
        };
        var unionOfIds = testResults.Select(x => x.TestCase.Id.ToString()).Union(expectedTestIds).ToList();
        Verify(unionOfIds.Count == expectedTestIds.Length);
    }

    public void TestIdUniqueness_DynamicDataGenericCollections_DisplayNameStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(DisplayNameStrategyDll);

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
        Verify(testResults.All(x => x.TestCase.Id.ToString() == "362ad6f0-9c6a-0787-77a5-3823ba94355b"));
    }
    #endregion // DynamicData tests

    #region TestDataSource tests
    public void TestIdUniqueness_TestDataSourceArrays_DisplayNameStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(DisplayNameStrategyDll);

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
        Verify(testResults.All(x => x.TestCase.Id.ToString() == "8921bd6a-c547-6b55-d551-faa731013c63"));
    }

    public void TestIdUniqueness_TestDataSourceTuples_DisplayNameStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(DisplayNameStrategyDll);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~TestDataSourceTuplesTests");
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        VerifyE2E.FailedTestCount(testResults, 0);
        VerifyE2E.TestsPassed(
            testResults,
            "Custom name",
            "Custom name");
        Verify(testResults.All(x => x.TestCase.Id.ToString() == "090538bc-78df-8243-f445-48496976c89e"));
    }

    public void TestIdUniqueness_TestDataSourceGenericCollections_DisplayNameStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(DisplayNameStrategyDll);

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
        Verify(testResults.All(x => x.TestCase.Id.ToString() == "a6f8a841-425a-a767-84a1-3ffb8854a21d"));
    }
    #endregion // TestDataSource tests
}
