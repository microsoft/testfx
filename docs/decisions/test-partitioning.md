# MSTest partition selection: research and decisions

Status: implemented; bounded local unit and packed acceptance gates passed,
2026-11-06. Remaining evidence limits are recorded below.
The user narrowed the feature to
**only `--partition` at MSTest level**. This supersedes the earlier, broader D2
proposal; it does not claim that its unimplemented orchestration/protocol work
has shipped.

## Problem and contract

Teams that cannot enable in-process parallelization should be able to run
independent selections of the same assembly in separate CI jobs without
creating extra projects or parsing human-readable discovery output.
The originating request is [#4068](https://github.com/microsoft/testfx/issues/4068).

```powershell
# Native MSTest/MTP applications; run every index with the same count and bits.
dotnet test --project .\Tests\Tests.csproj --partition 1/4
dotnet test --project .\Tests\Tests.csproj --partition 2/4
dotnet test --project .\Tests\Tests.csproj --partition 3/4
dotnet test --project .\Tests\Tests.csproj --partition 4/4

# The same option is available when invoking the built application directly.
.\Tests.exe --partition 2/4
```

Each invocation runs only its numbered selection. **The option does not start
additional processes.** CI or an external launcher controls concurrency.
There is no `--partition-count`, worker limit, new orchestrator, SDK protocol,
public grouping API, receipt/catalog format, or cross-job report aggregator.

### Input and compatibility

- Syntax is `index/count`, with positive invariant decimal Int32 values and
  `1 <= index <= count`; indexing is 1-based. Reject malformed, signed,
  embedded-whitespace, zero, out-of-range and overflowing values through normal
  CLI validation. Only ASCII digits are accepted; leading zeros are allowed.
  MTP's ordinary outer-whitespace argument normalization still applies before
  provider validation.
- `1/1` selects the same work as an ordinary run. Absence of the option keeps
  the existing discovery, execution and discovery-cache paths.
- This is native MSTest's MTP integration, not a new VSTest command-line option.
  JSON-RPC/IDE server mode and WebAssembly are explicitly unsupported in this
  first slice. Native `dotnet test` uses the ordinary existing test-host
  handshake; direct execution requires no SDK handshake.
- Existing user, runsettings, UID and MTP filter-provider constraints still
  narrow the selection. Partitioning must never broaden them. Discovery uses
  its normal discovery-context filters; execution uses Run-context filters.
  `--list-tests` is not proof that tests executed.
- **Keep normal zero-test/count policies.** An empty partition is an empty
  filtered run: exit `8` by default, or `9` when an explicit minimum is not
  met. All-skipped and programmatic drop follow existing policy. Existing
  `--ignore-exit-code` behavior remains available but is not injected by this
  feature. No intentional-empty success exception or fake passing test is added.

The user was unavailable to answer the empty-policy clarification. Retaining
existing policy is the conservative selector-only choice; transparent
intentional-empty success under every SDK is separate work.

### Selection and lifecycle

A concrete test class and all its data rows stay together, including folded,
discovery-time and execution-time data. Classes connected by resolvable declared
dependencies are co-located. Include attribute and `testconfig.json`
declarations in placement without mutating the elements later executed.
Resource locks and `[DoNotParallelize]` keep their existing process-local
meaning; they do not become distributed locks.

Compute placement from the **complete source discovery before host filtering**.
Only then intersect the assigned class set with ordinary filters. This keeps
placement stable when Retry narrows a request to failed UIDs. It does not
auto-include filtered-out prerequisites, alter directed dependency execution,
or create unmatched-declaration warnings merely because a different partition
contains a valid configured dependent.

Use the existing metadata/data discovery once; do not run a preliminary test
host or repeat discovery factories. Keep normal data expansion and result
identity in the framework rather than replaying discovery rows across processes.
Partition state is per request, not static mutable state. In-process scheduling,
fixture cleanup, cancellation, outcome counting and reporter behavior remain
ordinary MSTest behavior.

Separate invocations repeat their applicable assembly/class initialization.
Process isolation does not isolate a shared database, file, fixed port or
machine-wide resource. Users must make those fixtures independent.

### Deterministic assignment

For each dependency-connected class component, encode fields in this order:
`"mtp.partition.v1"`, the source module's filename without extension, and the
distinct concrete class full names sorted ordinally. Prefix each field with
its UTF-8 byte length as UInt32 big-endian and append its UTF-8 bytes, without
a BOM, terminator, case folding or Unicode normalization.

```text
partition(component, n) = 1 + (UInt64BE(SHA256(encodedFields)[0..8]) mod n)
```

The filename key follows the adapter's source-oriented identity and avoids
absolute checkout/deployment paths or loading assembly metadata from native
AOT executables. It is not the assembly version or a localized display name.
Placement is independent of test arrival order, row labels and unrelated
classes/components. Changing a class/component, module filename or count can
change its assignment. A hash collision affects balance, not identity.
No exact count/duration balance or linear speedup is promised.

Independent vectors for `Acme.Tests.dll`:

| Class | SHA-256 | `n=4` | `n=7` |
| --- | --- | --- | --- |
| `Acme.CheckoutTests` | `69a7b6b77714706c93cabfc95fe8e9a16f80ea87ef48ce219f5fe4c08e342e45` | 1 | 5 |
| `Acme.CartTests` | `134e357316c9c3bd8d18a561192be5c6ccdec18302c5e919405e169dd9780e94` | 2 | 7 |
| `Acme.Nested+Fixture` | `0c175d40a80712742b08686ef72c281ba461868a84a823d5c6ec68d24586795e` | 1 | 2 |

More partitions than components can leave empty selections. The implementation
must not allocate an array proportional to the user-supplied partition count.

### Retry composition

`--partition i/n --retry-failed-tests k` keeps Retry as the sole process
orchestrator. Retry retains the partition option when relaunching; subsequent
attempts use their failed-UID constraint and the same full-source placement.
Never rebuild component keys from only the failed subset.

Retry percentage/count thresholds apply to that invocation's selected work,
just as they do with an ordinary filter, not all tests physically present in
the assembly or other CI jobs. MSTest's in-process `[Retry]` is unchanged.
No automatic local partition fan-out or nested-orchestrator composition is
included in this PR.

## Research and alternatives

- [Issue discussion](https://github.com/microsoft/testfx/issues/4068) asks for
  numbered CI selections and also discusses process-level local scheduling.
  They are distinct features; only the former is in this implementation.
- [Youssef's prototype](https://github.com/Youssef1313/MTPSharding/blob/398ba09ebbdf4da4136230cb595644034d3b62f0/docs/Design.md)
  demonstrates the discover/partition/run orchestration approach. It remains a
  valid MTP-level candidate, not something ruled out by choosing this first slice.
- Native MSTest has neutral elements before host filtering in
  [UnitTestDiscoverer](../../src/Adapter/MSTestAdapter.PlatformServices/Discovery/UnitTestDiscoverer.cs).
  It can form stable class/dependency selections without a discovery child,
  UID replay or a cross-framework API.
- [Filter providers](../RFCs/020-Test-Execution-Filter-Providers.md) can distinguish
  discovery and run. Use the normal request path rather than inventing a
  discovery snapshot of all future execution decisions.
- [Dependencies](../RFCs/022-Test-Dependencies.md) require placement before filter
  narrowing while preserving the executor's existing selected-test semantics.
- [NUnit](https://docs.nunit.org/articles/nunit/running-tests/Partitioning-Tests.html)
  and [Playwright](https://playwright.dev/docs/test-sharding) demonstrate the
  distinction between deterministic assignment, fixture granularity and duration
  balance.

Rejected for this slice: a discover/replay orchestrator (more process/lifecycle
scope), method/row splitting (fixture/data affinity), post-filter component
hashing (Retry can move a failed test to another bucket), and independent local
partition/concurrency knobs without a demonstrated workload need.

The earlier broader design was reviewed in three rounds by Claude Opus 5.5 and
Gemini 4 Argon, with GPT-6.1 Sol synthesis. Its accepted technical snapshot was
`5a0dd5c32c6c48976611e519ae59c9ddf69ded18`; the full review record remains in
the version history at `42f2e6666a91a8c9fbc6aa5eebd2cc721c5d2f49`.
Those reviews do not constitute a review of this implementation.

Important later clarifications: MTP ownership is not intrinsically problematic;
basic discovery/replay can work for selectable stable discovery units. Automatic
multi-process fan-out needs orchestration wherever selection is implemented.
The user then explicitly chose only MSTest `--partition` for the first PR.

## Implementation and validation

Producer: `MSTest.TestAdapter` and its bundled
`MSTestAdapter.PlatformServices`, aligned with the repository's existing
`MSTest.TestFramework`/MTP packages. Consumers are direct native test
applications and native MTP `dotnet test`. No SDK, transport, reporter or package
dependency change is required. Target the current mainline MSTest 5/MTP 3
source train; no stable backport or automatic downstream enablement is implied.
The PR author owns this slice through review; release/backport decisions follow
normal maintainer approval.

| Requirement | Planned decisive evidence |
| --- | --- |
| Parsing and deterministic placement | Exact hash vectors, invalid/boundary inputs, `1/1`, shuffled input and relocated paths. |
| Full coverage without overlap | Union all partitions and compare exact class/test identities to the ordinary run; pairwise intersections are empty. |
| Data and dependency affinity | Multiple distinct rows, runtime/folded data, attribute/config dependency components, filtered bridges and UID-narrowed Retry. |
| Filter and normal-policy preservation | Category/runsettings/UID/provider intersections, legitimate drop/skip, default run, empty `8`, explicit minimum `9`, and option-absent behavior. |
| Real shipping consumer | Freshly packed local NuGets, isolated fixture restore, direct and native SDK runs, exact loaded/resolved package provenance, TRX identities/outcomes and help/info output. |
| Regression sensitivity | Same tests retained in an isolated compiling comparison with the selector changed to select all; intended assertions fail, then pass with the feature. |

Use the pinned SDK through the repository scripts. Pack before package-consuming
acceptance runs and repack after producer changes. Record exact commands,
SDK/packages/source states, binlogs, report paths, selected/executed/pass/fail/skip
counts, inner/outer exits and the intended counterfactual assertion failures.
Do not treat build failures, skipped/zero tests or green CI alone as regression
sensitivity.

### Implementation log

- Implemented the parser/planner, pre-filter discovery selection, native option
  registration, ordinary filter/Retry composition, and unchanged count policies.
  The original constructor and static discovery-filter entry point remain
  available to existing internal callers.
- Source `net10.0` compilation succeeded with the pinned SDK. The owning
  project's `UpdateXlf` generated all 13 catalogs; the localization regression
  suite (41 tests) and the 364-catalog guard passed.
- Independent code-review round 1 inspected
  `4a42a14dc6fa12d0b47633cd03db9466d13e9ec9`: Claude Opus 5.5 and Gemini
  4 Argon found no blocking correctness defects. Follow-up fixes clear stale
  diagnostic state on manager reuse and guard deliberate cache/factory bypass.
- Both reviewers noted unnecessary row-level cloning for configured placement.
  The implementation now resolves class edges through the existing private
  dependency-reference parser and a class/method index, without cloning
  elements or expanding configuration arrays per data row. Attribute edges
  are not discarded merely because methods have the same name: overloads can
  declare different prerequisites.
- Multi-target compilation covered `net462`, `net10.0`, `net11.0`, modern UWP
  and WinUI. It caught an older-TFM style diagnostic in the SHA-256 fallback,
  which was corrected. Classic UWP requires full Visual Studio MSBuild, absent
  on this host; local packing uses the repository's supported
  `SkipUapTargetFramework=true` build setting and must not be presented as a
  complete classic-UWP shipping-package validation.
  The full Windows file-count guard consequently reports omitted classic-UWP
  payloads (`MSTest` 9/10, adapter 64/76, framework 88/105). This is an explicit
  local validation gap, not a passed full-Windows package check; these partial
  local test packages are not for publication.
- This host cannot resolve bare `powershell` from the repository's cmd/Exec
  wrappers, despite the full executable being present. SDK/runtimes were
  initialized through the same repository scripts using native PowerShell.
  Post-pack validation is invoked explicitly through its native entry point;
  no repository build script or validation rule is weakened.

The focused Release/net10 unit gate passed **34/34**: 21 parser/planner cases,
12 native-option/discovery/factory cases and one manager-reuse regression.
TRX, JUnit XML and CTRF JSON exports were parsed for all three scopes with no
missing, duplicate, skipped or failed cases. The overload regression preserves
both same-named methods' different dependency edges, rather than incorrectly
deduplicating by method name.

Code-review round 2 inspected
`3be09024cb5759473ecf2bb6cb8efb19a84ccab6` against the original production
baseline `a8a62a02627b9e9d9c33949d06a1540f84b78bc4`. Claude Opus 5.5 and
Gemini 4 Argon both approved with no remaining code/test findings. Those were
read-only static reviews, not executed acceptance or mutation evidence.

### Executed unit sensitivity

The isolated comparison retains the tests from
`3be09024cb5759473ecf2bb6cb8efb19a84ccab6`. These are compiling targeted
counterfactuals, not tests run on the pristine production baseline.

| Contract | Counterfactual | Without behavior | Restored implementation |
| --- | --- | --- | --- |
| `TestPartitionTests` selection contracts | Change the valid-input fast path from `Count == 1` to `Count > 0`, selecting all tests. Patch SHA-256 `419C6FB7065F97DC6E406718C7DF96FE64590D810C209208C84AAA61EA29A863`. | Executed: 21, passed 5, failed 16, skipped 0; exit 2. Golden-vector, affinity and disjointness assertions detect the missing selection. | Same 21 source-declared identities: passed 21, failed/skipped 0; exit 0. |
| `TestsBasedRunAfterPartitionedSourceRun_UsesCurrentDependencyDiagnosticSelection` | Remove only the explicit-element entry point's `_unpartitionedTests = null` reset. Patch SHA-256 `70DD4A5358B3ED5E8AA9F1011FA02E7C24A5B24D6BE63F66520FA9FE955356A4`. | Executed: 1, failed 1, skipped 0; exit 2. The assertion expects one genuine missing-prerequisite warning but observes two, including a false unmatched-dependent warning from the previous request. | Same test: passed 1, failed/skipped 0; exit 0. |

Both comparisons used Release/net10.0, the pinned
`12.0.100-alpha.1.26509.101` SDK, fresh test-project builds, unique binlogs and
separate TRX/JUnit/CTRF result directories. The selectors were respectively
`--treenode-filter '/*/*/TestPartitionTests/*'` and
`--treenode-filter '/*/*/ExecutionManagerContinuationTests/TestsBasedRunAfterPartitionedSourceRun_UsesCurrentDependencyDiagnosticSelection'`.
TRX identities were reconciled with source declarations; there were no skips
or duplicate execution IDs. Test assertions were not changed between states.
The correct source was restored after each experiment.

### Packaged-consumer execution gates

The source-only producer pack passed with warnings treated as errors. The final
bounded local gate passed **35 distinct unit tests and 34 packed acceptance
cases**, with no failures or skips. Acceptance discovery selected exactly the
same 34 source-planned identities: 28 partition cases and six existing help/info
cases. The outer project used its declared net12.0; generated partition
consumers used net11.0, with help/info additionally exercising net10.0 and
net462. The real pinned SDK was `12.0.100-alpha.1.26509.101`.

The producer remained
`3be09024cb5759473ecf2bb6cb8efb19a84ccab6`. The validated test/support delta
has SHA-256
`50BF541DEA4243C0183C00A4EB8D2D7FE60F752DA06D86EAFA432735812A3FA7`;
it changes only the two partition test files. The final selector container has
22 passing methods, including independent 255/256/257-byte framing vectors
and an unresolved-dependent-method case. These two additions received bounded
static gap/assertion review and green execution, not new executed mutation
proof. The 21-test paired selector evidence above remains bound to its
original unchanged tests.

| Requirement | Executed acceptance evidence |
| --- | --- |
| Complete, disjoint class selection and matching discovery/run identities | `Partition_DiscoveryAndRun_SelectSameDisjointClasses` and `Partition_AbsentAndOneOfOne_PreserveExactBaselineIdentitiesAndOutcomes`. |
| Native SDK and reflection/generated metadata | `Partition_NativeDotnetTest_MatchesConsoleSelection` and `Partition_MetadataModes_PreserveClassAndDependencyPlacement`. |
| Dependency affinity and UID-narrowed Retry | `Partition_AttributeAndConfiguredDependencies_KeepFullComponentTogether` and `Partition_RetryUidNarrowing_DoesNotMoveDependencyComponent`. |
| Data rows and programmatic Run/Skip/Drop | `Partition_DataRows_RemainWithTheirConcreteClass` and `Partition_ProgrammaticDropAndSkip_AreNotBroadened`. |
| Existing empty/minimum/override policy | `Partition_EmptyBucket_PreservesExitEight`, `Partition_MinimumExpectedTests_UsesSelectedCountAndExitNine` and `Partition_IgnoreExitCode_PreservesExistingPolicy`. |
| CLI input and help/info | `Partition_CommandLineWhitespaceNormalization_PreservesExistingPolicy`, eleven invalid-option cases and the existing registered-extension help/info tests. |

The common MTP parser trims outer argument whitespace before provider
validation; the option preserves that policy. The direct partition parser
still rejects raw whitespace, and embedded CLI whitespace remains invalid.
The programmatic filter acts at invocation, so discovery can include a test
that execution later drops. Retry's top-level TRX describes the final attempt;
the acceptance test separately asserts both attempt maps, stable UIDs and all
final logical outcomes. These are existing consumer contracts, not feature
behavior changes made to obtain green tests.

Fresh consumer resolution was checked against current package SHA-512 values
in ten `project.assets.json` files. SHA-256 comparison matched 1,428 physical
runtime-output DLL files to resolved package payloads. The 73 recorded
partition child invocations returned `0` (48), `5` (11), `8` (11) and `9` (3);
the negative child exits were asserted by passing outer tests. Output-bit
agreement is not presented as an independent loaded-module inspection.
TRX, JUnit and CTRF identities/outcomes were reconciled, and the repository's
execution verifier confirmed planned/selected/executed/passed = 34, with
unique execution IDs and outer exit 0.

The decisive acceptance command was:

```powershell
# $results and $binlog name unique, retained artifact paths.
& .\.dotnet\dotnet.exe test --project test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Release -p:SkipUapTargetFramework=true -p:UsingDotNetTest=true -p:EnableCodeCoverage=false -p:TreatWarningsAsErrors=true -p:MSBuildTreatWarningsAsErrors=true "-p:ArtifactsTestResultsDir=$results\" "-bl:$binlog" --filter 'FullyQualifiedName~PartitionTests|FullyQualifiedName~HelpInfoTests' --no-ansi --no-progress --show-test-results all
```

Result: exit 0, 34 passed, zero failed/skipped. The matching independent
discovery used `--list-tests text` with the same filter. Runtime package
families were MSTest `5.0.0-dev` and MTP `3.0.0-dev`; eager fixture dependencies
were packed from unchanged source rather than bypassing package lookups.

Initial failed attempts exposed outer-TFM, eager-package, comparison-local
toolchain-path and generated filter-type qualification problems. Those setup
and compilation failures are not counted as behavioral red evidence. The
corrections do not change production code, repository build policy, shared
caches or assertions about the feature.

**Remaining limits:** packed-consumer counterfactual red/green, independent
loaded-module inspection and an interactive rendered-terminal demonstration
have not been completed. Classic-UWP/full-Windows package validation remains
the host/CI gap described above. The earlier standalone PowerShell/Node
experiments confirmed hash vectors and ordering only, not product execution.

### Review correction: acceptance source-generation opt-out

Review identified that the new fixture and its fixed metadata data rows ignored
the existing `MSTEST_ACCEPTANCE_SKIP_SOURCEGEN=1` acceptance-harness policy.
Both now reuse `MetadataModesToRun`: fixture builds consume that list, and
`[CombinatorialData]` with `[MetadataModeValues]` selects matching test cases.
This changes acceptance configuration only, not the product's partition UX.

At `90c9d074cc49192c963a3661d51d2b7c46494a0d`, discovery with the opt-out set
still selected all three modes. With the correction, the same discovery selects
only Reflection. This is executed discovery/configuration evidence, not an
old-revision failed-execution or mutation claim.

The focused acceptance method
`Partition_MetadataModes_PreserveClassAndDependencyPlacement` then passed
**1/1 with the opt-out** and **3/3 with it unset**, with no failures or skips.
Both runs used the pinned SDK, Release/net12.0 outer host and freshly generated
net11.0 consumers. Each run's canonical execution gate reconciled exact
source-planned mode names, unique execution IDs and exit 0.
Retained build manifests confirm that both independent and dependency assets
built only Reflection under the opt-out, with no `bin/SourceGen` or
`bin/AotSourceGen` directory; the default run built and executed all three
modes. Resolved adapter/framework/platform package hashes matched the current
unchanged producer packages.

The correction's test-file patch against that revision has SHA-256
`D9C1D48C995E315D8CE2C60AF323AC64A97361790A3DFA433E51A120C21FFF98`.
Both execution commands used the acceptance command above with
`--filter 'FullyQualifiedName~Partition_MetadataModes_PreserveClassAndDependencyPlacement'`
and separate fresh results/binlogs. The environment value was set or removed
only in the owning validation process. No assembly scheduling, assertions,
production files or shared/global environment were changed.

### CI compilation corrections

[Azure build 1668030](https://dev.azure.com/dnceng-public/cbb18261-c48f-4abb-8651-8cdcb5474649/_build/results?buildId=1668030)
tested PR head `ef14255e79617000327243bd2421f320d1c9013b` through merge
commit `e3add7f9ccaec3fcaf3410b2ff5719d971b5ee07`. All four failing build
legs were traced to the new test files, not a transient service failure:

- The Windows legs also compile net462/net48 tests. Three array
  `Reverse().ToArray()` expressions bound to a void-returning extension on
  those targets, producing CS0023/CS0030. Explicit
  `Enumerable.Reverse(tests).ToArray()` preserves the intended non-mutating
  reversed sequence on every target. A direct local net48 build reproduced
  the failure; the previously green net10/net11 runs did not cover it.
- Two multiline collection expressions in the discovery/TRX readers failed
  SA1500/SA1413. Their opening/closing delimiters and trailing commas now follow
  the repository format. The same six analyzer errors also caused the
  [shipping-attachments workflow failure](https://github.com/microsoft/testfx/actions/runs/42261351459/job/128041798600).

The earlier Azure build 1668021 was canceled, not a passing baseline; its merge
commit was `4f898f42afcbe668aa811b95ee661adad3ec07c3`. The failed build above
is the completed failure used for this correction.

Validation used the pinned SDK and unchanged assertions:

- A forced analyzer-enabled Rebuild of the owning acceptance project passed
  with zero warnings/errors. Existing, already-built references were reused
  with `BuildProjectReferences=false`; analyzers were not disabled.
- `TestPartitionTests` passed **22/22 on each of net462, net48, net10.0 and
  net11.0**. This includes
  `SelectTests_ReorderedDiscoveryAndCheckoutPaths_PreserveClassPlacement`,
  `SelectTests_TransitiveAndCyclicDependencies_UseOneSortedComponent` and
  `SelectTests_SameNamedOverloadsWithDifferentDependencies_CoLocateEveryTargetClass`.
- Packed `Partition_DiscoveryAndRun_SelectSameDisjointClasses` passed **1/1**
  through the formatted discovery/TRX helpers, with the existing source-gen
  opt-out set only for this process. Exact result identities, disjointness and
  associated outcomes remained asserted.

Each test invocation exited 0; TRX/JUnit/CTRF exports and exact source-planned
counts were verified, with no failures or skips. The two-test-file correction
against that PR head has SHA-256
`2036D8E158D4AD16F0E7679C387C54F446266E1B339D3E81A472C5E19FF8EC72`.
These are compilation/formatting repairs, not new product behavior or a
behavioral red/green claim. CI must still rerun on the pushed correction.

### Base merge and validation blocker

Merged main `0801b9574b165c714801ce56256f774c9619f9d7` in
`9ade8703ec4a7a8f5614306664a89fc553329df5`. The two text conflicts were
additive: retain both partition and expected-duration changelog entries, and
both sets of internal API declarations. The automatic framework merge retains
partition-aware manager creation/cache bypass and main's durable-result-file
lifecycle changes.

The API-owning PlatformServices project built successfully with warnings as
errors on net462, net10.0, net11.0, modern UWP and WinUI, with classic UWP
excluded by the existing local host limitation. Restoring the merged package
versions succeeded, but the configuration-matched producer repack failed on
SA1500/SA1413/SA1137 in
`RetryOrchestrator.OrchestrationSupport.cs:213-228`. That file's blob is
`59bf30fe55bae42a20871b115d3c83e03b9c5c0f` in both target main and the
merged branch; this PR does not modify it.

The unrelated upstream Retry formatting was not changed or suppressed.
Post-merge package-consuming validation is blocked, and the partial pack
outputs must not be used as if they were a complete aligned package set.
Earlier acceptance results remain evidence for their recorded revisions,
not executed proof of this merged revision.

### CI eligibility

Static inspection finds the acceptance project in both `TestFx.slnx` and
`NonWindowsTests.slnf`. Windows Release uses
`eng/pipelines/steps/test-windows-configuration-tests.yml`; Linux/macOS Release
use `eng/pipelines/steps/test-non-windows.yml`. Their normal full-test commands
select the solution without excluding this class, and the calling jobs honor
`SkipTests`. The shared environment pins `.dotnet`, the matching `.packages`
cache and `DOTNET_CLI_CONTEXT_VERBOSE=1`. This establishes eligibility, not an
executed CI result; no CI-verified claim is made before publication.
