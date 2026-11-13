# MSTest SDK composition: layer 3

## Problem and contract

This layer replaces the SDK/helper/composition portion of
[#12435](https://github.com/microsoft/testfx/pull/12435), following
[#7225](https://github.com/microsoft/testfx/issues/7225),
[#12412](https://github.com/microsoft/testfx/pull/12412), the
[shared-core layer](shared-core-ui-contract.md), and the
[explicit-package layer](explicit-mstest-packages.md).

`MSTestApplicationModel` accepts `Default`, `Uwp`, or `WinUI`. An empty value
preserves automatic inference, including early UWP inference and explicit
`UseUwpTools=false`. Selection is recomputed for each inner target framework;
the public property is never filled with an inferred outer-build value.
The override selects packages, not UI tooling. Invalid or conflicting choices
fail explicitly. ClassicEngine, NativeAOT, VSTest, helpers, and central package
management use the same selected composition.

Helpers retain required compile-time UI APIs and generated runtime hooks, but
their adapter references are private, exclude runtime assets, suppress manual
copy-local payloads and satellites, and do not register the MSTest application
builder hook. The stable hook GUID/order and unrelated MTP dependency hooks
remain intact. Applications own their selected runtime payload.

Adapter validation uses `ResolvePackageAssets` and `PackageDependencies` for
the current inner TFM, including ProjectReference-transitive packages whose
build assets were excluded. Non-SDK projects retain imported-marker checks.
All three conflicting pairs fail; separate ordinary/UI inner TFMs remain valid.
Exact UI/core ranges and package/runtime ownership belong to lower layers and
are unchanged here.

The producers are the aligned Release `MSTest.Sdk`, `MSTest.TestAdapter`,
`MSTest.Uwp`, and `MSTest.WinUI` packages. Generated SDK and direct-package
consumers exercise their real packed imports, resolved assemblies, copied
payloads, and helper nuspecs. MTP launch/activation, handshake/no-handshake,
cancellation, fallback, exit aggregation, and disposal responsibilities do not
change. Native/Bazel parity and real app activation are explicitly outside this
layer. The series coordinator owns rollout and release-branch/backport decisions;
this layer does not enable a downstream or servicing release.

## Research and decision

2026-11-13: the clean parent is
`f9708f5edbd37215b7e53cef840ef1e29ddf3f93`. The frozen source ref and live
#12435 head are `00428d48c212cf3630dcf6fa31d131c5ae745c98`; the carving input is
`8dd78e1347417e400c3ef8d786059a8bb346162f...refs/pr-stack/source/12435`.
Reconcile that bounded slice with the parent rather than replacing whole files.

No change would retain leaking source-generated helper dependencies and allow
ambiguous adapter packages. Checking only imported markers is simpler, but misses
excluded build assets and transitive packages. Scanning every assets target
would falsely reject valid multi-targeting. The chosen package-task output
already models the current inner target and avoids a new assets parser.
Moving only MSTest's hook registration from props to targets lets the project
body's `IsTestApplication=false` participate without removing dependency hooks.

## Validation and demonstration

| Requirement | Automated evidence | Surface / limitation |
| --- | --- | --- |
| Explicit, inferred, invalid, and conflicting models | `MSTestSdk_ExplicitApplicationModel_SelectsCompositionBeforeLateToolingImports`, `MSTestSdk_UwpToolingOptOut_RespectsExplicitApplicationModel`, `MSTestSdk_ApplicationModel_RejectsInvalidOrConflictingChoices` | Real packed SDK imports and resolved package/reference identity |
| Every injection path and CPM | `MSTestSdk_AdapterInjection_UsesSelectedCompositionWithCentralPackageManagement`, existing SDK/CPM and source-generation cases | Package selection, generated hooks, and real ordinary execution |
| Helpers do not contaminate applications | `MSTestSdk_Helper_KeepsOnlyRequiredCompileTimeComposition`, `MSTestSdk_UiApplication_WithOrdinarySourceGeneratedHelper_DoesNotResolveOrdinaryAdapter`, `MSTestSdk_SourceGenerationOptIn_DoesNotFlowTransitively` | Compile, copy-local/output inspection, copied hash identity, helper nuspec |
| Conflicts and valid multi-TFM closure | `ExplicitPackages_RejectConflictingAdaptersBeforeCompilation`, `MSTestSdk_MixedOrdinaryAndUiTargetFrameworks_SelectOneCompositionPerInnerFramework` | Direct/excluded/ProjectReference-transitive pairs and independent inner closures |
| Classic UWP and ordinary net462 compatibility | Existing targeted SDK cases and `MSTestSdk_ClassicUwpTargetFramework_InfersUwpComposition` | Desktop MSBuild where required; activation remains lower-layer evidence |

The acceptance assembly uses method-level, CPU-count parallelization. Every new
case generates a fresh asset directory; shared package inputs are immutable
during a run and the assembly fixture chooses an isolated cache. No test
scheduling declarations change. This build-policy layer is nonvisual; diagnostic
and resolved-asset transcripts are more useful than screenshots.

The baseline/current packs, exact commands and exits, revision/patch/package
hashes, selected/executed identities, retained-test sensitivity, provenance, and
gaps will be recorded here as the experiments finish. Historical unsplit-source
runs do not establish this layer's proof.

2026-11-13 build results: `.\build.cmd -pack -c Release -msbuildEngine vs
-projects artifacts\validation\layer3-pack.slnf -bl` built the 42 actual source
entries from `TestFx.slnx` at the clean parent and exited **0**, with **0 warnings
and 0 errors**. Its four composition packages are preserved separately for the
retained-test comparison. After the policy/test edits, the same command using
`artifacts\validation\layer3-delivery.slnf` (the same 42 entries plus the exact
MSTest acceptance owner) exited **0**, with **0 warnings and 0 errors**.
Both packs passed the package-content/metadata verifier. The outer acceptance
host targets `net12.0`; its generated consumers retain their own declared TFMs.
The pinned SDK is `12.0.100-alpha.1.26556.115`. The build script bootstrapped a
task-owned `.dotnet` directory; the borrowed worktree and its outputs were not
modified. Focused consumer execution is still pending at this source checkpoint.

## Open questions

CI execution and release-branch validation are not established by local runs.
Do not repair the unrelated native net462 System.Reflection.Metadata baseline.
