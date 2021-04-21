// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.MSTestV2.Smoke.DiscoveryAndExecutionTests
{
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
            Assert.That.ContainsTestsPassed(testResults,
                "DataRowTestMethod (BaseString1)",
                "DataRowTestMethod (BaseString2)",
                "DataRowTestMethod (BaseString3)",
                "DataRowTestMethod (DerivedString1)",
                "DataRowTestMethod (DerivedString2)"
            );

            // 4 tests of BaseClass.DataRowTestMethod - 3 data row results and 1 parent result
            // 3 tests of DerivedClass.DataRowTestMethod - 2 data row results and 1 parent result
            // Total 7 tests - Making sure that DerivedClass doesn't run BaseClass tests
            Assert.That.PassedTestCount(testResults, 7 - 2);
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
            Assert.That.ContainsTestsPassed(testResults,
                "DataRowTestMethod (DerivedString1)",
                "DataRowTestMethod (DerivedString2)"
            );

            // 3 tests of DerivedClass.DataRowTestMethod - 2 datarow result and 1 parent result
            Assert.That.PassedTestCount(testResults, 3 - 1);
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
            Assert.That.ContainsTestsPassed(testResults,
                "DataRowTestMethodWithSomeOptionalParameters (123)",
                "DataRowTestMethodWithSomeOptionalParameters (123,DerivedOptionalString1)",
                "DataRowTestMethodWithSomeOptionalParameters (123,DerivedOptionalString2,DerivedOptionalString3)"
            );

            // 4 tests of DerivedClass.DataRowTestMethodWithSomeOptionalParameters - 3 datarow result and 1 parent result
            Assert.That.PassedTestCount(testResults, 4 - 1);
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
            Assert.That.ContainsTestsPassed(testResults,
                "DataRowTestMethodWithParamsParameters (2)",
                "DataRowTestMethodWithParamsParameters (2,DerivedSingleParamsArg)",
                "DataRowTestMethodWithParamsParameters (2,DerivedParamsArg1,DerivedParamsArg2)",
                "DataRowTestMethodWithParamsParameters (2,DerivedParamsArg1,DerivedParamsArg2,DerivedParamsArg3)"
            );

            // 5 tests of DerivedClass.DataRowTestMethodWithParamsParameters - 4 datarow result and 1 parent result
            Assert.That.PassedTestCount(testResults, 5 - 1);
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
            Assert.That.ContainsTestsPassed(testResults,
                "DataRowTestMethodFailsWithInvalidArguments ()",
                "DataRowTestMethodFailsWithInvalidArguments (2)",
                "DataRowTestMethodFailsWithInvalidArguments (2,DerivedRequiredArgument,DerivedOptionalArgument,DerivedExtraArgument)"
            );

            // 4 tests of DerivedClass.DataRowTestMethodFailsWithInvalidArguments - 3 datarow result and 1 parent result
            Assert.That.PassedTestCount(testResults, 4 - 1);
        }
    }
}
