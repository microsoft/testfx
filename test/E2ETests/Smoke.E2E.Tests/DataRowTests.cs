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
        public void ExecuteOnlyDerivedClassDataRowsWhenBothBaseAndDerviedClassHasDataRows()
        {
            this.InvokeVsTestForExecution(new string[] { TestAssembly });

            this.ValidatePassedTestsContain(
                "DataRowTestMethod (BaseString1)",
                "DataRowTestMethod (BaseString2)",
                "DataRowTestMethod (BaseString3)",
                "DataRowTestMethod (DerivedString1)",
                "DataRowTestMethod (DerivedString2)",
                "DataRowTestMethodWithSomeOptionalParameters (42)",
                "DataRowTestMethodWithSomeOptionalParameters (42,BaseOptionalString1)",
                "DataRowTestMethodWithSomeOptionalParameters (42,BaseOptionalString2,BaseOptionalString3)",
                "DataRowTestMethodWithSomeOptionalParameters (42,BaseOptionalString4,BaseOptionalString5)",
                "DataRowTestMethodWithSomeOptionalParameters (123)",
                "DataRowTestMethodWithSomeOptionalParameters (123,DerivedOptionalString1)",
                "DataRowTestMethodWithSomeOptionalParameters (123,DerivedOptionalString2,DerivedOptionalString3)",
                "DataRowTestMethodWithAllOptionalParameters ()",
                "DataRowTestMethodWithAllOptionalParameters (42)",
                "DataRowTestMethodWithAllOptionalParameters (42,BaseOptionalString6)",
                "DataRowTestMethodWithAllOptionalParameters (42,BaseOptionalString7,BaseOptionalString8)",
                "DataRowTestMethodWithAllOptionalParameters (42,BaseOptionalString9,BaseOptionalString10)",
                "DataRowTestMethodWithAllOptionalParameters (123)",
                "DataRowTestMethodWithAllOptionalParameters (123,DerivedOptionalString4)",
                "DataRowTestMethodWithAllOptionalParameters (123,DerivedOptionalString5,DerivedOptionalString6)");

            // 4 tests of BaseClass.DataRowTestMethod - 3 datarow result and 1 parent result
            // 5 tests of BaseClass.DataRowTestMethodWithSomeOptionalParameters - 4 datarow result and 1 parent result
            // 6 tests of BaseClass.DataRowTestMethodWithAllOptionalParameters - 5 datarow result and 1 parent result
            // 3 tests of DerivedClass.DataRowTestMethod - 2 datarow result and 1 parent result
            // 4 tests of DerivedClass.DataRowTestMethodWithAllOptionalParameters - 3 datarow result and 1 parent result
            // 4 tests of DerivedClass.DataRowTestMethodWithAllOptionalParameters - 3 datarow result and 1 parent result
            // Total 26 tests - Making sure that DerivedClass doesn't run BaseClass tests
            this.ValidatePassedTestsCount(26);
        }

        [TestMethod]
        public void ExecuteOnlyDerivedClassDataRowsWhenItOverridesBaseClassDataRows()
        {
            this.InvokeVsTestForExecution(new string[] { TestAssembly }, testCaseFilter: "FullyQualifiedName~DerivedClass");

            this.ValidatePassedTestsContain(
                "DataRowTestMethod (DerivedString1)",
                "DataRowTestMethod (DerivedString2)",
                "DataRowTestMethodWithSomeOptionalParameters (123)",
                "DataRowTestMethodWithSomeOptionalParameters (123,DerivedOptionalString1)",
                "DataRowTestMethodWithSomeOptionalParameters (123,DerivedOptionalString2,DerivedOptionalString3)",
                "DataRowTestMethodWithAllOptionalParameters (123)",
                "DataRowTestMethodWithAllOptionalParameters (123,DerivedOptionalString4)",
                "DataRowTestMethodWithAllOptionalParameters (123,DerivedOptionalString5,DerivedOptionalString6)");

            // 3 tests of DerivedClass.DataRowTestMethod - 2 datarow result and 1 parent result
            // 4 tests of DerivedClass.DataRowTestMethodWithAllOptionalParameters - 3 datarow result and 1 parent result
            // 4 tests of DerivedClass.DataRowTestMethodWithAllOptionalParameters - 3 datarow result and 1 parent result
            this.ValidatePassedTestsCount(11);
        }
    }
}
