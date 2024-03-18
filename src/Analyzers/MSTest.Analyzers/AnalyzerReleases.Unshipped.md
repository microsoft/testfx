; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MSTEST0017 | Usage | Info | AssertionArgsShouldBePassedInCorrectOrder, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0017)
MSTEST0019 | Design | Disabled | PreferTestInitializeOverConstructorAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0019)
MSTEST0020 | Design | Disabled | PreferConstructorOverTestInitializeAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0020)
MSTEST0021 | Design | Disabled | PreferDisposeOverTestCleanupAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0021)
MSTEST0022 | Design | Disabled | PreferTestCleanupOverDisposeAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0022)
MSTEST0023 | Usage | Info | DoNotNegateBooleanAssertionAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0023)
