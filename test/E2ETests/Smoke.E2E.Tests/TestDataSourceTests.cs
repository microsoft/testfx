// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.Smoke.E2ETests;

using Microsoft.MSTestV2.CLIAutomation;

public class TestDataSourceTests : CLITestBase
{
    private const string TestAssembly = "DataSourceTestProject.dll";

    // Made it private because it Was Ignored.
    // TODO @haplois | @evangelink: This test fails under CI - will be fixed in a future PR
    private void ExecuteCsvTestDataSourceTests()
    {
        // Arrange & Act
        InvokeVsTestForExecution(
            new string[] { TestAssembly },
            testCaseFilter: "CsvTestMethod");

        // Assert
        ValidatePassedTests(
            "CsvTestMethod (Data Row 0)",
            "CsvTestMethod (Data Row 2)");

        ValidateFailedTests(
            TestAssembly,
            "CsvTestMethod (Data Row 1)",
            "CsvTestMethod (Data Row 3)");
    }

    public void ExecuteDynamicDataTests()
    {
        // Arrange & Act
        InvokeVsTestForExecution(
            new string[] { TestAssembly },
            testCaseFilter: "DynamicDataTest");

        // Assert
        ValidatePassedTests(
            "DynamicDataTest (John;Doe,DataSourceTestProject.ITestDataSourceTests.User)");

        ValidateFailedTestsCount(0);
    }

    public void ExecuteDataRowTests_Enums()
    {
        // Arrange & Act
        InvokeVsTestForExecution(
            new string[] { TestAssembly },
            testCaseFilter: "FullyQualifiedName~DataRowTests_Enums");

        // Assert
        ValidatePassedTests(
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

        ValidateFailedTestsCount(0);
    }

    public void ExecuteDataRowTests_NonSerializablePaths()
    {
        // Arrange & Act
        InvokeVsTestForExecution(
            new string[] { TestAssembly },
            testCaseFilter: "FullyQualifiedName~DataRowTests_NonSerializablePaths");

        // Assert
        ValidatePassedTests(
            "DataRowNonSerializable (System.String)",
            "DataRowNonSerializable (System.Int32)",
            "DataRowNonSerializable (DataSourceTestProject.ITestDataSourceTests.DataRowTests_Enums)");

        ValidateFailedTestsCount(0);
    }

    public void ExecuteRegular_DataRowTests()
    {
        // Arrange & Act
        InvokeVsTestForExecution(
            new string[] { TestAssembly },
            testCaseFilter: "FullyQualifiedName~Regular_DataRowTests");

        // Assert
        ValidatePassedTests(
            "DataRow1 (10)",
            "DataRow1 (20)",
            "DataRow1 (30)",
            "DataRow1 (40)",
            "DataRow2 (10,String parameter,True,False)",
            "DataRow2 (20,String parameter,True,False)",
            "DataRow2 (30,String parameter,True,False)",
            "DataRow2 (40,String parameter,True,False)",
            "DataRowTestMethodFailsWithInvalidArguments ()",
            "DataRowTestMethodFailsWithInvalidArguments (2)",
            "DataRowTestMethodFailsWithInvalidArguments (2,DerivedRequiredArgument,DerivedOptionalArgument,DerivedExtraArgument)",
            "DataRowTestDouble (10.01,20.01)",
            "DataRowTestDouble (10.02,20.02)",
            "DataRowTestMixed (1,10,10,10,10,10,10,10,10)",
            "DataRowTestMixed (2,10,10,10,10,10,10,10,10)",
            "DataRowTestMixed (3,10,10,10,10,10,10,10,10)",
            "DataRowTestMixed (4,10,10,10,10,10,10,10,10)",
            "NullValueInData (john.doe@example.com,abc123,)",
            "NullValueInData (john.doe@example.com,abc123,/unit/test)");

        ValidateFailedTestsCount(0);
    }
}
