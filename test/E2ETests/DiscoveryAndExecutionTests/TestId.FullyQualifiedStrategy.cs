// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;

using FluentAssertions;

using Microsoft.MSTestV2.CLIAutomation;
using Microsoft.MSTestV2.Smoke.DiscoveryAndExecutionTests;

namespace DiscoveryAndExecutionTests;
public partial class TestId : CLITestBase
{
    private const string FullyQualifiedTestStrategyDll = "TestIdProject.FullyQualifiedTestStrategy.dll";

    #region DataRow tests
    public void TestIdUniqueness_DataRowArray_FullyQualifiedTestStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(FullyQualifiedTestStrategyDll);

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
            "96a675c6-c2ec-8d0b-8e58-20b0ebcae172",
            "4777c91d-ed1d-2376-8574-02290eb6ea2a",
            "c467510c-2b09-ada5-dd57-0a063a265324",
        };
        testResults.Select(x => x.TestCase.Id.ToString()).Should().BeEquivalentTo(expectedTestIds);
    }

    public void TestIdUniqueness_DataRowString_FullyQualifiedTestStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(FullyQualifiedTestStrategyDll);

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
            "73224067-c8e7-2d47-7a76-f8def64ecbe4",
            "559cb91c-0737-3246-dc77-8e6a0a3c1976",
            "3c77645c-44b5-532b-6a9d-856885c7d1e0",
            "5ebfafcd-fb75-8d90-79af-92c179b0d786",
        };
        testResults.Select(x => x.TestCase.Id.ToString()).Should().BeEquivalentTo(expectedTestIds);
    }
    #endregion // DataRow tests

    #region DynamicData tests
    public void TestIdUniqueness_DynamicDataArrays_FullyQualifiedTestStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(FullyQualifiedTestStrategyDll);

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
            "7b56d403-105d-7622-cc7b-28351ee013e0",
            "320385c0-b17d-33cf-75f6-674e9a4fa267",
            "c883fef3-9f68-4eff-4ad2-4fee93d7d8ec",
        };
        testResults.Select(x => x.TestCase.Id.ToString()).Should().BeEquivalentTo(expectedTestIds);
    }

    public void TestIdUniqueness_DynamicDataTuple_FullyQualifiedTestStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(FullyQualifiedTestStrategyDll);

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
            "5b908b4b-1983-a6e3-af64-6da9dac50407",
            "ce1edae0-e539-cd40-7d6b-954b8a98d694",
        };
        testResults.Select(x => x.TestCase.Id.ToString()).Should().BeEquivalentTo(expectedTestIds);
    }

    public void TestIdUniqueness_DynamicDataGenericCollections_FullyQualifiedTestStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(FullyQualifiedTestStrategyDll);

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
            "3a069052-7f3c-6cb0-30fd-90c28a3f7bd9",
            "42464c65-2a8c-1a34-4eac-84fd89c19367",
            "821b7b00-b628-9cab-eba3-aa533f8229fe",
            "12d006d1-e3b7-0e29-76e1-19827fc2becf",
        };
        testResults.Select(x => x.TestCase.Id.ToString()).Should().BeEquivalentTo(expectedTestIds);
    }
    #endregion // DynamicData tests

    #region TestDataSource tests
    public void TestIdUniqueness_TestDataSourceArrays_FullyQualifiedTestStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(FullyQualifiedTestStrategyDll);

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
            "65570218-1d9b-bb29-a5c8-7d0d14d8c600",
            "7d7a395e-eed3-6b79-6fd8-805bcca513e4",
            "4f9ce31d-8c45-6807-719c-87cb01ebd6b6",
        };
        testResults.Select(x => x.TestCase.Id.ToString()).Should().BeEquivalentTo(expectedTestIds);
    }

    public void TestIdUniqueness_TestDataSourceTuples_FullyQualifiedTestStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(FullyQualifiedTestStrategyDll);

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
            "aee40850-f6a6-a0dd-3730-4869fc3c6b5f",
            "b03431d7-3486-4301-39d0-2d8d576a5c0c",
        };
        testResults.Select(x => x.TestCase.Id.ToString()).Should().BeEquivalentTo(expectedTestIds);
    }

    public void TestIdUniqueness_TestDataSourceGenericCollections_FullyQualifiedTestStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(FullyQualifiedTestStrategyDll);

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
            "3f235c7d-6ef7-b4e0-fc63-d40111322934",
            "ad3a245c-64f8-c724-0da3-cc5bf8721a63",
            "bd43580d-1e18-662a-70c0-5845a777928d",
            "d98cc584-2ff2-543e-f4f2-1d8ec9d8c54c",
        };
        testResults.Select(x => x.TestCase.Id.ToString()).Should().BeEquivalentTo(expectedTestIds);
    }
    #endregion // TestDataSource tests
}
