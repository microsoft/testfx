// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTest.IntegrationTests;

public partial class TestId : CLITestBase
{
    private const string DisplayNameStrategyDll = "TestIdProject.DisplayNameStrategy";

    public void TestIdUniqueness_DataRowArray_DisplayNameStrategy()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(DisplayNameStrategyDll);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowArraysTests");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = RunTests(testCases);

        // Assert
        VerifyE2E.FailedTestCount(testResults, 0);
        VerifyE2E.TestsPassed(
            testResults,
            "DataRowArraysTests (0,System.Int32[])",
            "DataRowArraysTests (0,System.Int32[])",
            "DataRowArraysTests (0,System.Int32[])");

        // We cannot assert the expected ID as it is path dependent
        testResults.Select(x => x.TestCase.Id.ToString()).Distinct().Should().ContainSingle();
    }

    public void TestIdUniqueness_DataRowString_DisplayNameStrategy()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(DisplayNameStrategyDll);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowStringTests");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = RunTests(testCases);

        // Assert
        VerifyE2E.FailedTestCount(testResults, 0);
        VerifyE2E.TestsPassed(
            testResults,
            "DataRowStringTests ()",
            "DataRowStringTests ()",
            "DataRowStringTests ( )",
            "DataRowStringTests (  )");

        // We cannot assert the expected ID as it is path dependent.
        // First two display names are equals so we have the same ID for them
        testResults.Select(x => x.TestCase.Id.ToString()).Distinct().Should().HaveCount(3);
    }

    public void TestIdUniqueness_DynamicDataArrays_DisplayNameStrategy()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(DisplayNameStrategyDll);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DynamicDataArraysTests");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = RunTests(testCases);

        // Assert
        VerifyE2E.FailedTestCount(testResults, 0);
        VerifyE2E.TestsPassed(
            testResults,
            "DynamicDataArraysTests (0,System.Int32[])",
            "DynamicDataArraysTests (0,System.Int32[])",
            "DynamicDataArraysTests (0,System.Int32[])");

        // We cannot assert the expected ID as it is path dependent
        testResults.Select(x => x.TestCase.Id.ToString()).Distinct().Should().ContainSingle();
    }

    public void TestIdUniqueness_DynamicDataTuple_DisplayNameStrategy()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(DisplayNameStrategyDll);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DynamicDataTuplesTests");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = RunTests(testCases);

        // Assert
        VerifyE2E.FailedTestCount(testResults, 0);
        VerifyE2E.TestsPassed(
            testResults,
            "DynamicDataTuplesTests ((1, text, True))",
            "DynamicDataTuplesTests ((1, text, False))");

        // We cannot assert the expected ID as it is path dependent
        testResults.Select(x => x.TestCase.Id.ToString()).Should().OnlyHaveUniqueItems();
    }

    public void TestIdUniqueness_DynamicDataGenericCollections_DisplayNameStrategy()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(DisplayNameStrategyDll);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DynamicDataGenericCollectionsTests");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = RunTests(testCases);

        // Assert
        VerifyE2E.FailedTestCount(testResults, 0);
        VerifyE2E.TestsPassed(
            testResults,
            "DynamicDataGenericCollectionsTests (System.Collections.Generic.List`1[System.Int32],System.Collections.Generic.List`1[System.String],System.Collections.Generic.List`1[System.Boolean])",
            "DynamicDataGenericCollectionsTests (System.Collections.Generic.List`1[System.Int32],System.Collections.Generic.List`1[System.String],System.Collections.Generic.List`1[System.Boolean])",
            "DynamicDataGenericCollectionsTests (System.Collections.Generic.List`1[System.Int32],System.Collections.Generic.List`1[System.String],System.Collections.Generic.List`1[System.Boolean])",
            "DynamicDataGenericCollectionsTests (System.Collections.Generic.List`1[System.Int32],System.Collections.Generic.List`1[System.String],System.Collections.Generic.List`1[System.Boolean])");

        // We cannot assert the expected ID as it is path dependent
        testResults.Select(x => x.TestCase.Id.ToString()).Distinct().Should().ContainSingle();
    }

    public void TestIdUniqueness_TestDataSourceArrays_DisplayNameStrategy()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(DisplayNameStrategyDll);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~TestDataSourceArraysTests");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = RunTests(testCases);

        // Assert
        VerifyE2E.FailedTestCount(testResults, 0);
        VerifyE2E.TestsPassed(
            testResults,
            "Custom name",
            "Custom name",
            "Custom name");

        // We cannot assert the expected ID as it is path dependent
        testResults.Select(x => x.TestCase.Id.ToString()).Distinct().Should().ContainSingle();
    }

    public void TestIdUniqueness_TestDataSourceTuples_DisplayNameStrategy()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(DisplayNameStrategyDll);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~TestDataSourceTuplesTests");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = RunTests(testCases);

        // Assert
        VerifyE2E.FailedTestCount(testResults, 0);
        VerifyE2E.TestsPassed(
            testResults,
            "Custom name",
            "Custom name");

        // We cannot assert the expected ID as it is path dependent
        testResults.Select(x => x.TestCase.Id.ToString()).Distinct().Should().ContainSingle();
    }

    public void TestIdUniqueness_TestDataSourceGenericCollections_DisplayNameStrategy()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(DisplayNameStrategyDll);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~TestDataSourceGenericCollectionsTests");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = RunTests(testCases);

        // Assert
        VerifyE2E.FailedTestCount(testResults, 0);
        VerifyE2E.TestsPassed(
            testResults,
            "Custom name",
            "Custom name",
            "Custom name",
            "Custom name");

        // We cannot assert the expected ID as it is path dependent
        testResults.Select(x => x.TestCase.Id.ToString()).Distinct().Should().ContainSingle();
    }
}
