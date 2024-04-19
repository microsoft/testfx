// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTest.IntegrationTests;

public class DataRowTests : CLITestBase
{
    private const string TestAssetName = "DataRowTestProject";

    public void ExecuteOnlyDerivedClassDataRowsWhenBothBaseAndDerivedClassHasDataRows_SimpleDataRows()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        var testCases = DiscoverTests(assemblyPath, "TestCategory~DataRowSimple");
        var testResults = RunTests(testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "DataRowTestMethod (BaseString1)",
            "DataRowTestMethod (BaseString2)",
            "DataRowTestMethod (BaseString3)",
            "DataRowTestMethod (DerivedString1)",
            "DataRowTestMethod (DerivedString2)");
    }

    public void ExecuteOnlyDerivedClassDataRowsWhenItOverridesBaseClassDataRows_SimpleDataRows()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DerivedClass&TestCategory~DataRowSimple");
        var testResults = RunTests(testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "DataRowTestMethod (DerivedString1)",
            "DataRowTestMethod (DerivedString2)");
    }

    public void DataRowsExecuteWithRequiredAndOptionalParameters()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        var testCases = DiscoverTests(assemblyPath, "TestCategory~DataRowSomeOptional");
        var testResults = RunTests(testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "DataRowTestMethodWithSomeOptionalParameters (123)",
            "DataRowTestMethodWithSomeOptionalParameters (123,DerivedOptionalString1)",
            "DataRowTestMethodWithSomeOptionalParameters (123,DerivedOptionalString2,DerivedOptionalString3)");
    }

    public void DataRowsExecuteWithParamsArrayParameter()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        var testCases = DiscoverTests(assemblyPath, "TestCategory~DataRowParamsArgument");
        var testResults = RunTests(testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "DataRowTestMethodWithParamsParameters (2)",
            "DataRowTestMethodWithParamsParameters (2,DerivedSingleParamsArg)",
            "DataRowTestMethodWithParamsParameters (2,DerivedParamsArg1,DerivedParamsArg2)",
            "DataRowTestMethodWithParamsParameters (2,DerivedParamsArg1,DerivedParamsArg2,DerivedParamsArg3)");
    }

    public void DataRowsFailWhenInvalidArgumentsProvided()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowTests_Regular&TestCategory~DataRowOptionalInvalidArguments");
        var testResults = RunTests(testCases);

        // Assert
        VerifyE2E.TestsFailed(
            testResults,
            "DataRowTestMethodFailsWithInvalidArguments ()",
            "DataRowTestMethodFailsWithInvalidArguments (2)",
            "DataRowTestMethodFailsWithInvalidArguments (2,DerivedRequiredArgument,DerivedOptionalArgument,DerivedExtraArgument)");
    }

    public void DataRowsShouldSerializeDoublesProperly()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowTests_Regular.DataRowTestDouble");
        var testResults = RunTests(testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "DataRowTestDouble (10.01,20.01)",
            "DataRowTestDouble (10.02,20.02)");
    }

    public void DataRowsShouldSerializeMixedTypesProperly()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowTests_DerivedClass.DataRowTestMixed");
        var testResults = RunTests(testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "DataRowTestMixed (10,10,10,10,10,10,10,10)");
    }

    public void DataRowsShouldSerializeEnumsProperly()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowTests_DerivedClass.DataRowEnums");
        var testResults = RunTests(testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "DataRowEnums ()",
            "DataRowEnums (Alfa)",
            "DataRowEnums (Beta)",
            "DataRowEnums (Gamma)");
    }

    public void DataRowsShouldHandleNonSerializableValues()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowTests_DerivedClass.DataRowNonSerializable");
        var testResults = RunTests(testCases);

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

    public void ExecuteDataRowTests_Enums()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowTests_Enums");
        var testResults = RunTests(testCases);

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
            "DataRowEnums_Nullable_SByte ()",
            "DataRowEnums_Nullable_SByte (Alfa)",
            "DataRowEnums_Nullable_SByte (Beta)",
            "DataRowEnums_Nullable_SByte (Gamma)",
            "DataRowEnums_Nullable_Byte ()",
            "DataRowEnums_Nullable_Byte (Alfa)",
            "DataRowEnums_Nullable_Byte (Beta)",
            "DataRowEnums_Nullable_Byte (Gamma)",
            "DataRowEnums_Nullable_Short ()",
            "DataRowEnums_Nullable_Short (Alfa)",
            "DataRowEnums_Nullable_Short (Beta)",
            "DataRowEnums_Nullable_Short (Gamma)",
            "DataRowEnums_Nullable_UShort ()",
            "DataRowEnums_Nullable_UShort (Alfa)",
            "DataRowEnums_Nullable_UShort (Beta)",
            "DataRowEnums_Nullable_UShort (Gamma)",
            "DataRowEnums_Nullable_Int ()",
            "DataRowEnums_Nullable_Int (Alfa)",
            "DataRowEnums_Nullable_Int (Beta)",
            "DataRowEnums_Nullable_Int (Gamma)",
            "DataRowEnums_Nullable_UInt ()",
            "DataRowEnums_Nullable_UInt (Alfa)",
            "DataRowEnums_Nullable_UInt (Beta)",
            "DataRowEnums_Nullable_UInt (Gamma)",
            "DataRowEnums_Nullable_Long ()",
            "DataRowEnums_Nullable_Long (Alfa)",
            "DataRowEnums_Nullable_Long (Beta)",
            "DataRowEnums_Nullable_Long (Gamma)",
            "DataRowEnums_Nullable_ULong ()",
            "DataRowEnums_Nullable_ULong (Alfa)",
            "DataRowEnums_Nullable_ULong (Beta)",
            "DataRowEnums_Nullable_ULong (Gamma)",
            "DataRowEnums_MixedTypes_Byte (Alfa,True,1)",
            "DataRowEnums_MixedTypes_Byte (Beta,False,2)",
            "DataRowEnums_MixedTypes_Byte (Gamma,True,3)");

        VerifyE2E.FailedTestCount(testResults, 0);
    }

    public void ExecuteDataRowTests_NonSerializablePaths()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowTests_NonSerializablePaths");
        var testResults = RunTests(testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "DataRowNonSerializable (System.String)",
            "DataRowNonSerializable (System.Int32)",
            "DataRowNonSerializable (DataRowTestProject.DataRowTests_Enums)");
        VerifyE2E.FailedTestCount(testResults, 0);
    }

    public void ExecuteDataRowTests_Regular()
    {
        // Arrange
        var assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowTests_Regular");
        var testResults = RunTests(testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "DataRow1 (10)",
            "DataRow1 (20)",
            "DataRow1 (30)",
            "DataRow1 (40)",
            "DataRow2 (10,String parameter,True,False)",
            "DataRow2 (20,String parameter,True,False)",
            "DataRow2 (30,String parameter,True,False)",
            "DataRow2 (40,String parameter,True,False)",
            "DataRowTestDouble (10.01,20.01)",
            "DataRowTestDouble (10.02,20.02)",
            "DataRowTestMixed (1,10,10,10,10,10,10,10,10)",
            "DataRowTestMixed (2,10,10,10,10,10,10,10,10)",
            "DataRowTestMixed (3,10,10,10,10,10,10,10,10)",
            "DataRowTestMixed (4,10,10,10,10,10,10,10,10)",
            "NullValueInData (john.doe@example.com,abc123,)",
            "NullValueInData (john.doe@example.com,abc123,/unit/test)",
            "NullValue ()",
            "OneStringArray (System.String[])",
            "TwoStringArrays (System.String[],System.String[])",
            "OneObjectArray (System.Object[])",
            "TwoObjectArrays (System.Object[],System.Object[])",
            "ThreeObjectArrays (System.Object[],System.Object[],System.Object[])",
            "FourObjectArrays (System.Object[],System.Object[],System.Object[],System.Object[])",
            "FiveObjectArrays (System.Object[],System.Object[],System.Object[],System.Object[],System.Object[])",
            "SixObjectArrays (System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[])",
            "SevenObjectArrays (System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[])",
            "EightObjectArrays (System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[])",
            "NineObjectArrays (System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[])",
            "TenObjectArrays (System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[])",
            "ElevenObjectArrays (System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[])",
            "TwelveObjectArrays (System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[])",
            "ThirteenObjectArrays (System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[])",
            "FourteenObjectArrays (System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[])",
            "FifteenObjectArrays (System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[])",
            "SixteenObjectArrays (System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[],System.Object[])",
            "MultipleIntegersWrappedWithParams (1,2,3,4,5)");

        VerifyE2E.TestsFailed(
            testResults,
            "DataRowTestMethodFailsWithInvalidArguments ()",
            "DataRowTestMethodFailsWithInvalidArguments (2)",
            "DataRowTestMethodFailsWithInvalidArguments (2,DerivedRequiredArgument,DerivedOptionalArgument,DerivedExtraArgument)");
    }
}
