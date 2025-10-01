; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MSTEST0056 | Usage | Warning | TestMethodAttributeShouldNotHaveStringArgumentAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0056)
MSTEST0057 | Usage | Warning | TestMethodAttributeShouldPropagateSourceInformationAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0057)

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

### Removed Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MSTEST0006 | Design | Info | AvoidExpectedExceptionAttributeAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0006)
MSTEST0034 | Usage | Info | UseClassCleanupBehaviorEndOfClassAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0034)
MSTEST0039 | Usage | Warning | Usage | Info | UseNewerAssertThrowsAnalyzer
MSTEST0053 | Usage | Warning | AvoidAssertFormatParametersAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0053)
