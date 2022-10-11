// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

using Microsoft.MSTestV2.CLIAutomation;

namespace Microsoft.MSTestV2.Smoke.DiscoveryAndExecutionTests;
public class DataRowTests : CLITestBase
{
    private const string TestAssembly = "DataRowTestProject.dll";

    public void ExecuteOnlyDerivedClassDataRowsWhenBothBaseAndDerviedClassHasDataRows_SimpleDataRows()
    {
        // Arrange
        var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : GetAssetFullPath(TestAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath, "TestCategory~DataRowSimple");
        var testResults = RunTests(assemblyPath, testCases);

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
        var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : GetAssetFullPath(TestAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DerivedClass&TestCategory~DataRowSimple");
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "DataRowTestMethod (DerivedString1)",
            "DataRowTestMethod (DerivedString2)");
    }

    public void DataRowsExecuteWithRequiredAndOptionalParameters()
    {
        // Arrange
        var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : GetAssetFullPath(TestAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath, "TestCategory~DataRowSomeOptional");
        var testResults = RunTests(assemblyPath, testCases);

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
        var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : GetAssetFullPath(TestAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath, "TestCategory~DataRowParamsArgument");
        var testResults = RunTests(assemblyPath, testCases);

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
        var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : GetAssetFullPath(TestAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath, "TestCategory~DataRowOptionalInvalidArguments");
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "DataRowTestMethodFailsWithInvalidArguments ()",
            "DataRowTestMethodFailsWithInvalidArguments (2)",
            "DataRowTestMethodFailsWithInvalidArguments (2,DerivedRequiredArgument,DerivedOptionalArgument,DerivedExtraArgument)");
    }

    public void DataRowsShouldSerializeDoublesProperly()
    {
        // Arrange
        var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : GetAssetFullPath(TestAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowTestDouble");
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "DataRowTestDouble (10.01,20.01)",
            "DataRowTestDouble (10.02,20.02)");
    }

    public void DataRowsShouldSerializeMixedTypesProperly()
    {
        // Arrange
        var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : GetAssetFullPath(TestAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowTestMixed");
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "DataRowTestMixed (10,10,10,10,10,10,10,10)");
    }

    public void DataRowsShouldSerializeEnumsProperly()
    {
        // Arrange
        var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : GetAssetFullPath(TestAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowEnums");
        var testResults = RunTests(assemblyPath, testCases);

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
        var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : GetAssetFullPath(TestAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowNonSerializable");
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        VerifyE2E.TestsDiscovered(
            testCases,
            "DataRowNonSerializable");

        VerifyE2E.TestsPassed(
            testResults,
            "DataRowNonSerializable (System.String)",
            "DataRowNonSerializable (System.Int32)",
            "DataRowNonSerializable (DataRowTestProject.DerivedClass)");
    }
}
