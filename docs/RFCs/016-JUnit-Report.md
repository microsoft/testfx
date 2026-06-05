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
      <skipped message="..."/>             <!-- 0..1 -->
      <error message="..." type="..."/>    <!-- 0..n -->
      <failure message="..." type="..."/>  <!-- 0..n -->
      <system-out>...</system-out>         <!-- 0..n -->
      <system-err>...</system-err>         <!-- 0..n -->
    </testcase>
    <system-out>...</system-out>
    <system-err>...</system-err>
  </testsuite>
</testsuites>
```

**Element ordering inside `<testcase>` is normative**: `properties?, skipped?, error*, failure*, system-out*, system-err*`. Stricter consumers (and some IDEs) reject documents that emit elements out of order.

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
| `SkippedTestNodeStateProperty`                 | `<skipped message="..."/>` + reason as text     |
| `FailedTestNodeStateProperty`                  | `<failure message="..." type="..."/>` + stack   |
| `TimeoutTestNodeStateProperty`                 | `<error message="..." type="..."/>` + stack     |
| `ErrorTestNodeStateProperty`                   | `<error message="..." type="..."/>` + stack     |
| `CancelledTestNodeStateProperty` *(obsolete)*  | `<error message="..." type="..."/>` + stack     |
| Other `WellKnownTestNodeTestRunOutcomeFailedProperties` | `<failure message="..." type="..."/>`  |
| `DiscoveredTestNodeStateProperty`              | *(filtered out — not emitted)*                  |
| `InProgressTestNodeStateProperty`              | *(filtered out — not emitted)*                  |

`Cancelled` becomes `<error>` rather than `<failure>` because cancellation indicates an interruption, not an assertion failure — `<error>` is the schema-correct bucket for "the test could not be evaluated".

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

When the same `(classname, name)` pair is emitted more than once (parameterized rows that share an identifier, retries, framework reruns), the writer **uniquifies** by appending `[attempt 2]`, `[attempt 3]`, … (preceded by a single space) and stores the original name + original UID as `<property>` children. We never drop a row — every captured result reaches the XML.

## File naming

| Scenario                                   | Behavior                                                                                         |
| ------------------------------------------ | ------------------------------------------------------------------------------------------------ |
| `--report-junit` alone                     | Default name: `{user}_{machine}_{module}_{tfm}_{yyyy-MM-dd_HH_mm_ss}.xml`                          |
| `--report-junit-filename custom.xml`       | Name overridden. Must end with `.xml`.                                                            |
| `--report-junit-filename subdir/custom.xml`| Relative paths allowed. Must not contain `..` segments or be drive-relative.                      |
| `--report-junit-filename /abs/path.xml`    | Fully-qualified paths allowed (Windows: `C:\foo.xml`, UNC, POSIX `/foo.xml`).                     |
| Default name collides with existing file   | Auto-retry with `_1`, `_2`, ... suffix (5-second budget).                                         |
| Explicit name collides with existing file  | Overwrite with a warning logged to the output device.                                             |

Placeholders supported in the explicit name (via `ArtifactNamingHelper.GetStandardReplacements`): `{pname}`, `{pid}`, `{asm}`, `{tfm}`, `{time}`.

The file is **written to a `.tmp` sibling first, then renamed on success.** This avoids leaving a partial / corrupted `.xml` on the disk if serialization throws or the run is cancelled mid-write.

## CLI options

| Option                       | Arity        | Description                                                                  |
| ---------------------------- | ------------ | ---------------------------------------------------------------------------- |
| `--report-junit`             | `Zero`       | Enable JUnit report generation.                                              |
| `--report-junit-filename`    | `ExactlyOne` | Override the report file name (must end with `.xml`).                        |

Validation rules:

- `--report-junit-filename` may not be combined with `--discover-tests`.
- `--report-junit-filename` requires `--report-junit`.
- The supplied file name must end with `.xml` (case-insensitive).
- The supplied path must not contain `..` segments.
- The supplied path must not be drive-relative (`C:foo.xml`).
- Reserved Windows filenames (`CON`, `PRN`, ...) are rejected.

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
