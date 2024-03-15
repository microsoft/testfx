// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.Analyzers.Helpers;

internal static class DiagnosticIds
{
    public const string UseParallelizedAttributeRuleId = "MSTEST0001";
    public const string TestClassShouldBeValidRuleId = "MSTEST0002";
    public const string TestMethodShouldBeValidRuleId = "MSTEST0003";
    public const string PublicTypeShouldBeTestClassRuleId = "MSTEST0004";
    public const string TestContextShouldBeValidRuleId = "MSTEST0005";
    public const string AvoidExpectedExceptionAttributeRuleId = "MSTEST0006";
    public const string UseAttributeOnTestMethodRuleId = "MSTEST0007";
    public const string TestInitializeShouldBeValidRuleId = "MSTEST0008";
    public const string TestCleanupShouldBeValidRuleId = "MSTEST0009";
    public const string ClassInitializeShouldBeValidRuleId = "MSTEST0010";
    public const string ClassCleanupShouldBeValidRuleId = "MSTEST0011";
    public const string AssemblyInitializeShouldBeValidRuleId = "MSTEST0012";
    public const string AssemblyCleanupShouldBeValidRuleId = "MSTEST0013";
    public const string DataRowShouldBeValidRuleId = "MSTEST0014";
    public const string TestMethodShouldNotBeIgnoredRuleId = "MSTEST0015";
    public const string TestClassShouldHaveTestMethodRuleId = "MSTEST0016";
    public const string AssertionArgsShouldBePassedInCorrectOrderRuleId = "MSTEST0017";
    public const string PreferTestInitializeOverConstructorRuleId = "MSTEST0019";
    public const string PreferConstructorOverTestInitializeRuleId = "MSTEST0020";
    public const string PreferTestCleanupOverDisposeRuleId = "MSTEST0022";
}
