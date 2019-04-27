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

            // 4 tests of BaseClass.DataRowTestMethod - 3 datarow result and 1 parent result
            // 3 tests of DerivedClass.DataRowTestMethod - 2 datarow result and 1 parent result
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
        public void ExecuteOnlyDerivedClassDataRowsWhenBothBaseAndDerviedClassHasDataRows_DataRowSomeOptional()
        {
            this.InvokeVsTestForExecution(new string[] { TestAssembly }, testCaseFilter: "TestCategory~DataRowSomeOptional");

            this.ValidatePassedTestsContain(
                "DataRowTestMethodWithSomeOptionalParameters (42)",
                "DataRowTestMethodWithSomeOptionalParameters (42,BaseOptionalString1)",
                "DataRowTestMethodWithSomeOptionalParameters (42,BaseOptionalString2,BaseOptionalString3)",
                "DataRowTestMethodWithSomeOptionalParameters (42,BaseOptionalString4,BaseOptionalString5)",
                "DataRowTestMethodWithSomeOptionalParameters (123)",
                "DataRowTestMethodWithSomeOptionalParameters (123,DerivedOptionalString1)",
                "DataRowTestMethodWithSomeOptionalParameters (123,DerivedOptionalString2,DerivedOptionalString3)");

            // 5 tests of BaseClass.DataRowTestMethod - 4 datarow result and 1 parent result
            // 4 tests of DerivedClass.DataRowTestMethod - 3 datarow result and 1 parent result
            // Total 9 tests - Making sure that DerivedClass doesn't run BaseClass tests
            this.ValidatePassedTestsCount(9);
        }

        [TestMethod]
        public void ExecuteOnlyDerivedClassDataRowsWhenItOverridesBaseClassDataRows_DataRowSomeOptional()
        {
            this.InvokeVsTestForExecution(new string[] { TestAssembly }, testCaseFilter: "FullyQualifiedName~DerivedClass&TestCategory~DataRowSomeOptional");

            this.ValidatePassedTestsContain(
                "DataRowTestMethodWithSomeOptionalParameters (123)",
                "DataRowTestMethodWithSomeOptionalParameters (123,DerivedOptionalString1)",
                "DataRowTestMethodWithSomeOptionalParameters (123,DerivedOptionalString2,DerivedOptionalString3)");

            // 4 tests of DerivedClass.DataRowTestMethod - 3 datarow result and 1 parent result
            this.ValidatePassedTestsCount(4);
        }

        [TestMethod]
        public void ExecuteOnlyDerivedClassDataRowsWhenBothBaseAndDerviedClassHasDataRows_DataRowAllOptional()
        {
            this.InvokeVsTestForExecution(new string[] { TestAssembly }, testCaseFilter: "TestCategory~DataRowAllOptional");

            this.ValidatePassedTestsContain(
                "DataRowTestMethodWithAllOptionalParameters ()",
                "DataRowTestMethodWithAllOptionalParameters (42)",
                "DataRowTestMethodWithAllOptionalParameters (42,BaseOptionalString6)",
                "DataRowTestMethodWithAllOptionalParameters (42,BaseOptionalString7,BaseOptionalString8)",
                "DataRowTestMethodWithAllOptionalParameters (42,BaseOptionalString9,BaseOptionalString10)",
                "DataRowTestMethodWithAllOptionalParameters (123)",
                "DataRowTestMethodWithAllOptionalParameters (123,DerivedOptionalString4)",
                "DataRowTestMethodWithAllOptionalParameters (123,DerivedOptionalString5,DerivedOptionalString6)");

            // 6 tests of BaseClass.DataRowTestMethod - 4 datarow result and 1 parent result
            // 4 tests of DerivedClass.DataRowTestMethod - 3 datarow result and 1 parent result
            // Total 10 tests - Making sure that DerivedClass doesn't run BaseClass tests
            this.ValidatePassedTestsCount(10);
        }

        [TestMethod]
        public void ExecuteOnlyDerivedClassDataRowsWhenItOverridesBaseClassDataRows_DataRowAllOptional()
        {
            this.InvokeVsTestForExecution(new string[] { TestAssembly }, testCaseFilter: "FullyQualifiedName~DerivedClass&TestCategory~DataRowAllOptional");

            this.ValidatePassedTestsContain(
                "DataRowTestMethodWithAllOptionalParameters (123)",
                "DataRowTestMethodWithAllOptionalParameters (123,DerivedOptionalString4)",
                "DataRowTestMethodWithAllOptionalParameters (123,DerivedOptionalString5,DerivedOptionalString6)");

            // 4 tests of DerivedClass.DataRowTestMethod - 3 datarow result and 1 parent result
            this.ValidatePassedTestsCount(4);
        }

        [TestMethod]
        public void ExecuteOnlyDerivedClassDataRowsWhenBothBaseAndDerviedClassHasDataRows_DataRowParamsArgument()
        {
            this.InvokeVsTestForExecution(new string[] { TestAssembly }, testCaseFilter: "TestCategory~DataRowParamsArgument");

            this.ValidatePassedTestsContain(
                "DataRowTestMethodWithParamsParameters (1)",
                "DataRowTestMethodWithParamsParameters (1,BaseSingleParamsArg)",
                "DataRowTestMethodWithParamsParameters (1,BaseParamsArg1,BaseParamsArg2)",
                "DataRowTestMethodWithParamsParameters (1,BaseParamsArg1,BaseParamsArg2,BaseParamsArg3)",
                "DataRowTestMethodWithParamsParameters (2)",
                "DataRowTestMethodWithParamsParameters (2,DerivedSingleParamsArg)",
                "DataRowTestMethodWithParamsParameters (2,DerivedParamsArg1,DerivedParamsArg2)");

            // 5 tests of BaseClass.DataRowTestMethod - 4 datarow result and 1 parent result
            // 4 tests of DerivedClass.DataRowTestMethod - 3 datarow result and 1 parent result
            // Total 9 tests - Making sure that DerivedClass doesn't run BaseClass tests
            this.ValidatePassedTestsCount(9);
        }

        [TestMethod]
        public void ExecuteOnlyDerivedClassDataRowsWhenItOverridesBaseClassDataRows_DataRowParamsArgument()
        {
            this.InvokeVsTestForExecution(new string[] { TestAssembly }, testCaseFilter: "FullyQualifiedName~DerivedClass&TestCategory~DataRowParamsArgument");

            this.ValidatePassedTestsContain(
                "DataRowTestMethodWithParamsParameters (2)",
                "DataRowTestMethodWithParamsParameters (2,DerivedSingleParamsArg)",
                "DataRowTestMethodWithParamsParameters (2,DerivedParamsArg1,DerivedParamsArg2)");

            // 4 tests of DerivedClass.DataRowTestMethod - 3 datarow result and 1 parent result
            this.ValidatePassedTestsCount(4);
        }

        [TestMethod]
        public void ExecuteOnlyDerivedClassDataRowsWhenBothBaseAndDerviedClassHasDataRows_DataRowOptionalInvalidArguments()
        {
            this.InvokeVsTestForExecution(new string[] { TestAssembly }, testCaseFilter: "TestCategory~DataRowOptionalInvalidArguments");

            this.ValidatePassedTestsContain(
                "DataRowTestMethodFailsWithInvalidArguments ()",
                "DataRowTestMethodFailsWithInvalidArguments (1)",
                "DataRowTestMethodFailsWithInvalidArguments (1,BaseRequiredArgument,BaseOptionalArgument,BaseExtraArgument)",
                "DataRowTestMethodFailsWithInvalidArguments (2)",
                "DataRowTestMethodFailsWithInvalidArguments (2,DerivedRequiredArgument,DerivedOptionalArgument,DerivedExtraArgument)");

            // 4 tests of BaseClass.DataRowTestMethod - 3 datarow result and 1 parent result
            // 3 tests of DerivedClass.DataRowTestMethod - 2 datarow result and 1 parent result
            // Total 7 tests - Making sure that DerivedClass doesn't run BaseClass tests
            this.ValidatePassedTestsCount(7);
        }

        [TestMethod]
        public void ExecuteOnlyDerivedClassDataRowsWhenItOverridesBaseClassDataRows_DataRowOptionalInvalidArguments()
        {
            this.InvokeVsTestForExecution(new string[] { TestAssembly }, testCaseFilter: "FullyQualifiedName~DerivedClass&TestCategory~DataRowOptionalInvalidArguments");

            this.ValidatePassedTestsContain(
                "DataRowTestMethodFailsWithInvalidArguments (2)",
                "DataRowTestMethodFailsWithInvalidArguments (2,DerivedRequiredArgument,DerivedOptionalArgument,DerivedExtraArgument)");

            // 3 tests of DerivedClass.DataRowTestMethod - 2 datarow result and 1 parent result
            this.ValidatePassedTestsCount(3);
        }
    }
}
