; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MSTEST0058 | Usage | Info | AvoidAssertsInCatchBlocksAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0058)
MSTEST0059 | Usage | Warning | DoNotUseParallelizeAndDoNotParallelizeTogetherAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0059)
MSTEST0060 | Usage | Warning | DuplicateTestMethodAttributeAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0060)
MSTEST0061 | Usage | Info | UseOSConditionAttributeInsteadOfRuntimeCheckAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0061)
MSTEST0062 | Usage | Info | AvoidOutRefTestMethodParametersAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0062)

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-------
MSTEST0001 | Performance | Info | Performance | Warning | UseParallelizeAttributeAnalyzer
MSTEST0023 | USage | Info | Usage | Warning | DoNotNegateBooleanAssertionAnalyzer
MSTEST0037 | USage | Info | Usage | Warning | UseProperAssertMethodsAnalyzer
MSTEST0045 | USage | Info | Usage | Warning | UseCooperativeCancellationForTimeoutAnalyzer
