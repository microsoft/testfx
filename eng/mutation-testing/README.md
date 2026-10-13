# Mutation-testing project inventory

Every project under `src/` must appear in `modules.json`, either as a mutation
module or with an explicit exclusion reason. `knownTargetCount` must equal the
number of source projects. The mutation workflow checks this inventory when it
builds the module matrix, and its configuration tests check it against the
repository.

Choose test projects based on direct project references, not only by following
test-to-production `ProjectReference` edges. In particular,
`Microsoft.Testing.Platform.UnitTests` links selected platform source files
with `<Compile Include>` instead of referencing the core platform project.
The `platform-core` module includes that suite as well as
`MSTest.SelfRealExamples.UnitTests`, which references the core project and lets
Stryker run those tests against the mutated platform assembly.
