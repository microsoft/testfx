## Release 3.11.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MSTEST0051 | Usage | Info | AssertThrowsShouldContainSingleStatementAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0051)
MSTEST0052 | Usage | Warning | PreferDynamicDataSourceTypeAutoDetectAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0052)
MSTEST0053 | Usage | Warning | AvoidAssertFormatParametersAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0053)
MSTEST0054 | Usage | Info | UseCancellationTokenPropertyAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0054)
MSTEST0055 | Usage | Warning | IgnoreStringMethodReturnValueAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0055)

## Release 3.10.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MSTEST0044 | Design | Warning | PreferTestMethodOverDataTestMethodAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0044)
MSTEST0045 | Usage | Info | UseCooperativeCancellationForTimeoutAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0045)
MSTEST0046 | Usage | Info | StringAssertToAssertAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0046)
MSTEST0048 | Usage | Warning | TestContextPropertyUsageAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0048)
MSTEST0049 | Usage | Info | FlowTestContextCancellationTokenAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0049)
MSTEST0050 | Usage | Error | GlobalTestFixtureShouldBeValidAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0050)

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-------
MSTEST0006 | Design | Warning | Design | Info | AvoidExpectedExceptionAttributeAnalyzer
MSTEST0039 | Usage | Warning | Usage | Info | UseNewerAssertThrowsAnalyzer

## Release 3.9.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MSTEST0042 | Usage | Warning | DuplicateDataRowAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0042)
MSTEST0043 | Usage | Warning | UseRetryWithTestMethodAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0043)

## Release 3.8.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MSTEST0015 | Design | Disabled | TestMethodShouldNotBeIgnoredAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0015)
MSTEST0026 | Usage | Disabled | AssertionArgsShouldAvoidConditionalAccessRuleId, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0026)
MSTEST0038 | Usage | Warning | AvoidAssertAreSameWithValueTypesAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0038)
MSTEST0039 | Usage | Info | UseNewerAssertThrowsAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0039)
MSTEST0040 | Usage | Warning | AvoidUsingAssertsInAsyncVoidContextAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0040)
MSTEST0041 | Usage | Warning | UseConditionBaseWithTestClassAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0041)

## Release 3.7.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MSTEST0037 | `Usage` | Info | UseProperAssertMethodsAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0037)

## Release 3.6.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MSTEST0018 | Usage | Warning | DynamicDataShouldBeValidAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0018)
MSTEST0034 | Usage | Info | UseClassCleanupBehaviorEndOfClassAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0034)
MSTEST0035 | Usage | Info | UseDeploymentItemWithTestMethodOrTestClassAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0035)
MSTEST0036 | Design | Warning | DoNotUseShadowingAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0036)

## Release 3.5.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MSTEST0026 | Usage | Info | AssertionArgsShouldAvoidConditionalAccessRuleId, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0026)
MSTEST0029 | Design | Disabled | PublicMethodShouldBeTestMethodAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0029)
MSTEST0030 | Usage | Info | TypeContainingTestMethodShouldBeATestClassAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0030)
MSTEST0031 | Usage | Info | DoNotUseSystemDescriptionAttributeAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0031)
MSTEST0032 | Usage | Info | ReviewAlwaysTrueAssertConditionAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0032)

## Release 3.4.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MSTEST0017 | Usage | Info | AssertionArgsShouldBePassedInCorrectOrder, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0017)
MSTEST0019 | Design | Disabled | PreferTestInitializeOverConstructorAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0019)
MSTEST0020 | Design | Disabled | PreferConstructorOverTestInitializeAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0020)
MSTEST0021 | Design | Disabled | PreferDisposeOverTestCleanupAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0021)
MSTEST0022 | Design | Disabled | PreferTestCleanupOverDisposeAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0022)
MSTEST0023 | Usage | Info | DoNotNegateBooleanAssertionAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0023)
MSTEST0024 | Usage | Info | DoNotStoreStaticTestContextAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0024)
MSTEST0025 | Usage | Info | PreferAssertFailOverAlwaysFalseConditionsAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0025)

## Release 3.3.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MSTEST0007 | Usage | Info | UseAttributeOnTestMethodAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0007)
MSTEST0008 | Usage | Warning | TestInitializeShouldBeValidAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0008)
MSTEST0009 | Usage | Warning | TestCleanupShouldBeValidAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0009)
MSTEST0010 | Usage | Warning | ClassInitializeShouldBeValidAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0010)
MSTEST0011 | Usage | Warning | ClassCleanupShouldBeValidAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0011)
MSTEST0012 | Usage | Warning | AssemblyInitializeShouldBeValidAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0012)
MSTEST0013 | Usage | Warning | AssemblyCleanupShouldBeValidAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0013)
MSTEST0014 | Usage | Warning | DataRowShouldBeValidAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0014)
MSTEST0015 | Design | Info | TestMethodShouldNotBeIgnoredAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0015)
MSTEST0016 | Design | Info | TestClassShouldHaveTestMethodAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0016)

## Release 3.2.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MSTEST0001 | Performance | Info | UseParallelizeAttributeAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0001)
MSTEST0002 | Usage | Warning | TestClassShouldBeValidAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0002)
MSTEST0003 | Usage | Warning | TestMethodShouldBeValidAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0003)
MSTEST0004 | Design | Disabled | PublicTypeShouldBeTestClassAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0004)
MSTEST0005 | Usage | Warning | TestContextShouldBeValidAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0005)
MSTEST0006 | Design | Info | AvoidExpectedExceptionAttributeAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0006)
