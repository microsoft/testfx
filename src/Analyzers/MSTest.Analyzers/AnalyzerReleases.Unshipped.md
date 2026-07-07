; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MSTEST0064 | Usage | Info | PreferAsyncAssertionAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0064)
MSTEST0065 | Usage | Warning | AvoidAssertAreEqualOnCollectionsAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0065)
MSTEST0066 | Design | Info | IgnoreShouldHaveJustificationAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0066)
MSTEST0067 | Performance | Disabled | AvoidThreadSleepAndTaskWaitInTestsAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0067)
MSTEST0068 | Usage | Info | CollectionAssertToAssertAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0068)
MSTEST0070 | Usage | Warning | MemberConditionShouldBeValidAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0070)
MSTEST0071 | Usage | Info | RedundantTestMethodDisplayNameAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0071)

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-------
MSTEST0050 | Usage | Warning | Usage | Error | GlobalTestFixtureShouldBeValidAnalyzer
