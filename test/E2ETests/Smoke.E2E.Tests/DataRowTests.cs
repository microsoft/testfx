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
            this.InvokeVsTestForExecution(new string[] { TestAssembly }, testCaseFilter: "FullyQualifiedName~DerivedClass");

            this.ValidatePassedTestsContain(
                "DataRowTestMethod (BaseString1)",
                "DataRowTestMethod (BaseString2)",
                "DataRowTestMethod (BaseString3)",
                "DataRowTestMethod (DerivedString1)",
                "DataRowTestMethod (DerivedString2)");

            // 4 tests of BaseClass - 3 datarow result and 1 parent result
            // 3 tests of DerivedClass - 2 datarow result and 1 parent result
            // Total 7 tests - Making sure that DerivedClass doesn't run BaseClass tests
            this.ValidatePassedTestsCount(7);
        }

        [TestMethod]
        public void ExecuteOnlyDerivedClassDataRowsWhenItOverridesBaseClassDataRows()
        {
            this.InvokeVsTestForExecution(new string[] { TestAssembly }, testCaseFilter: "FullyQualifiedName~DerivedClass");

            this.ValidatePassedTestsContain(
                "DataRowTestMethod (DerivedString1)",
                "DataRowTestMethod (DerivedString2)");

            // 3 tests of DerivedClass - 2 datarow result and 1 parent result
            this.ValidatePassedTestsCount(3);
        }
    }
}
