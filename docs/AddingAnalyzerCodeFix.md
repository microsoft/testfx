# Adding analyzer code fix

You should add it under src/Analyzers/MSTest.Analyzers.CodeFixes.

Add your fixer logic and match the analyzer rule id with your analyzer.

## To update unit tests you should replace

`Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;` by `MSTest.Analyzers."fixerName">;`

`VerifyCS.VerifyAnalyzerAsync` by `VerifyCS.VerifyCodeFixAsync`

you can use this PR as reference:[https://github.com/microsoft/testfx/pull/3091]

## Severity and enabled-by-default policy

When choosing the `DiagnosticSeverity` and `isEnabledByDefault` value for a new rule, follow this rule of thumb:

**An analyzer that is enabled by default may only use `Warning` severity when it reports a *known runtime break* — code that is already broken (or will break) at run time.**

The reason is that many of our users build with `TreatWarningsAsErrors`. A new enabled-by-default warning fails their build, so it is only justified when there is a strong, concrete reason (an actual bug in their code) rather than a stylistic preference or a conditional/latent risk.

Concretely, an enabled-by-default rule should be:

- `Warning` only when the flagged code represents a definite runtime failure or defect (for example, an invalid fixture signature that MSTest will reject at run time).
- `Info` (or lower) when the finding is advisory, stylistic, or only conditionally problematic — even if the condition is common. Latent risks that depend on configuration (for example, a data race that only manifests once in-assembly parallelization is enabled) fall here: they are real, but they are not an unconditional runtime break, so surfacing them as build-breaking warnings by default is too aggressive.

If you want a `Warning`-severity rule that is *not* a runtime break, ship it with `isEnabledByDefault: false` so consumers opt in explicitly and it never breaks a build unexpectedly.
