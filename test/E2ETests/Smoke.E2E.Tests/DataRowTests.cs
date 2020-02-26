// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.Smoke.E2ETests
{
    using Microsoft.MSTestV2.CLIAutomation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DataRowTests : CLITestBase
    {
        private const string TestAssembly = "DataRowTestProject.dll";

        [TestMethod]
        public void ExecuteOnlyDerivedClassDataRowsWhenBothBaseAndDerviedClassHasDataRows_SimpleDataRows()
        {
            this.InvokeVsTestForExecution(new string[] { TestAssembly }, testCaseFilter: "TestCategory~DataRowSimple");

            this.ValidatePassedTestsContain(
                "DataRowTestMethod (BaseString1)",
                "DataRowTestMethod (BaseString2)",
                "DataRowTestMethod (BaseString3)",
                "DataRowTestMethod (DerivedString1)",
                "DataRowTestMethod (DerivedString2)");

            // 4 tests of BaseClass.DataRowTestMethod - 3 data row results and 1 parent result
            // 3 tests of DerivedClass.DataRowTestMethod - 2 data row results and 1 parent result
            // Total 7 tests - Making sure that DerivedClass doesn't run BaseClass tests
            this.ValidatePassedTestsCount(7);
        }

        [TestMethod]
        public void ExecuteOnlyDerivedClassDataRowsWhenItOverridesBaseClassDataRows_SimpleDataRows()
        {
            this.InvokeVsTestForExecution(new string[] { TestAssembly }, testCaseFilter: "FullyQualifiedName~DerivedClass&TestCategory~DataRowSimple");

            this.ValidatePassedTestsContain(
                "DataRowTestMethod (DerivedString1)",
                "DataRowTestMethod (DerivedString2)");

            // 3 tests of DerivedClass.DataRowTestMethod - 2 datarow result and 1 parent result
            this.ValidatePassedTestsCount(3);
        }

        [TestMethod]
        public void DataRowsExecuteWithRequiredAndOptionalParameters()
        {
            this.InvokeVsTestForExecution(new string[] { TestAssembly }, testCaseFilter: "TestCategory~DataRowSomeOptional");

            this.ValidatePassedTestsContain(
                "DataRowTestMethodWithSomeOptionalParameters (123)",
                "DataRowTestMethodWithSomeOptionalParameters (123,DerivedOptionalString1)",
                "DataRowTestMethodWithSomeOptionalParameters (123,DerivedOptionalString2,DerivedOptionalString3)");

            // 4 tests of DerivedClass.DataRowTestMethodWithSomeOptionalParameters - 3 datarow result and 1 parent result
            this.ValidatePassedTestsCount(4);
        }

        [TestMethod]
        public void DataRowsExecuteWithAllOptionalParameters()
        {
            this.InvokeVsTestForExecution(new string[] { TestAssembly }, testCaseFilter: "TestCategory~DataRowAllOptional");

            this.ValidatePassedTestsContain(
                "DataRowTestMethodWithAllOptionalParameters ()",
                "DataRowTestMethodWithAllOptionalParameters (123)",
                "DataRowTestMethodWithAllOptionalParameters (123,DerivedOptionalString4)",
                "DataRowTestMethodWithAllOptionalParameters (123,DerivedOptionalString5,DerivedOptionalString6)");

            // 5 tests of DerivedClass.DataRowTestMethodWithAllOptionalParameters - 4 datarow result and 1 parent result
            this.ValidatePassedTestsCount(5);
        }

        [TestMethod]
        public void DataRowsExecuteWithParamsArrayParameter()
        {
            this.InvokeVsTestForExecution(new string[] { TestAssembly }, testCaseFilter: "TestCategory~DataRowParamsArgument");

            this.ValidatePassedTestsContain(
                "DataRowTestMethodWithParamsParameters (2)",
                "DataRowTestMethodWithParamsParameters (2,DerivedSingleParamsArg)",
                "DataRowTestMethodWithParamsParameters (2,DerivedParamsArg1,DerivedParamsArg2)",
                "DataRowTestMethodWithParamsParameters (2,DerivedParamsArg1,DerivedParamsArg2,DerivedParamsArg3)");

            // 5 tests of DerivedClass.DataRowTestMethodWithParamsParameters - 4 datarow result and 1 parent result
            this.ValidatePassedTestsCount(5);
        }

        [TestMethod]
        public void DataRowsFailWhenInvalidArgumentsProvided()
        {
            this.InvokeVsTestForExecution(new string[] { TestAssembly }, testCaseFilter: "TestCategory~DataRowOptionalInvalidArguments");

            this.ValidatePassedTestsContain(
                "DataRowTestMethodFailsWithInvalidArguments ()",
                "DataRowTestMethodFailsWithInvalidArguments (2)",
                "DataRowTestMethodFailsWithInvalidArguments (2,DerivedRequiredArgument,DerivedOptionalArgument,DerivedExtraArgument)");

            // 4 tests of DerivedClass.DataRowTestMethodFailsWithInvalidArguments - 3 datarow result and 1 parent result
            this.ValidatePassedTestsCount(4);
        }
    }
}
