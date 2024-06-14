# Adding analyzer code fix

You should add it under src/Analyzers/MSTest.Analyzers.CodeFixes.

Add your fixer logic and match the analyzer rule id with your analyzer.

## To update unit tests you should replace

`Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;` by `MSTest.Analyzers."fixerName">;`

`VerifyCS.VerifyAnalyzerAsync` by `VerifyCS.VerifyCodeFixAsync`

you can use this PR as refrence:[https://github.com/microsoft/testfx/pull/3091]
