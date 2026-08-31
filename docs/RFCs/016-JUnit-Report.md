# RFC 016 - JUnit XML report extension

- [x] Approved in principle
- [ ] Under discussion
- [ ] Implementation
- [ ] Shipped

## Summary

Introduce **`Microsoft.Testing.Extensions.JUnitReport`**: a new Microsoft.Testing.Platform (MTP) extension that emits a JUnit-style XML report at the end of a test run. The report follows the Jenkins/Surefire `<testsuites><testsuite><testcase>` schema that is consumed by Jenkins, GitLab CI, GitHub Actions test reporters, Azure DevOps, CircleCI, TeamCity, and most other CI tooling. The extension ships with the standard MTP integration surface (CLI option, MSBuild auto-registration, `AddJUnitReportProvider` builder method) and addresses [#4268](https://github.com/microsoft/testfx/issues/4268).

## Motivation

JUnit XML is the de facto interchange format for test results in CI ecosystems. Every modern CI tool can ingest it; nearly all reporting/badging/flaky-test services consume it. Today, MTP-based projects must either:

1. Use the TRX report and convert it via a third-party tool, or
2. Switch back to the VSTest test host just to get `--logger junit` from the VSTest JUnit logger.

Both are friction the platform should remove. xUnit's MTP-native runner already ships its own `--report-junit` (renamed to `--report-xunit-junit` in 4.0), proving the demand. Shipping a first-party MTP extension means **any** test framework that consumes MTP messages (MSTest, NUnit MTP runner, custom frameworks) gets JUnit XML output by setting a single property.

## Goals

- Produce a JUnit XML report that validates against the widely-used Jenkins/Surefire schema and is accepted by Jenkins, GitLab CI, GitHub Actions test reporters, and Azure DevOps without manual post-processing.
- Mirror the user experience of `--report-trx` / `--report-html`: a single switch to enable, an optional `--report-junit-filename` for path/name customization.
- Auto-register through the existing MSBuild known-extension mechanism so `dotnet build /p:EnableMicrosoftTestingExtensionsJUnitReport=true` is enough.
- Preserve MTP's hierarchical `TestNode` tree in a way that **flat-only** JUnit consumers can still ingest the report.

## Non-goals

- Reverse-compatibility with the legacy VSTest JUnit logger output, byte-for-byte. Where the legacy logger and the Surefire schema disagree, we follow the schema.
- A pluggable schema. The output targets the Jenkins/Surefire flavor exclusively; alternative flavors (e.g. JUnit 5 platform XML) are out of scope.
- Nested `<testsuite>` (see [Tree of tests](#tree-of-tests) below). May be revisited in a future RFC behind an opt-in switch.

## Naming

- CLI options: `--report-junit` (enable) and `--report-junit-filename` (name/path override).
- Public API: `Microsoft.Testing.Extensions.JUnitReportExtensions.AddJUnitReportProvider(this ITestApplicationBuilder)`.
- Package: `Microsoft.Testing.Extensions.JUnitReport`.

### Short-term naming conflict with xUnit pre-4.0

xUnit v3 (pre-4.0) ships its own `--report-junit` option from its MTP runner. **MTP's CLI validator treats duplicate option names across providers as a fatal error** (`CommandLineOptionsValidator`). Concretely:

- An app that registers **both** our `JUnitReportGeneratorCommandLine` and the xUnit JUnit provider will fail validation at startup with a duplicate-option-name error. This is intentional MTP behavior.
- Users typically opt into report extensions via the MSBuild known-extension mechanism. Two simultaneous JUnit registrations are unlikely in practice, but the conflict is real and must be called out.
- xUnit 4.0 renames its option to `--report-xunit-junit`, removing the conflict permanently. We accept the short-term overlap; if a user hits it, they pick one provider for the run.

The RFC author and reviewers acknowledge this trade-off explicitly. We choose the name that aligns with sibling MTP extensions (`--report-trx`, `--report-html`) over the name that avoids the temporary collision.

## Schema choice

We target the **Jenkins/Surefire** JUnit XML flavor (the schema published at `jenkins-junit.xsd`):

```xml
<testsuites name="..." tests="N" failures="N" errors="N" skipped="N" time="..." timestamp="...">
  <testsuite name="..." tests="N" failures="N" errors="N" skipped="N" time="..." timestamp="..." hostname="..." id="0">
    <properties>
      <property name="..." value="..."/>
    </properties>
    <testcase classname="..." name="..." time="..." >
      <properties>
        <property name="testpath" value="A/B/C/D"/>
        <property name="uid" value="..."/>
        <property name="trait.Category" value="..."/>
      </properties>
      <skipped message="..."/>                  <!-- 0..1 -->
      <error message="..." type="...">...</error>      <!-- 0..n -->
      <failure message="..." type="...">...</failure>  <!-- 0..n -->
      <system-out>...</system-out>              <!-- 0..n -->
      <system-err>...</system-err>              <!-- 0..n -->
    </testcase>
    <system-out>...</system-out>
    <system-err>...</system-err>
  </testsuite>
</testsuites>
```

**Element ordering inside `<testcase>` is normative**: `properties?, skipped?, error*, failure*, system-out*, system-err*`. Stricter consumers (and some IDEs) reject documents that emit elements out of order.

### JUnit XML flavors compared

"JUnit XML" is not a single specification — it is a family of dialects that grew organically from Ant's original `junitreport` task. The most commonly encountered flavors:

| Flavor                          | Root element     | Nested suites | `<properties>` placement                 | Retry / rerun support                         | Output layout              | Notes                                                                                                                                                                |
| ------------------------------- | ---------------- | ------------- | ---------------------------------------- | --------------------------------------------- | -------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Jenkins / Surefire** *(chosen)* | `<testsuites>`   | Discouraged   | Per-suite **and** per-testcase           | None native (handled per source — see [Retry handling](#retry-handling)) | Single file                | The most portable, broadest-consumer flavor. Accepted by Jenkins JUnit plugin, GitLab CI, Azure DevOps, GitHub Actions reporters, CircleCI, TeamCity, Bamboo, codecov-like services. |
| **Maven Surefire (legacy)**     | `<testsuite>`    | No            | Per-suite, optionally per-testcase       | None native                                   | One file per suite         | Surefire's on-disk layout. Equivalent on the wire to Jenkins/Surefire but split across files; many parsers tolerate both.                                            |
| **Maven Surefire 3.x (`<rerunFailure>`)** | `<testsuites>` | No | Per-suite **and** per-testcase | `<rerunFailure>` / `<rerunError>` / `<flakyFailure>` child elements of `<testcase>` | One file per suite | Adds explicit rerun children. Only a small fraction of consumers parse these elements; the rest silently ignore them. Not portable. |
| **JUnit 5 Open Test Reporting** | `<events>`       | n/a (event stream) | n/a (attributes on events)         | First-class                                   | Single file (event log)    | A completely different schema (event-based, not result-based). Designed for IDE consumption, not CI reporters. None of the major CI services parse it.               |
| **xUnit.net's `--report-junit`** | `<testsuites>`   | No            | Per-testcase only                        | None                                          | Single file                | Targets the Jenkins/Surefire shape but skips some optional metadata (no per-suite `<properties>`, no `<system-out>` mirror at suite level). Will be renamed to `--report-xunit-junit` in xUnit 4.0. |
| **pytest's `--junitxml`**       | `<testsuites>` (with `junit_family=xunit2`) or `<testsuite>` (legacy `xunit1`) | No | Per-testcase | None native — flaky output via `<system-out>` / properties | Single file                | The default `xunit2` family is functionally a Jenkins/Surefire dialect with a few pytest-specific extras (`<properties>` for fixtures, `file`/`line` attributes).    |
| **NUnit 2 XML**                 | `<test-results>` | Yes           | n/a (different schema)                   | None                                          | Single file                | Predates the JUnit consensus. Some parsers accept it via auto-detection; most do not. Out of scope for an interop format.                                            |
| **testmoapp / testmo-junit**    | `<testsuites>`   | No            | Per-testcase (rich, opinionated)         | None documented                               | Single file                | A vendor-specific superset. Its [JUnit XML reference](https://github.com/testmoapp/junitxml) documents no rerun/flaky element — flakiness is derived by the service from run history, not encoded in the XML. Strict supersets of Jenkins/Surefire are safe; we do not adopt their extensions. |

The trade-offs in one line: **portability ↔ expressiveness**. Jenkins/Surefire is the largest-common-denominator that every mainstream CI ingests verbatim; everything else either loses a chunk of consumers or carries data the consumer will not display anyway. Where MTP needs to express something the flat schema lacks (test tree, retry, traits), we encode it via standard `<property>` children so portable consumers see uniform `<testcase>` rows while richer consumers can dig deeper.

## Tree of tests

MTP exposes parent-child test relationships via `TestNodeUpdateMessage.ParentTestNodeUid`. The natural mapping would be **nested `<testsuite>` elements**.

**This is portability-hostile.** Jenkins's JUnit plugin, GitLab CI's test report parser, Azure DevOps's test results processor, and most third-party badges/reporters explicitly do not descend into nested suites. A report that nests will silently lose half the results in those tools.

### v1: flat suites + `testpath` property

The shipped output uses a **flat** `<testsuites><testsuite>...</testsuite></testsuites>` structure (one `<testsuite>` per discovered test class, see [Suite grouping](#suite-grouping)). Hierarchy is preserved as a `<property name="testpath" value="Root/Container/Subcontainer/MyTest"/>` inside each `<testcase>`'s `<properties>` block.

To compute `testpath` correctly, the consumer must track `ParentTestNodeUid` for **every** `TestNodeUpdateMessage`, not only terminal results. Container nodes typically arrive as `Discovered`/`InProgress` updates and **never appear as terminal results** themselves. If we only retained terminal nodes (as HtmlReport does today), parent-chain resolution would fail with broken links.

The implementation therefore maintains two structures:

- A **`Dictionary<TestNodeUid, NodeInfo>`** populated from every `TestNodeUpdateMessage` we see (parent UID + raw display name + class/method if available). Uses **raw, uncapped** `TestNodeUid.Value` keys so chain resolution does not collide on truncated UIDs.
- The **`List<CapturedTestResult>`** of terminal results that becomes `<testcase>` rows.

Final `testpath` strings are capped at **`MaxTestPathLength = 64 * 1024`** (larger than the per-identity cap) so a pathologically deep tree cannot produce an unwritable XML attribute. Truncation appends the standard `\n…[truncated, original length: N]` marker.

### v2 (future): opt-in nested mode

A future RFC may introduce `--report-junit-nested` that emits actual nested `<testsuite>` elements for the (small) set of consumers that support them. v1 deliberately defers this to keep the default output portable.

## Suite grouping

Each `<testsuite>` corresponds to a **test class**, derived from `TestMethodIdentifierProperty.Namespace + "." + TypeName`. This matches Surefire's convention and is what Jenkins/GitLab UIs visualize naturally.

Tests with **no** `TestMethodIdentifierProperty` fall back to a synthetic suite whose name is derived from the **assembly module name** plus the immediate `ParentTestNodeUid` display name if one exists, rather than dumping every classless test into a single global `__unknown__` bucket (which would create duplicate `(classname, name)` rows for unrelated tests).

The root `<testsuites>` `name` attribute is the module file name without extension.

## MTP outcome → JUnit element mapping

| MTP `TestNodeStateProperty`                    | JUnit element                                  |
| ---------------------------------------------- | ---------------------------------------------- |
| `PassedTestNodeStateProperty`                  | *(no child element)*                            |
| `SkippedTestNodeStateProperty`                 | `<skipped message="..."/>` *(no body)*          |
| `FailedTestNodeStateProperty`                  | `<failure message="..." type="...">body</failure>` |
| `TimeoutTestNodeStateProperty`                 | `<error message="..." type="...">body</error>`  |
| `ErrorTestNodeStateProperty`                   | `<error message="..." type="...">body</error>`  |
| `CancelledTestNodeStateProperty` *(obsolete)*  | `<error message="..." type="...">body</error>`  |
| Other `WellKnownTestNodeTestRunOutcomeFailedProperties` | `<failure message="..." type="...">body</failure>` |
| `DiscoveredTestNodeStateProperty`              | *(filtered out — not emitted)*                  |
| `InProgressTestNodeStateProperty`              | *(filtered out — not emitted)*                  |

`body` is the composed failure text described in [Failure and error body format](#failure-and-error-body-format); the element is written with explicit start/end tags whenever that text is non-empty.

`Cancelled` becomes `<error>` rather than `<failure>` because cancellation indicates an interruption, not an assertion failure — `<error>` is the schema-correct bucket for "the test could not be evaluated".

### Failure and error body format

The body of `<failure>`/`<error>` mirrors the shape Java's `Throwable.printStackTrace()` produces, which is what genuine Surefire reports contain:

```text
System.InvalidOperationException: The expected condition was not met.
   at MyProject.MyTests.MyTest()
```

That is, the exception type and message form a header line, followed by the stack trace. Neither the Ant/windyroad `JUnit.xsd` nor Surefire's `surefire-test-report.xsd` constrains this body — both declare it as free-form text — so the header line is schema-valid in every flavor.

Writing the stack trace alone would **not** be equivalent to Java's output: .NET's `Exception.StackTrace` omits the leading `type: message` header that `Throwable.printStackTrace()` (and `Exception.ToString()`) include. Consumers that render the body rather than the `message` attribute — GitLab CI and CircleCI most notably — would then show a stack trace with no indication of *why* the test failed. This is especially damaging for fluent assertion libraries, whose stack traces are largely framework frames while the assertion message carries the actual diagnosis.

The `message` and `type` attributes are still written, so consumers that read them directly are unaffected. The resulting duplication between the `message` attribute and the body is exactly what every Maven/Surefire report already exhibits, so consumers that render both have long handled it.

Each part degrades gracefully: a missing exception type drops the header prefix, a missing message drops the colon-space separator, and a missing stack trace yields a header-only body.

### The `type` attribute

`type` is **always** emitted on `<failure>`/`<error>`. The Ant/windyroad `JUnit.xsd` marks it `use="required"` (Surefire relaxes it to optional), but MTP only supplies an exception type when the state property carried an actual `Exception` — frameworks that report a failure through `Explanation` alone leave it null. In that case the element name (`failure` or `error`) is used as the value, keeping the document valid under the stricter schema without inventing a bogus exception type name.

## Per-testcase metadata

The `<properties>` block (emitted **first** inside `<testcase>`) carries:

| Property name      | Value                                                       |
| ------------------ | ----------------------------------------------------------- |
| `testpath`         | `/`-joined display names from the root to this node         |
| `uid`              | The full `TestNode.Uid.Value` (capped at the identity limit) |
| `trait.<key>`      | One entry per `TestMetadataProperty` on the node            |

`<system-out>` and `<system-err>` are populated from `StandardOutputProperty` / `StandardErrorProperty`, truncated per the [memory bounds](#memory-bounds) below.

## XML safety

Test output is arbitrary user-controlled text. It may contain:

- Control characters that XML 1.0 forbids (everything below `0x20` except TAB / LF / CR).
- Unpaired surrogate halves (especially after fixed-length truncation).
- Bytes that are not valid UTF-16.

`XmlWriter` does not silently sanitize these — it throws `ArgumentException` when fed an invalid character. The extension therefore runs every textual value through a **`XmlSafeText`** helper that:

- Replaces control characters and unpaired surrogates with U+FFFD.
- Performs truncation **without splitting surrogate pairs**.
- Is applied to attribute values *and* element text.

## Memory bounds

The generator must not grow O(test-output-size) in memory. We reuse `Microsoft.Testing.Extensions.HtmlReport`'s caps applied at **capture** time:

| Field                                   | Cap                |
| --------------------------------------- | ------------------ |
| `MaxStandardStreamLength`               | `32 * 1024`        |
| `MaxStackTraceLength`                   | `32 * 1024`        |
| `MaxMessageLength`                      | `16 * 1024`        |
| `MaxIdentityFieldLength` (UID, names)   | `4 * 1024`         |
| `MaxTraitFieldLength`                   | `1024`             |
| `MaxTestPathLength` *(new)*             | `64 * 1024`        |

Each cap appends the standard `\n…[truncated, original length: N]` marker.

## Duplicate test identities

Multiple `<testcase>` rows in the same `<testsuite>` that share both `classname` and `name` are technically legal but **break in practice**: older Surefire collapses them, Jenkins' badge counts go wrong, GitLab's diff view shows one row, and so on.

When the same `(classname, name)` pair is emitted more than once (parameterized rows that share an identifier, retries, framework reruns), the writer **uniquifies** by suffixing each occurrence with `[attempt 1]`, `[attempt 2]`, … in capture order (preceded by a single space), and stores the original name + `attempt-index` / `attempt-of` as `<property>` children. We never drop a row — every captured result reaches the XML.

## Retry handling

Different retry mechanisms publish attempts to MTP differently, and the engine reflects that faithfully:

- **MSTest `[Retry]` attribute** — The MSTest adapter retries in-process and publishes **every** attempt as a `TestNodeUpdateMessage` under the same test node uid, tagged with `RetryAttemptProperty`. The JUnit report generator drops the attempts marked `IsSuperseded` (JUnit has no notion of attempts, so keeping them would inflate the suite totals), so the report still contains a single `<testcase>` row per logical test with its eventual outcome. No per-attempt disambiguation is applied.
- **`Microsoft.Testing.Extensions.Retry` (MTP-level orchestrator, `--retry-failed-tests`)** — The orchestrator re-runs the entire test-host child process on failure, and each re-run is filtered down to the tests that failed in the previous attempt. Every attempt keeps its own immutable JUnit XML file under `<results>/Retries/<id>/<n>/`. After the final attempt, the JUnit post-processor writes the top-level report as one row per logical test using each test's final outcome. JUnit XML has no portable retry vocabulary, so earlier failure history remains available only in the per-attempt files; the consolidated top-level report deliberately favors correct CI gating and suite totals over inflating failures with superseded attempts.
- **Per-attempt `TestNodeUpdateMessage`s that are *not* tagged with `RetryAttemptProperty`** (e.g. some 3rd-party test frameworks) — A producer that tags its attempts has its superseded ones filtered out above, so this case covers only *unattributed* duplicates. The Jenkins/Surefire flavor has no native rerun element, but consumers require `(classname, name)` pairs to be unique within a suite (see [Duplicate test identities](#duplicate-test-identities) above). When the engine sees two or more such nodes with the same `(classname, name)` pair within one report it:
  - Preserves every attempt as its own `<testcase>` row (never drops history) so flaky-test dashboards can compute pass-rate over the run.
  - Disambiguates each row by appending `[attempt 1]`, `[attempt 2]`, … to `name` (preceded by a single space), so portable consumers see distinct entries.
  - Emits two `<property>` children per disambiguated attempt — `attempt-index` (`1`, `2`, …) and `attempt-of` (total attempts for that logical test) — so retry-aware consumers can collapse the rows back into a single logical test.
  - Emits `<property name="original-name">` on every disambiguated attempt so consumers can recover the un-suffixed display name without parsing the `[attempt N]` marker.
  - Keeps the original `<property name="uid">` value on every attempt (when present), reflecting whatever attempt-disambiguating UID the framework chose to publish.
- **Atomic write via `.tmp` rename** — The engine writes to a `<final>.<random>.tmp` sibling (random suffix from `Path.GetRandomFileName`) and then renames it onto the final path with `overwrite: true`. The random `.tmp` suffix prevents concurrent processes from clobbering each other's intermediate file, and the atomic rename guarantees the final `.xml` is either fully written or absent.

We deliberately do **not** emit Maven Surefire 3.x's `<rerunFailure>` / `<flakyFailure>` children: only a small fraction of consumers parse them, and they require nesting attempt outcomes inside the *last* `<testcase>` element — which the majority of parsers would then mis-count.

## File naming

| Scenario                                   | Behavior                                                                                         |
| ------------------------------------------ | ------------------------------------------------------------------------------------------------ |
| `--report-junit` alone                     | Default name: `<asm>_<tfm>_<arch>.xml`                                                            |
| `--report-junit-filename custom.xml`       | Name overridden. Must end with `.xml`.                                                            |
| `--report-junit-filename subdir/custom.xml`| Relative paths allowed. Must not contain `..` segments or be drive-relative.                      |
| `--report-junit-filename /abs/path.xml`    | Fully-qualified paths allowed (Windows: `C:\foo.xml`, UNC, POSIX `/foo.xml`).                     |
| File already exists (default or explicit)  | Overwrite with a warning logged to the output device.                                             |

Placeholders supported in the explicit name (via `ArtifactNamingHelper.GetStandardReplacements`): `{pname}`, `{pid}`, `{asm}`, `{tfm}`, `{arch}`, `{time}`.

The file is **written to a `.tmp` sibling first, then renamed on success.** This avoids leaving a partial / corrupted `.xml` on the disk if serialization throws or the run is cancelled mid-write.

## CLI options

| Option                       | Arity        | Description                                                                  |
| ---------------------------- | ------------ | ---------------------------------------------------------------------------- |
| `--report-junit`             | `Zero`       | Enable JUnit report generation.                                              |
| `--report-junit-filename`    | `ExactlyOne` | Override the report file name (must end with `.xml`).                        |

Validation rules:

- `--report-junit-filename` may not be combined with `--list-tests`.
- `--report-junit-filename` requires `--report-junit`.
- The supplied file name must end with `.xml` (case-insensitive).
- The supplied path must not contain `..` segments.
- The supplied path must not be drive-relative (`C:foo.xml`).
- Reserved Windows filenames (`CON`, `PRN`, `AUX`, `NUL`, `CLOCK$`, `COM1`–`COM9`, `LPT1`–`LPT9`) are sanitized by prefixing with `_` (e.g. `CON.xml` → `_CON.xml`), mirroring the behavior of `Microsoft.Testing.Extensions.HtmlReport`. Invalid file-name characters (control characters, `" < > | : * ? \ / @ ( ) ^` and space) are likewise replaced with `_`.

## MSBuild auto-registration

Mirrors `Microsoft.Testing.Extensions.HtmlReport`:

```xml
<Project>
  <PropertyGroup>
    <EnableMicrosoftTestingExtensionsJUnitReport
        Condition=" '$(EnableMicrosoftTestingExtensionsJUnitReport)' == '' ">true</EnableMicrosoftTestingExtensionsJUnitReport>
  </PropertyGroup>

  <ItemGroup Condition=" '$(EnableMicrosoftTestingExtensionsJUnitReport)' == 'true' ">
    <TestingPlatformBuilderHook Include="JUnitReport-NEW-GUID-HERE">
      <DisplayName>Microsoft.Testing.Extensions.JUnitReport</DisplayName>
      <TypeFullName>Microsoft.Testing.Extensions.JUnitReport.TestingPlatformBuilderHook</TypeFullName>
    </TestingPlatformBuilderHook>
  </ItemGroup>
</Project>
```

A fresh GUID is generated for the `<TestingPlatformBuilderHook Include>` attribute (HtmlReport's GUID cannot be reused).

## Testing strategy

- **Acceptance tests** (`test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/JUnitReportTests.cs`, modelled on `HtmlReportTests.cs`):
  1. Reporter not enabled → no `.xml` written.
  2. Reporter enabled → file matches default name regex, root element is `<testsuites>`, contains `<?xml version="1.0"`.
  3. `--report-junit-filename custom.xml` → file uses custom name.
  4. Custom relative subdirectory path.
  5. `--report-junit-filename` without `--report-junit` → option-validation error.

- **Schema-conformance smoke** in the same suite: parse the produced XML with `XDocument`, walk the structure, assert `<testcase>` child ordering (`properties` first, then `skipped`/`error`/`failure`/`system-out`/`system-err`).

- **Help/info regression tests** updated:
  - `HelpInfoAllExtensionsTests.cs` — add `--report-junit`, `--report-junit-filename` to the `--help` block; add the `JUnitReportGeneratorCommandLine` block to the `--info` output.
  - `MSBuild.KnownExtensionRegistration.cs` — register the new package and assert the diag-log entry.

- **Unit tests** for `JUnitReportEngine` (`test/UnitTests/Microsoft.Testing.Extensions.UnitTests/JUnitReport/`):
  - XML safety: control chars, unpaired surrogates, surrogate-pair truncation.
  - Element ordering inside `<testcase>`.
  - Duplicate `(classname, name)` uniquification.
  - Parent-chain resolution with missing intermediate parents.
  - Counters at the suite and root level.
  - Outcome mapping (skipped/failed/error/timeout/cancelled).
  - Failure/error body composition across every combination of present/absent exception type, message and stack trace, plus the `type` attribute fallback to the element name.
