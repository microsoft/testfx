// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;

using Microsoft.MSTestV2.CLIAutomation;
using Microsoft.MSTestV2.Smoke.DiscoveryAndExecutionTests;

namespace DiscoveryAndExecutionTests;
public class TestId : CLITestBase
{
    private const string LegacyStrategyDll = "TestIdProject.LegacyStrategy.dll";
    private const string DisplayNameStrategyDll = "TestIdProject.DisplayNameStrategy.dll";
    private const string DefaultStrategyDll = "TestIdProject.DefaultStrategy.dll";
    private const string FullyQualifiedTestStrategyDll = "TestIdProject.FullyQualifiedTestStrategy.dll";

    #region Legacy strategy
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
        var expectedTestIds = new[]
        {
            "92814605-fa83-2863-df10-40f669045636",
            "92814605-fa83-2863-df10-40f669045636",
            "92814605-fa83-2863-df10-40f669045636",
        };
        var unionOfIds = testResults.Select(x => x.TestCase.Id.ToString()).Union(expectedTestIds).ToList();
        Verify(unionOfIds.Count == 1);
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
        var expectedTestIds = new[]
        {
            "d97bb8fa-5211-c046-b1a6-3003649c01d7",
            "d97bb8fa-5211-c046-b1a6-3003649c01d7",
            "d97bb8fa-5211-c046-b1a6-3003649c01d7",
            "d97bb8fa-5211-c046-b1a6-3003649c01d7",
        };
        var unionOfIds = testResults.Select(x => x.TestCase.Id.ToString()).Union(expectedTestIds).ToList();
        Verify(unionOfIds.Count == 1);
    }
    #endregion // Legacy strategy

    #region Display name strategy
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
        var expectedTestIds = new[]
        {
            "693508a2-b678-55e9-5e15-9d4999d7dd1f",
            "693508a2-b678-55e9-5e15-9d4999d7dd1f",
            "693508a2-b678-55e9-5e15-9d4999d7dd1f",
        };
        var unionOfIds = testResults.Select(x => x.TestCase.Id.ToString()).Union(expectedTestIds).ToList();
        Verify(unionOfIds.Count == 1);
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
    #endregion // Display name strategy

    #region Default strategy
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
    #endregion // Default strategy

    #region Fully qualified test strategy
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
        var unionOfIds = testResults.Select(x => x.TestCase.Id.ToString()).Union(expectedTestIds).ToList();
        Verify(unionOfIds.Count == expectedTestIds.Length);
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
        var unionOfIds = testResults.Select(x => x.TestCase.Id.ToString()).Union(expectedTestIds).ToList();
        Verify(unionOfIds.Count == expectedTestIds.Length);
    }

    public void TestIdUniqueness_DataRowMultipleArraysTests_FullyQualifiedTestStrategy()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(DisplayNameStrategyDll);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowMultipleArraysTests");
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
        var unionOfIds = testResults.Select(x => x.TestCase.Id.ToString()).Union(expectedTestIds).ToList();
        Verify(unionOfIds.Count == expectedTestIds.Length);
    }
    #endregion // Fully qualified test strategy
}
