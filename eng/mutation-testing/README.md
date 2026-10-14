# Mutation-testing project inventory

Every project under `src/` must appear in `modules.json`, either as a mutation
module or with an explicit exclusion reason. `knownTargetCount` must equal the
number of source projects. The mutation workflow checks this inventory when it
builds the module matrix, and its configuration tests check it against the
repository.

Modules may set `targetFramework` to override the default `net8.0` framework
used to execute the mutation tests. The workflow passes it to Stryker and sets
the test-project targets through
`MutationTestingTargetFramework`; manual mutation builds that omit this property
continue to use `net8.0`. Select an execution TFM supported by the test projects
that can consume the mutated production assembly. The production project need
not declare the same TFM: `mstest-analyzers`, `analyzer-code-fixes`, and
`mstest-source-generation` mutate `netstandard2.0` assemblies while their tests
execute on `net8.0`.

Mutation builds retain the source MSTest analyzer for test-host compilation and
delay-sign the core with its Microsoft public key. This preserves its assembly
identity while using Stryker's supported signing path; normal builds are unchanged.

Choose test projects that exercise the mutated production assembly. Inspect both
`ProjectReference` dependencies and `<Compile Include>` source links: tests
against linked copies do not exercise mutations in that assembly. In particular,
`Microsoft.Testing.Platform.UnitTests` links selected platform source files
with `<Compile Include>` instead of referencing the core platform project.
The `platform-core` module selects `net9.0` and includes that suite as well as
`MSTest.SelfRealExamples.UnitTests`, whose `net9.0` target references the core
project and lets Stryker run those tests against the mutated platform assembly.
