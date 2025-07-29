; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MSTEST0044 | Design | Warning | PreferTestMethodOverDataTestMethodAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0044)
MSTEST0045 | Design | Warning | UseCooperativeCancellationForTimeoutAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0045)
MSTEST0046 | Usage | Info | StringAssertToAssertAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0046)
MSTEST0048 | Usage | Warning | TestContextPropertyUsageAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0047)
MSTEST0049 | Usage | Info | FlowTestContextCancellationTokenAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0049)
MSTEST0050 | Usage | Error | GlobalTestFixtureShouldBeValidAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0050)

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
