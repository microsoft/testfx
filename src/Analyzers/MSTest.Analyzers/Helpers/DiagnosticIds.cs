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
    public const string PreferDisposeOverTestCleanupRuleId = "MSTEST0021";
    public const string PreferTestCleanupOverDisposeRuleId = "MSTEST0022";
    public const string DoNotNegateBooleanAssertionRuleId = "MSTEST0023";
    public const string DoNotStoreStaticTestContextAnalyzerRuleId = "MSTEST0024";
    public const string PreferAssertFailOverAlwaysFalseConditionsRuleId = "MSTEST0025";
    public const string AssertionArgsShouldAvoidConditionalAccessRuleId = "MSTEST0026";
    public const string UseAsyncSuffixTestMethodSuppressorRuleId = "MSTEST0027";
    public const string UseAsyncSuffixTestFixtureMethodSuppressorRuleId = "MSTEST0028";
    public const string PublicMethodShouldBeTestMethodRuleId = "MSTEST0029";
    public const string TypeContainingTestMethodShouldBeATestClassRuleId = "MSTEST0030";
    public const string DoNotUseSystemDescriptionAttributeRuleId = "MSTEST0031";
    public const string ReviewAlwaysTrueAssertConditionAnalyzerRuleId = "MSTEST0032";
    public const string NonNullableReferenceNotInitializedSuppressorRuleId = "MSTEST0033";
    public const string UseClassCleanupBehaviorEndOfClassRuleId = "MSTEST0034";
    public const string UseDeploymentItemWithTestClassRuleId = "MSTEST0035";
    public const string UseDeploymentItemWithTestMethodRuleId = "MSTEST0036";
}
