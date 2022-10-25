﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;

using FluentAssertions;

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

        // We cannot assert the expected guid as it is path dependent
        testResults.Select(x => x.TestCase.Id.ToString()).Distinct().Should().ContainSingle();
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

        // We cannot assert the expected guid as it is path dependent.
        // First two display names are equals so we have the same ID for them
        testResults.Select(x => x.TestCase.Id.ToString()).Distinct().Should().HaveCount(3);
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

        // We cannot assert the expected guid as it is path dependent
        testResults.Select(x => x.TestCase.Id.ToString()).Distinct().Should().ContainSingle();
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

        // We cannot assert the expected guid as it is path dependent
        testResults.Select(x => x.TestCase.Id.ToString()).Should().OnlyHaveUniqueItems();
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

        // We cannot assert the expected guid as it is path dependent
        testResults.Select(x => x.TestCase.Id.ToString()).Distinct().Should().ContainSingle();
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

        // We cannot assert the expected guid as it is path dependent
        testResults.Select(x => x.TestCase.Id.ToString()).Distinct().Should().ContainSingle();
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

        // We cannot assert the expected guid as it is path dependent
        testResults.Select(x => x.TestCase.Id.ToString()).Distinct().Should().ContainSingle();
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

        // We cannot assert the expected guid as it is path dependent
        testResults.Select(x => x.TestCase.Id.ToString()).Distinct().Should().ContainSingle();
    }
    #endregion // TestDataSource tests
}
