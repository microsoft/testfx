# Adding analyzer code fix

You should add it under src/Analyzers/MSTest.Analyzers.CodeFixes.  
Add your fixer logic and match the analyzer rule id with your analyzer.

## Updating unit tests you should replace:

 - `VerifyCS.VerifyCodeFixAsync` by `VerifyCS.VerifyAnalyzerAsync`
 - `Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;` by `MSTest.Analyzers."fixerName">;`

you can use this PR as refrence: [https://github.com/microsoft/testfx/pull/3091]
