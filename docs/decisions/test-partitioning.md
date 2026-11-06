# MSTest partition selection: research and decisions

Status: implementation in progress, 2026-11-06. The user narrowed the feature to
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
  `1 <= index <= count`; indexing is 1-based. Reject malformed, signed, spaced,
  zero, out-of-range and overflowing values through normal CLI validation.
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

Packed acceptance, paired counterfactual results and final review outcomes are
still pending. The earlier standalone PowerShell/Node experiments confirmed
the hash vectors and ordering properties only; they are not product evidence.
