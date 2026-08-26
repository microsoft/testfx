; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MSTEST0072 | Usage | Warning | AssemblyFixtureProviderNotSupportedWithNativeAotAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0072)
MSTEST0073 | Usage | Info | PreferConstantForResourceLockAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0073)
MSTEST0074 | Usage | Info | UndeclaredProcessGlobalStateMutationAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0074)
MSTEST0075 | Usage | Info | CurrentDirectoryMutationUnderParallelizationAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0075)
MSTEST0076 | Usage | Info | CultureMutationUnderParallelizationAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0076)
MSTEST0077 | Usage | Info | SharedFileSystemPathInTestAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0077)
MSTEST0078 | Usage | Warning | DependsOnShouldBeValidAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0078)
MSTEST0079 | Usage | Info | UseArchitectureConditionAttributeInsteadOfRuntimeCheckAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0079)
MSTEST0080 | Usage | Info | UseCIConditionAttributeInsteadOfEnvironmentCheckAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0080)
MSTEST0081 | Usage | Warning | TestFilterProviderShouldBeValidAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0081)
MSTEST0082 | Usage | Warning | InheritedMemberFromDifferentMSTestVersionAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0082)
MSTEST0083 | Usage | Info | UseExecutableConditionAttributeInsteadOfProcessCheckAnalyzer, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0083)
