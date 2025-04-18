﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTest.IntegrationTests;

public class DataRowTests : CLITestBase
{
    private const string TestAssetName = "DataRowTestProject";

    public async Task ExecuteOnlyDerivedClassDataRowsWhenBothBaseAndDerivedClassHasDataRows_SimpleDataRows()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "TestCategory~DataRowSimple");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = await RunTestsAsync(testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "DataRowTestMethod (\"BaseString1\")",
            "DataRowTestMethod (\"BaseString2\")",
            "DataRowTestMethod (\"BaseString3\")",
            "DataRowTestMethod (\"DerivedString1\")",
            "DataRowTestMethod (\"DerivedString2\")");
    }

    public async Task ExecuteOnlyDerivedClassDataRowsWhenItOverridesBaseClassDataRows_SimpleDataRows()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DerivedClass&TestCategory~DataRowSimple");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = await RunTestsAsync(testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "DataRowTestMethod (\"DerivedString1\")",
            "DataRowTestMethod (\"DerivedString2\")");
    }

    public async Task DataRowsExecuteWithRequiredAndOptionalParameters()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "TestCategory~DataRowSomeOptional");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = await RunTestsAsync(testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "DataRowTestMethodWithSomeOptionalParameters (123)",
            "DataRowTestMethodWithSomeOptionalParameters (123,\"DerivedOptionalString1\")",
            "DataRowTestMethodWithSomeOptionalParameters (123,\"DerivedOptionalString2\",\"DerivedOptionalString3\")");
    }

    public async Task DataRowsExecuteWithParamsArrayParameter()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "TestCategory~DataRowParamsArgument");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = await RunTestsAsync(testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "DataRowTestMethodWithParamsParameters (2)",
            "DataRowTestMethodWithParamsParameters (2,\"DerivedSingleParamsArg\")",
            "DataRowTestMethodWithParamsParameters (2,\"DerivedParamsArg1\",\"DerivedParamsArg2\")",
            "DataRowTestMethodWithParamsParameters (2,\"DerivedParamsArg1\",\"DerivedParamsArg2\",\"DerivedParamsArg3\")");
    }

    public async Task DataRowsFailWhenInvalidArgumentsProvided()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowTests_Regular&TestCategory~DataRowOptionalInvalidArguments");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = await RunTestsAsync(testCases);

        // Assert
        VerifyE2E.TestsFailed(
            testResults,
            "DataRowTestMethodFailsWithInvalidArguments ()",
            "DataRowTestMethodFailsWithInvalidArguments (2)",
            "DataRowTestMethodFailsWithInvalidArguments (2,\"DerivedRequiredArgument\",\"DerivedOptionalArgument\",\"DerivedExtraArgument\")");
    }

    public async Task DataRowsShouldSerializeDoublesProperly()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowTests_Regular.DataRowTestDouble");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = await RunTestsAsync(testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "DataRowTestDouble (10.01,20.01)",
            "DataRowTestDouble (10.02,20.02)");
    }

    public async Task DataRowsShouldSerializeMixedTypesProperly()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowTests_DerivedClass.DataRowTestMixed");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = await RunTestsAsync(testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "DataRowTestMixed (10,10,10,10,10,10,10,\"10\")");
    }

    public async Task DataRowsShouldSerializeEnumsProperly()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowTests_DerivedClass.DataRowEnums");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = await RunTestsAsync(testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "DataRowEnums (null)",
            "DataRowEnums (Alfa)",
            "DataRowEnums (Beta)",
            "DataRowEnums (Gamma)");
    }

    public async Task DataRowsShouldHandleNonSerializableValues()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowTests_DerivedClass.DataRowNonSerializable");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = await RunTestsAsync(testCases);

        // Assert
        VerifyE2E.TestsDiscovered(
            testCases,
            "DataRowNonSerializable");

        VerifyE2E.TestsPassed(
            testResults,
            "DataRowNonSerializable (System.String)",
            "DataRowNonSerializable (System.Int32)",
            "DataRowNonSerializable (DataRowTestProject.DataRowTests_DerivedClass)");
    }

    public async Task ExecuteDataRowTests_Enums()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowTests_Enums");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = await RunTestsAsync(testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "DataRowEnums_SByte (Alfa)",
            "DataRowEnums_SByte (Beta)",
            "DataRowEnums_SByte (Gamma)",
            "DataRowEnums_Byte (Alfa)",
            "DataRowEnums_Byte (Beta)",
            "DataRowEnums_Byte (Gamma)",
            "DataRowEnums_Short (Alfa)",
            "DataRowEnums_Short (Beta)",
            "DataRowEnums_Short (Gamma)",
            "DataRowEnums_UShort (Alfa)",
            "DataRowEnums_UShort (Beta)",
            "DataRowEnums_UShort (Gamma)",
            "DataRowEnums_Int (Alfa)",
            "DataRowEnums_Int (Beta)",
            "DataRowEnums_Int (Gamma)",
            "DataRowEnums_UInt (Alfa)",
            "DataRowEnums_UInt (Beta)",
            "DataRowEnums_UInt (Gamma)",
            "DataRowEnum_Long (Alfa)",
            "DataRowEnum_Long (Beta)",
            "DataRowEnum_Long (Gamma)",
            "DataRowEnum_ULong (Alfa)",
            "DataRowEnum_ULong (Beta)",
            "DataRowEnum_ULong (Gamma)",
            "DataRowEnums_Nullable_SByte (null)",
            "DataRowEnums_Nullable_SByte (Alfa)",
            "DataRowEnums_Nullable_SByte (Beta)",
            "DataRowEnums_Nullable_SByte (Gamma)",
            "DataRowEnums_Nullable_Byte (null)",
            "DataRowEnums_Nullable_Byte (Alfa)",
            "DataRowEnums_Nullable_Byte (Beta)",
            "DataRowEnums_Nullable_Byte (Gamma)",
            "DataRowEnums_Nullable_Short (null)",
            "DataRowEnums_Nullable_Short (Alfa)",
            "DataRowEnums_Nullable_Short (Beta)",
            "DataRowEnums_Nullable_Short (Gamma)",
            "DataRowEnums_Nullable_UShort (null)",
            "DataRowEnums_Nullable_UShort (Alfa)",
            "DataRowEnums_Nullable_UShort (Beta)",
            "DataRowEnums_Nullable_UShort (Gamma)",
            "DataRowEnums_Nullable_Int (null)",
            "DataRowEnums_Nullable_Int (Alfa)",
            "DataRowEnums_Nullable_Int (Beta)",
            "DataRowEnums_Nullable_Int (Gamma)",
            "DataRowEnums_Nullable_UInt (null)",
            "DataRowEnums_Nullable_UInt (Alfa)",
            "DataRowEnums_Nullable_UInt (Beta)",
            "DataRowEnums_Nullable_UInt (Gamma)",
            "DataRowEnums_Nullable_Long (null)",
            "DataRowEnums_Nullable_Long (Alfa)",
            "DataRowEnums_Nullable_Long (Beta)",
            "DataRowEnums_Nullable_Long (Gamma)",
            "DataRowEnums_Nullable_ULong (null)",
            "DataRowEnums_Nullable_ULong (Alfa)",
            "DataRowEnums_Nullable_ULong (Beta)",
            "DataRowEnums_Nullable_ULong (Gamma)",
            "DataRowEnums_MixedTypes_Byte (Alfa,True,1)",
            "DataRowEnums_MixedTypes_Byte (Beta,False,2)",
            "DataRowEnums_MixedTypes_Byte (Gamma,True,3)");

        VerifyE2E.FailedTestCount(testResults, 0);
    }

    public async Task ExecuteDataRowTests_NonSerializablePaths()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowTests_NonSerializablePaths");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = await RunTestsAsync(testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "DataRowNonSerializable (System.String)",
            "DataRowNonSerializable (System.Int32)",
            "DataRowNonSerializable (DataRowTestProject.DataRowTests_Enums)");
        VerifyE2E.FailedTestCount(testResults, 0);
    }

    public async Task ExecuteDataRowTests_Regular()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowTests_Regular");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = await RunTestsAsync(testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "DataRow1 (10)",
            "DataRow1 (20)",
            "DataRow1 (30)",
            "DataRow1 (40)",
            "DataRow2 (10,\"String parameter\",True,False)",
            "DataRow2 (20,\"String parameter\",True,False)",
            "DataRow2 (30,\"String parameter\",True,False)",
            "DataRow2 (40,\"String parameter\",True,False)",
            "DataRowTestDouble (10.01,20.01)",
            "DataRowTestDouble (10.02,20.02)",
            "DataRowTestMixed (1,10,10,10,10,10,10,10,\"10\")",
            "DataRowTestMixed (2,10,10,10,10,10,10,10,\"10\")",
            "DataRowTestMixed (3,10,10,10,10,10,10,10,\"10\")",
            "DataRowTestMixed (4,10,10,10,10,10,10,10,\"10\")",
            "NullValueInData (\"john.doe@example.com\",\"abc123\",null)",
            "NullValueInData (\"john.doe@example.com\",\"abc123\",\"/unit/test\")",
            "NullValue (null)",
            "OneStringArray ([\"\"])",
            "TwoStringArrays ([\"\"],[\"1.4\",\"message\"])",
            "OneObjectArray ([\"\",1])",
            "TwoObjectArrays ([\"\",1],[3])",
            "ThreeObjectArrays ([1],[2],[3])",
            "FourObjectArrays ([1],[2],[3],[4])",
            "FiveObjectArrays ([1],[2],[3],[4],[5])",
            "SixObjectArrays ([1],[2],[3],[4],[5],[6])",
            "SevenObjectArrays ([1],[2],[3],[4],[5],[6],[7])",
            "EightObjectArrays ([1],[2],[3],[4],[5],[6],[7],[8])",
            "NineObjectArrays ([1],[2],[3],[4],[5],[6],[7],[8],[9])",
            "TenObjectArrays ([1],[2],[3],[4],[5],[6],[7],[8],[9],[10])",
            "ElevenObjectArrays ([1],[2],[3],[4],[5],[6],[7],[8],[9],[10],[11])",
            "TwelveObjectArrays ([1],[2],[3],[4],[5],[6],[7],[8],[9],[10],[11],[12])",
            "ThirteenObjectArrays ([1],[2],[3],[4],[5],[6],[7],[8],[9],[10],[11],[12],[13])",
            "FourteenObjectArrays ([1],[2],[3],[4],[5],[6],[7],[8],[9],[10],[11],[12],[13],[14])",
            "FifteenObjectArrays ([1],[2],[3],[4],[5],[6],[7],[8],[9],[10],[11],[12],[13],[14],[15])",
            "SixteenObjectArrays ([1],[2],[3],[4],[5],[6],[7],[8],[9],[10],[11],[12],[13],[14],[15],[16])",
            "MultipleIntegersWrappedWithParams (1,2,3,4,5)",
            "MethodWithOverload (1)",
            "MethodWithOverload (2)",
            "MethodWithOverload (\"a\")",
            "MethodWithOverload (\"b\")",
            "NullValueOnObjectArray ([null])");

        VerifyE2E.TestsFailed(
            testResults,
            "DataRowTestMethodFailsWithInvalidArguments ()",
            "DataRowTestMethodFailsWithInvalidArguments (2)",
            "DataRowTestMethodFailsWithInvalidArguments (2,\"DerivedRequiredArgument\",\"DerivedOptionalArgument\",\"DerivedExtraArgument\")");
    }

    public async Task GetDisplayName_AfterOverriding_GetsTheNewDisplayName()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "TestCategory~OverriddenGetDisplayName");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = await RunTestsAsync(testCases);

        VerifyE2E.TestsPassed(
            testResults,
            "Overridden DisplayName");
    }

    public async Task ParameterizedTestsWithTestMethodSettingDisplayName_DataIsPrefixWithDisplayName()
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "TestCategory~OverriddenTestMethodDisplayNameForParameterizedTest");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = await RunTestsAsync(testCases);

        VerifyE2E.TestsPassed(
            testResults,
            "SomeCustomDisplayName2 (\"SomeData\")",
            "SomeCustomDisplayName3 (\"SomeData\")");
    }
}
