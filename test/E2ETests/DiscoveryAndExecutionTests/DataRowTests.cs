// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.MSTestV2.Smoke.DiscoveryAndExecutionTests;

using Microsoft.MSTestV2.CLIAutomation;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.IO;


[TestClass]
public class DataRowTests : CLITestBase
{
    private const string TestAssembly = "DataRowTestProject.dll";

    [TestMethod]
    public void ExecuteOnlyDerivedClassDataRowsWhenBothBaseAndDerviedClassHasDataRows_SimpleDataRows()
    {
        // Arrange
        var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : this.GetAssetFullPath(TestAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath, "TestCategory~DataRowSimple");
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        Assert.That.TestsPassed(testResults,
            "DataRowTestMethod (BaseString1)",
            "DataRowTestMethod (BaseString2)",
            "DataRowTestMethod (BaseString3)",
            "DataRowTestMethod (DerivedString1)",
            "DataRowTestMethod (DerivedString2)"
        );
    }

    [TestMethod]
    public void ExecuteOnlyDerivedClassDataRowsWhenItOverridesBaseClassDataRows_SimpleDataRows()
    {
        // Arrange
        var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : this.GetAssetFullPath(TestAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DerivedClass&TestCategory~DataRowSimple");
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        Assert.That.TestsPassed(testResults,
            "DataRowTestMethod (DerivedString1)",
            "DataRowTestMethod (DerivedString2)"
        );
    }

    [TestMethod]
    public void DataRowsExecuteWithRequiredAndOptionalParameters()
    {
        // Arrange
        var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : this.GetAssetFullPath(TestAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath, "TestCategory~DataRowSomeOptional");
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        Assert.That.TestsPassed(testResults,
            "DataRowTestMethodWithSomeOptionalParameters (123)",
            "DataRowTestMethodWithSomeOptionalParameters (123,DerivedOptionalString1)",
            "DataRowTestMethodWithSomeOptionalParameters (123,DerivedOptionalString2,DerivedOptionalString3)"
        );
    }

    [TestMethod]
    public void DataRowsExecuteWithParamsArrayParameter()
    {
        // Arrange
        var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : this.GetAssetFullPath(TestAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath, "TestCategory~DataRowParamsArgument");
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        Assert.That.TestsPassed(testResults,
            "DataRowTestMethodWithParamsParameters (2)",
            "DataRowTestMethodWithParamsParameters (2,DerivedSingleParamsArg)",
            "DataRowTestMethodWithParamsParameters (2,DerivedParamsArg1,DerivedParamsArg2)",
            "DataRowTestMethodWithParamsParameters (2,DerivedParamsArg1,DerivedParamsArg2,DerivedParamsArg3)"
        );
    }

    [TestMethod]
    public void DataRowsFailWhenInvalidArgumentsProvided()
    {
        // Arrange
        var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : this.GetAssetFullPath(TestAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath, "TestCategory~DataRowOptionalInvalidArguments");
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        Assert.That.TestsPassed(testResults,
            "DataRowTestMethodFailsWithInvalidArguments ()",
            "DataRowTestMethodFailsWithInvalidArguments (2)",
            "DataRowTestMethodFailsWithInvalidArguments (2,DerivedRequiredArgument,DerivedOptionalArgument,DerivedExtraArgument)"
        );
    }

    [TestMethod]
    public void DataRowsShouldSerializeDoublesProperly()
    {
        // Arrange
        var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : this.GetAssetFullPath(TestAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowTestDouble");
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        Assert.That.TestsPassed(testResults,
            "DataRowTestDouble (10.01,20.01)",
            "DataRowTestDouble (10.02,20.02)"
        );
    }

    [TestMethod]
    public void DataRowsShouldSerializeMixedTypesProperly()
    {
        // Arrange
        var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : this.GetAssetFullPath(TestAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowTestMixed");
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        Assert.That.TestsPassed(testResults,
            "DataRowTestMixed (10,10,10,10,10,10,10,10)"
        );
    }

    [TestMethod]
    public void DataRowsShouldSerializeEnumsProperly()
    {
        // Arrange
        var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : this.GetAssetFullPath(TestAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowEnums");
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        Assert.That.TestsPassed(testResults,
            "DataRowEnums ()",
            "DataRowEnums (Alfa)",
            "DataRowEnums (Beta)",
            "DataRowEnums (Gamma)"
        );
    }

    [TestMethod]
    public void DataRowsShouldHandleNonSerializableValues()
    {
        // Arrange
        var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : this.GetAssetFullPath(TestAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath, "FullyQualifiedName~DataRowNonSerializable");
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        Assert.That.TestsDiscovered(testCases,
            "DataRowNonSerializable"
        );

        Assert.That.TestsPassed(testResults,
            "DataRowNonSerializable (System.String)",
            "DataRowNonSerializable (System.Int32)",
            "DataRowNonSerializable (DataRowTestProject.DerivedClass)"
        );
    }
}
