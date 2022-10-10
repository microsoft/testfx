// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTestAdapter.Smoke.E2ETests;
public class DataRowTests : CLITestBase
{
    private const string TestAssembly = "DataRowTestProject.dll";

    public void ExecuteOnlyDerivedClassDataRowsWhenBothBaseAndDerviedClassHasDataRows_SimpleDataRows()
    {
        InvokeVsTestForExecution(new string[] { TestAssembly }, testCaseFilter: "TestCategory~DataRowSimple");

        ValidatePassedTestsContain(
            "DataRowTestMethod (BaseString1)",
            "DataRowTestMethod (BaseString2)",
            "DataRowTestMethod (BaseString3)",
            "DataRowTestMethod (DerivedString1)",
            "DataRowTestMethod (DerivedString2)");

        // 3 tests of BaseClass.DataRowTestMethod - 3 data row results and no parent result
        // 2 tests of DerivedClass.DataRowTestMethod - 2 data row results and no parent result
        // Total 5 tests - Making sure that DerivedClass doesn't run BaseClass tests
        ValidatePassedTestsCount(5);
    }

    public void ExecuteOnlyDerivedClassDataRowsWhenItOverridesBaseClassDataRows_SimpleDataRows()
    {
        InvokeVsTestForExecution(new string[] { TestAssembly }, testCaseFilter: "FullyQualifiedName~DerivedClass&TestCategory~DataRowSimple");

        ValidatePassedTestsContain(
            "DataRowTestMethod (DerivedString1)",
            "DataRowTestMethod (DerivedString2)");

        // 2 tests of DerivedClass.DataRowTestMethod - 2 datarow result and no parent result
        ValidatePassedTestsCount(2);
    }

    public void DataRowsExecuteWithRequiredAndOptionalParameters()
    {
        InvokeVsTestForExecution(new string[] { TestAssembly }, testCaseFilter: "TestCategory~DataRowSomeOptional");

        ValidatePassedTestsContain(
            "DataRowTestMethodWithSomeOptionalParameters (123)",
            "DataRowTestMethodWithSomeOptionalParameters (123,DerivedOptionalString1)",
            "DataRowTestMethodWithSomeOptionalParameters (123,DerivedOptionalString2,DerivedOptionalString3)");

        // 3 tests of DerivedClass.DataRowTestMethodWithSomeOptionalParameters - 3 datarow result and no parent result
        ValidatePassedTestsCount(3);
    }

    public void DataRowsExecuteWithAllOptionalParameters()
    {
        InvokeVsTestForExecution(new string[] { TestAssembly }, testCaseFilter: "TestCategory~DataRowAllOptional");

        ValidatePassedTestsContain(
            "DataRowTestMethodWithAllOptionalParameters ()",
            "DataRowTestMethodWithAllOptionalParameters (123)",
            "DataRowTestMethodWithAllOptionalParameters (123,DerivedOptionalString4)",
            "DataRowTestMethodWithAllOptionalParameters (123,DerivedOptionalString5,DerivedOptionalString6)");

        // 4 tests of DerivedClass.DataRowTestMethodWithAllOptionalParameters - 4 datarow result and no parent result
        ValidatePassedTestsCount(4);
    }

    public void DataRowsExecuteWithParamsArrayParameter()
    {
        InvokeVsTestForExecution(new string[] { TestAssembly }, testCaseFilter: "TestCategory~DataRowParamsArgument");

        ValidatePassedTestsContain(
            "DataRowTestMethodWithParamsParameters (2)",
            "DataRowTestMethodWithParamsParameters (2,DerivedSingleParamsArg)",
            "DataRowTestMethodWithParamsParameters (2,DerivedParamsArg1,DerivedParamsArg2)",
            "DataRowTestMethodWithParamsParameters (2,DerivedParamsArg1,DerivedParamsArg2,DerivedParamsArg3)");

        // 4 tests of DerivedClass.DataRowTestMethodWithParamsParameters - 4 datarow result and no parent result
        ValidatePassedTestsCount(4);
    }

    public void DataRowsFailWhenInvalidArgumentsProvided()
    {
        InvokeVsTestForExecution(new string[] { TestAssembly }, testCaseFilter: "TestCategory~DataRowOptionalInvalidArguments");

        ValidatePassedTestsContain(
            "DataRowTestMethodFailsWithInvalidArguments ()",
            "DataRowTestMethodFailsWithInvalidArguments (2)",
            "DataRowTestMethodFailsWithInvalidArguments (2,DerivedRequiredArgument,DerivedOptionalArgument,DerivedExtraArgument)");

        // 3 tests of DerivedClass.DataRowTestMethodFailsWithInvalidArguments - 3 datarow result and no parent result
        ValidatePassedTestsCount(3);
    }
}
