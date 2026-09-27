; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 4.4.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MSTEST0069 | Usage | Warning | InheritedTestClassAttributeWithSourceGeneratorAnalyzer
AOTSG0001 | MSTest.AotReflection | Warning | StaticTestClass
AOTSG0002 | MSTest.AotReflection | Warning | GenericTestClass
AOTSG0003 | MSTest.AotReflection | Warning | InaccessibleTestClass
AOTSG0004 | MSTest.AotReflection | Warning | GenericTestMethod
AOTSG0005 | MSTest.AotReflection | Warning | ByRefParameter
