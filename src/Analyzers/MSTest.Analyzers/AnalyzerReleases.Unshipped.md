; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MSTEST0053 | `<Undetected>` | Warning | TestMethodAttributeShouldNotHaveStringArgumentAnalyzer

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-------
MSTEST0001 | Performance | Warning | Performance | Info | UseParallelizeAttributeAnalyzer
MSTEST0007 | Usage | Warning | Usage | Info | UseAttributeOnTestMethodAnalyzer
MSTEST0017 | Usage | Warning | Usage | Info | AssertionArgsShouldBePassedInCorrectOrderAnalyzer
MSTEST0023 | Usage | Warning | Usage | Info | DoNotNegateBooleanAssertionAnalyzer
MSTEST0024 | Usage | Warning | Usage | Info | DoNotStoreStaticTestContextAnalyzer
MSTEST0025 | Usage | Warning | Usage | Info | PreferAssertFailOverAlwaysFalseConditionsAnalyzer
MSTEST0030 | Usage | Warning | Usage | Info | TypeContainingTestMethodShouldBeATestClassAnalyzer
MSTEST0031 | Usage | Warning | Usage | Info | DoNotUseSystemDescriptionAttributeAnalyzer
MSTEST0032 | Usage | Warning | Usage | Info | ReviewAlwaysTrueAssertConditionAnalyzer
MSTEST0035 | Usage | Warning | Usage | Info | UseDeploymentItemWithTestMethodOrTestClassAnalyzer
MSTEST0037 | Usage | Warning | Usage | Info | UseProperAssertMethodsAnalyzer
MSTEST0045 | Usage | Warning | Usage | Info | UseCooperativeCancellationForTimeoutAnalyzer

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MSTEST0051 | Usage | Info | AssertThrowsShouldContainSingleStatementAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0051)
MSTEST0052 | Usage | Warning | PreferDynamicDataSourceTypeAutoDetectAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0052)
