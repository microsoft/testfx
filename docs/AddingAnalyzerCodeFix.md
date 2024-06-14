# Adding analyzer code fix

 - you should add it under src/Analyzers/MSTest.Analyzers.CodeFixes
 - match the rule id, add name for your fix and add your fixer logic.

## Updating unit tests you should replace:
 - `Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;` by `MSTest.Analyzers."fixerName">;`
 - `VerifyCS.VerifyCodeFixAsync` by `VerifyCS.VerifyAnalyzerAsync`

you can use this PR as refrence: [https://github.com/microsoft/testfx/pull/3091]
