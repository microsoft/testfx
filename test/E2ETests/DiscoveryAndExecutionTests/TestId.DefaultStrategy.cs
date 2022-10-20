// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;

using Microsoft.MSTestV2.CLIAutomation;
using Microsoft.MSTestV2.Smoke.DiscoveryAndExecutionTests;

namespace DiscoveryAndExecutionTests;
public partial class TestId : CLITestBase
{
    private const string DefaultStrategyDll = "TestIdProject.DefaultStrategy.dll";

    #region DataRow tests
    public void TestIdUniqueness_DataRowArray_DefaultStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(DefaultStrategyDll);

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
        var expectedTestIds = new[]
        {
            "7cc86263-3acf-95bd-bd3e-de5245f19d06",
            "f22ba36c-acdc-08f7-8ced-5fcdb59192f4",
            "e0566ef4-a881-a2bd-1309-cca44ae26bae",
        };
        var unionOfIds = testResults.Select(x => x.TestCase.Id.ToString()).Union(expectedTestIds).ToList();
        Verify(unionOfIds.Count == expectedTestIds.Length);
    }

    public void TestIdUniqueness_DataRowString_DefaultStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(DefaultStrategyDll);

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
            "2b39a68d-fbd9-e047-f4cb-f46c0ba7a69c",
            "406739d9-a57c-323a-f896-38c409e0202c",
            "277aa910-c697-6a9d-b2cf-e9d515809cfb",
            "456a489d-7057-eb18-266f-1b4a69ceb5d4",
        };
        var unionOfIds = testResults.Select(x => x.TestCase.Id.ToString()).Union(expectedTestIds).ToList();
        Verify(unionOfIds.Count == expectedTestIds.Length);
    }
    #endregion // DataRow tests

    #region DynamicData tests
    public void TestIdUniqueness_DynamicDataArrays_DefaultStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(DefaultStrategyDll);

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
        var expectedTestIds = new[]
        {
            "22243efb-20f6-c4b9-2dd1-2bdc375b8fd3",
            "344ecd8b-ff90-903b-b98b-81aa782c3d78",
            "420603ea-ca59-83ad-1cc4-c51735dc81bf",
        };
        var unionOfIds = testResults.Select(x => x.TestCase.Id.ToString()).Union(expectedTestIds).ToList();
        Verify(unionOfIds.Count == expectedTestIds.Length);
    }

    public void TestIdUniqueness_DynamicDataTuple_DefaultStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(DefaultStrategyDll);

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
            "4d641a07-9a0e-24ce-23d9-ec1baaf49e3d",
            "bd5bcfbb-f305-cd1c-205e-93ffeeb2349b",
        };
        var unionOfIds = testResults.Select(x => x.TestCase.Id.ToString()).Union(expectedTestIds).ToList();
        Verify(unionOfIds.Count == expectedTestIds.Length);
    }

    public void TestIdUniqueness_DynamicDataGenericCollections_DefaultStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(DefaultStrategyDll);

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
        var expectedTestIds = new[]
        {
            "94bf1f94-8944-0e70-1471-0e17b22bee14",
            "fd2665ec-f445-9b52-90d5-21ca3eb5fe8f",
            "710bbb17-4439-5225-45d1-e21b8f880c9f",
            "a072b06f-8c98-9c13-242a-c9a1181a2dfa",
        };
        var unionOfIds = testResults.Select(x => x.TestCase.Id.ToString()).Union(expectedTestIds).ToList();
        Verify(unionOfIds.Count == expectedTestIds.Length);
    }
    #endregion // DynamicData tests

    #region TestDataSource tests
    public void TestIdUniqueness_TestDataSourceArrays_DefaultStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(DefaultStrategyDll);

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
        var expectedTestIds = new[]
        {
            "1c457ff0-9d71-3843-f5b4-4aee9db56207",
            "836ea2ff-7fb9-98cf-d904-c5f608507310",
            "abe32ba2-25e6-60b5-4215-243c18218be8",
        };
        var unionOfIds = testResults.Select(x => x.TestCase.Id.ToString()).Union(expectedTestIds).ToList();
        Verify(unionOfIds.Count == expectedTestIds.Length);
    }

    public void TestIdUniqueness_TestDataSourceTuples_DefaultStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(DefaultStrategyDll);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~TestDataSourceTuplesTests");
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        VerifyE2E.FailedTestCount(testResults, 0);
        VerifyE2E.TestsPassed(
            testResults,
            "Custom name",
            "Custom name");
        var expectedTestIds = new[]
        {
            "5c7232dc-de95-3a0a-0fb4-17383f53f862",
            "8934f095-bfa1-8675-e2ea-43043dda7a11",
        };
        var unionOfIds = testResults.Select(x => x.TestCase.Id.ToString()).Union(expectedTestIds).ToList();
        Verify(unionOfIds.Count == expectedTestIds.Length);
    }

    public void TestIdUniqueness_TestDataSourceGenericCollections_DefaultStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(DefaultStrategyDll);

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
        var expectedTestIds = new[]
        {
            "5edd61e0-eb96-0cd6-aa0f-30b70866a5b7",
            "4dbadc26-5cfc-f34b-2544-aacd93ec13cb",
            "095f9807-6fd5-8aea-a040-9ae4b849fdcd",
            "6e598202-b1a4-6b27-ac0d-e9a66ca49637",
        };
        var unionOfIds = testResults.Select(x => x.TestCase.Id.ToString()).Union(expectedTestIds).ToList();
        Verify(unionOfIds.Count == expectedTestIds.Length);
    }
    #endregion // TestDataSource tests
}
