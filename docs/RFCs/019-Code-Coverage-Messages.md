# RFC 019 - Code coverage messages & consumer model for Microsoft.Testing.Platform

> **Current implementation note (2026-10-09):** The message consumer/read model is implemented in
> [TestCoverageResult](../../src/Platform/Microsoft.Testing.Platform/Services/TestCoverageResult.cs);
> [CoverageThresholdPolicy](../../src/Platform/Microsoft.Testing.Platform/Services/CoverageThresholdPolicy.cs)
> also implements overall line/branch gates beyond this RFC's message-only scope.
> See [MTP-013](../specifications/mtp.md#mtp-013--coverage-gates-consume-measurements-not-report-files).
> Collector producer/version interoperability and the ecosystem survey are not verified by this audit.

- [ ] Approved in principle
- [x] Under discussion
- [ ] Implementation
- [ ] Shipped

## Summary

Define a first-class, cross-language-informed contract for **code coverage** in
Microsoft.Testing.Platform (MTP). Coverage collectors (in-process test frameworks or
out-of-process `ITestHostProcessLifetimeHandler` extensions) publish coverage measurements,
threshold evaluations, and report-artifact references through the platform's data-consumer
pipeline. The platform correlates that data once and exposes it through an
`ITestCoverageResult` read model, which the terminal output device renders as a summary and
which any third-party extension (HTML/UI report generator, PR-comment bot, dashboard, coverage
badge) can consume without re-parsing raw artifacts or re-implementing aggregation. A threshold
failure drives a dedicated process exit code.

This RFC exists because an initial implementation ([#9896](https://github.com/microsoft/testfx/pull/9896))
introduces **permanent public API** for this feature while the producer side does not yet
exist. Public API is effectively forever, so the shape is captured here for sign-off before
implementation continues.

## Motivation

Enable coverage data to flow through MTP so that:

1. The **terminal** renders a coverage + threshold summary.
2. A **threshold failure drives a dedicated process exit code**.
3. **Third-party extensions** can consume normalized coverage data as a first-class citizen.

Goal (3) is the design driver: the contract must be a *good consumption surface*, not merely
enough to print three lines in the terminal. An HTML/UI report generator is the canonical
consumer and is used throughout this RFC to validate the shape.

## Cross-ecosystem survey (evidence)

The design is grounded in how real coverage tools across languages model their data, not in
intuition.

### Metric vocabulary — summary layer

| Metric | .NET Coverlet | MS.CodeCoverage | JaCoCo | Cobertura | Istanbul | coverage.py | lcov/gcov | llvm-cov | PHPUnit | SimpleCov | Go |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| Line        | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ~ |
| Statement   |   |   |   |   | ✔ | ✔ | ~ | ✔ |   |   | ✔ |
| Branch      | ✔ |   | ✔ | ✔ | ✔ | plug | ✔ | ✔ | ✔ | plug |   |
| Method      | ✔ |   | ✔ | ✔ |   |   |   |   | ✔ |   |   |
| Function    |   |   |   |   | ✔ |   | ✔ | ✔ |   |   | ~ |
| **Block**   |   | ✔ (primary) | ✔ |   |   |   |   |   |   |   |   |
| Instruction |   |   | ✔ |   |   |   |   |   |   |   |   |
| Region      |   |   |   |   |   |   |   | ✔ |   |   |   |
| Condition   |   |   | ~ |   | ~ |   |   |   | path |   |   |
| Class       |   |   | ✔ |   |   |   |   |   | ✔ |   |   |
| Complexity  |   |   | ✔ |   |   |   |   |   |   |   |   |
| MC/DC       |   |   | exp |   |   |   |   |   |   |   |   |

**Union at the summary layer ≈ 8 recurring metrics** (Line, Statement, Branch, Method,
Function, Block, Instruction, Region) plus niche/entity counters (Class, Complexity, Condition,
MC/DC). Notably, a 3-value Line/Branch/Method enum **cannot even name `block`**, the primary
metric of Microsoft's own `dotnet-coverage` / `Microsoft.CodeCoverage`.

### Raw data-model layer — how tools actually store data

| Format | Core primitive stored |
|---|---|
| lcov       | `DA:line,hits` · `BRDA:line,block,branch,taken` · `FN/FNDA` · `LF/LH` |
| Cobertura  | `<line hits= branch= condition-coverage="x% (n/m)">` |
| JaCoCo     | per-counter `covered/missed` (instruction/branch/line/method/class/complexity) |
| Istanbul   | `s` stmt hits · `f` fn hits · `b` branch-arm hit arrays + location maps |
| coverage.py| `executed_lines` · `missing_branches:[line,idx]` |
| llvm-cov   | `regions:[…executionCount]` · per-function counts |
| SonarQube generic | `<lineToCover covered= branchesToCover= coveredBranches=>` |
| Codecov    | per-line `covered` + optional `branches{total,covered}` |

Two universal facts emerge:

1. **Every format stores COUNTS; percentages are always derived.** No surveyed tool stores a
   bare percentage. A contract carrying a lone percentage cannot compute correct cross-scope
   aggregation (a weighted `Total` or an `Average` requires numerator + denominator) and cannot
   faithfully represent count-based counters such as JaCoCo's complexity/class.
2. **Purpose-built cross-language coverage contracts chose a SMALL CLOSED model.** SonarQube's
   "generic coverage" (explicitly language-agnostic) and Codecov (which ingests
   Cobertura/lcov/JaCoCo/gcov) both normalize to **per-line covered + optional
   branch{total,covered}** and derive everything else.

### The layering insight (open vs. closed)

The large metric vocabulary exists only at the **summary/rollup** layer. At the **raw
per-line/per-branch** layer, `line` and `branch` hit data form the **reduced cross-language
common denominator** that every tool can emit — but they are *not* full-fidelity primitives
from which the others can be reconstructed. Statement, instruction, block, and region coverage
each subdivide a line differently (multiple such units can share a single line), which is
exactly why JaCoCo carries dedicated instruction counters and llvm-cov carries regions. So the
raw layer is "universal" only in the sense of a lowest common denominator; the authoritative,
tool-specific detail lives in the report artifact.

Therefore the open-vs-closed question is answered by **choosing the layer**:

- **Raw layer** → the line/branch common denominator, which *is* essentially Cobertura/lcov.
  Don't reinvent it in messages — reference the artifact and let deep consumers parse the
  tool's own full-fidelity structure.
- **Summary layer** (what these messages are for) → a **seeded, closed, append-only** enum
  covers every surveyed tool, with **one** narrow escape hatch for safety-critical /
  proprietary metrics (MC/DC, condition, vendor-specific) that keep appearing at the edge.

**Conclusion: a closed API can cover all needed cases, and cross-language precedent favors
closed over open** — provided we (a) seed the enum with the real union, (b) store counts, and
(c) keep raw per-line data in the referenced artifact, not the messages.

## Goals

- Model coverage as **counts** (`covered`/`coverable`), deriving percentages, matching every
  surveyed ecosystem and enabling honest aggregation.
- Provide a **closed but seeded, append-only** metric vocabulary that names the real
  cross-language union, with a single escape hatch for exotic metrics.
- Make coverage a **first-class consumption surface** via an `ITestCoverageResult` read model
  that correlates data once, so consumers (terminal, HTML/UI generators, bots) never
  re-aggregate.
- Be **scope-aware** (Overall → Module → … → File) so a UI can build a tree and thresholds can
  be per-scope.
- Drive a dedicated **exit code** on threshold failure.

## Non-goals

- Reproducing per-line / per-branch fidelity in messages. That belongs in the referenced
  report artifact; the contract points at it (`TestCoverageReportMessage`).
- Defining a new on-disk report format. Cobertura/lcov/Microsoft.CodeCoverage already exist and
  are referenced, not reinvented.
- Producer/option/CLI design (how a collector computes thresholds, `--coverage-threshold`
  option surface, IPC framing for out-of-process collectors). Required to ship end-to-end but
  tracked separately; this RFC is the message + consumer contract only.

## Design principles

1. **Counts, not percentages.** Carry `covered`/`coverable`; derive `%`.
2. **Closed but seeded + append-only.** Enums documented non-exhaustive; consumers handle a
   `default` arm; adding members later is not a breaking change.
3. **One narrow escape hatch** on the metric kind only (not on aggregation/status).
4. **Layer separation.** Summary + threshold + a typed pointer to the rich artifact in
   messages; per-line fidelity stays in the artifact.
5. **Wire vs. consumer split.** Flat primitive messages for IPC; a correlated read model for
   consumers.
6. **Session-scoped.** All three messages derive from `DataWithSessionUid` (not `PropertyBagData`
   directly), like `TestNodeUpdateMessage` and the file-artifact messages, so a host that runs
   multiple sessions can attribute coverage correctly. `SessionUid` is part of the correlation
   key.
7. **Scope-aware, but not a strict tree.** Scopes are addressed by `(level, name)` across
   Overall / Module / Assembly / Namespace / Type / File. These levels do **not** form a
   single parent tree — a file can contain multiple types and a partial type can span several
   files — so the contract models scope identity, not a forced hierarchy (see the scope value
   type below).
8. **Guideline compliance.** No `init` accessors on new public API. User-facing terminal
   strings via `TerminalResources.resx` (+ `.xlf`). Numbers formatted with
   `CultureInfo.InvariantCulture`. New public API declared in `PublicAPI.Unshipped.txt`.

## Proposed public API shape

Namespaces: `Microsoft.Testing.Platform.Extensions.Messages` (messages + value types) and
`Microsoft.Testing.Platform.Services` (consumer read model).

### Metric kind — closed, seeded, append-only, one escape hatch

```csharp
namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>
/// Identifies a code-coverage metric. This is a closed but append-only set: consumers MUST
/// treat unrecognized values non-exhaustively (always handle a default case). New well-known
/// members may be added in future versions without being a breaking change.
/// </summary>
public enum CoverageMetric
{
    Line = 0,
    Statement = 1,
    Branch = 2,
    Method = 3,
    Function = 4,
    Block = 5,        // Microsoft.CodeCoverage / dotnet-coverage primary metric
    Instruction = 6,  // JaCoCo
    Region = 7,       // llvm-cov
    Class = 8,        // JaCoCo / PHPUnit entity counter
    Condition = 9,
    Complexity = 10,  // JaCoCo (count-based, not a percentage)

    /// <summary>
    /// A collector-specific metric not covered by the well-known members. When set,
    /// <see cref="TestCoverageMessage.CustomMetricName"/> carries the metric identifier.
    /// This is the single escape hatch for proprietary / safety-critical metrics (e.g. MC/DC).
    /// </summary>
    Custom = 255,
}
```

### Aggregation & scope level — small, universal, fully closed

```csharp
namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>
/// How per-scope values are combined for a threshold evaluation. Append-only. The population
/// being aggregated is identified separately by
/// <see cref="TestCoverageThresholdMessage.AggregatedOver"/> — e.g. <c>Minimum</c> over
/// <c>Module</c> vs. over <c>File</c> are distinct evaluations.
/// </summary>
public enum CoverageAggregation
{
    /// <summary>Not an aggregate — a single scope's own value (use with an exact scope).</summary>
    None = 0,
    Total = 1,    // aggregate covered / aggregate coverable across the population
    Minimum = 2,  // worst scope in the population
    Average = 3,  // mean of per-scope percentages in the population
    Maximum = 4,
}

/// <summary>The entity a coverage/threshold entry describes. Append-only.</summary>
public enum CoverageScopeLevel
{
    Overall = 0,
    Module = 1,
    Assembly = 2,
    Namespace = 3,
    Type = 4,
    File = 5,
}
```

There is intentionally **no** `CoverageThresholdStatus` enum — pass/fail is a derived `bool`
computed from the two numbers (see below), removing derivable state that could contradict the
values.

### Scope value type

```csharp
namespace Microsoft.Testing.Platform.Extensions.Messages;

public readonly struct CoverageScope : IEquatable<CoverageScope>
{
    public CoverageScope(CoverageScopeLevel level, string? name = null, string? containerHint = null)
    {
        // Invariant: only Overall is unnamed; every other level requires a stable identifier.
        if (level == CoverageScopeLevel.Overall)
        {
            if (name is not null)
            {
                throw new ArgumentException("The Overall scope must not have a name.", nameof(name));
            }
        }
        else if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException($"A name is required for scope level '{level}'.", nameof(name));
        }

        Level = level;
        Name = name;
        ContainerHint = containerHint;
    }

    /// <summary>Granularity of this scope.</summary>
    public CoverageScopeLevel Level { get; }

    /// <summary>Scope identifier (module path, type name, file path…); null only for Overall.</summary>
    public string? Name { get; }

    /// <summary>
    /// Optional, non-authoritative grouping hint for UIs (e.g. the assembly of a type, or a
    /// representative file of a type). It is deliberately <b>not</b> a tree edge: a file can
    /// contain many types and a partial type can span many files, so scope levels form
    /// overlapping dimensions rather than a single parent hierarchy. Consumers that need exact
    /// containment should read the referenced report artifact (see
    /// <see cref="TestCoverageReportMessage"/>), which carries the authoritative structure.
    /// </summary>
    public string? ContainerHint { get; }

    public static CoverageScope Overall => new(CoverageScopeLevel.Overall);

    // Identity is (Level, Name) only. ContainerHint is a non-authoritative display hint and is
    // intentionally excluded so two collectors reporting the same scope with different hints are
    // not split into separate entries by the correlator.
    public bool Equals(CoverageScope other)
        => Level == other.Level
        && string.Equals(Name, other.Name, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is CoverageScope s && Equals(s);

    public override int GetHashCode() => HashCode.Combine(Level, Name);
}
```

### `TestCoverageMessage` — a single measurement (counts-based)

```csharp
namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>
/// Reports a single code-coverage measurement (one metric for one scope) as counts.
/// The percentage is derived, never stored.
/// </summary>
public sealed class TestCoverageMessage : DataWithSessionUid
{
    public TestCoverageMessage(
        SessionUid sessionUid,
        CoverageScope scope,
        CoverageMetric metric,
        long coveredCount,
        long coverableCount,
        string producerId,
        string? customMetricName = null)
        : base("Test coverage", "Reports a code coverage measurement for a scope.", sessionUid)
    {
        if (coverableCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(coverableCount));
        }

        if (coveredCount < 0 || coveredCount > coverableCount)
        {
            throw new ArgumentOutOfRangeException(nameof(coveredCount));
        }

        if (string.IsNullOrEmpty(producerId))
        {
            throw new ArgumentException("A stable, non-empty producer id is required.", nameof(producerId));
        }

        if (metric == CoverageMetric.Custom && string.IsNullOrWhiteSpace(customMetricName))
        {
            throw new ArgumentException("A custom metric name is required when metric is Custom.", nameof(customMetricName));
        }

        if (metric != CoverageMetric.Custom && customMetricName is not null)
        {
            throw new ArgumentException("A custom metric name is only valid when metric is Custom.", nameof(customMetricName));
        }

        Scope = scope;
        Metric = metric;
        CoveredCount = coveredCount;
        CoverableCount = coverableCount;
        ProducerId = producerId;
        CustomMetricName = customMetricName;
    }

    public CoverageScope Scope { get; }

    public CoverageMetric Metric { get; }

    /// <summary>
    /// Identifies the collector that produced this measurement (e.g. "microsoft-code-coverage").
    /// <b>Required and non-empty</b>: it is part of the correlation key, so results from multiple
    /// collectors are never silently merged, and it ties a measurement back to its
    /// <see cref="TestCoverageReportMessage"/>. A collector that does not care about
    /// multi-collector scenarios should still pass a stable constant (e.g. its extension Uid).
    /// </summary>
    public string ProducerId { get; }

    /// <summary>Set only when <see cref="Metric"/> is <see cref="CoverageMetric.Custom"/>.</summary>
    public string? CustomMetricName { get; }

    public long CoveredCount { get; }

    public long CoverableCount { get; }

    /// <summary><see langword="true"/> when there is any coverable data.</summary>
    public bool HasCoverableData => CoverableCount > 0;

    /// <summary>Coverage as a percentage in the range 0–100; 0 when nothing is coverable.</summary>
    public double Percentage => HasCoverableData ? (double)CoveredCount / CoverableCount * 100d : 0d;
}
```

### `TestCoverageThresholdMessage` — scope-aware, derived pass/fail

```csharp
namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>Reports the result of a coverage threshold evaluation.</summary>
public sealed class TestCoverageThresholdMessage : DataWithSessionUid
{
    public TestCoverageThresholdMessage(
        SessionUid sessionUid,
        CoverageScope scope,
        CoverageMetric metric,
        CoverageAggregation aggregation,
        double actualPercentage,
        double requiredPercentage,
        bool hasCoverableData,
        string producerId,
        CoverageScopeLevel? aggregatedOver = null,
        bool treatNoDataAsFailure = true,
        string? customMetricName = null)
        : base("Test coverage threshold", "Reports the result of a coverage threshold evaluation.", sessionUid)
    {
        ValidatePercentage(requiredPercentage, nameof(requiredPercentage));

        // ActualPercentage is only meaningful when there is coverable data; otherwise it is
        // normalized to a stable 0 so it can never leak NaN/Infinity into renderers/consumers
        // that display it without checking HasCoverableData.
        if (hasCoverableData)
        {
            ValidatePercentage(actualPercentage, nameof(actualPercentage));
        }
        else
        {
            actualPercentage = 0d;
        }

        if (string.IsNullOrEmpty(producerId))
        {
            throw new ArgumentException("A stable, non-empty producer id is required.", nameof(producerId));
        }

        if (metric == CoverageMetric.Custom && string.IsNullOrWhiteSpace(customMetricName))
        {
            throw new ArgumentException("A custom metric name is required when metric is Custom.", nameof(customMetricName));
        }

        if (metric != CoverageMetric.Custom && customMetricName is not null)
        {
            throw new ArgumentException("A custom metric name is only valid when metric is Custom.", nameof(customMetricName));
        }

        // An aggregate must name the population it aggregated over; a non-aggregate must not.
        if (aggregation is CoverageAggregation.None && aggregatedOver is not null)
        {
            throw new ArgumentException("A non-aggregate evaluation must not specify a population.", nameof(aggregatedOver));
        }

        if (aggregation is not CoverageAggregation.None
            && aggregatedOver is null or CoverageScopeLevel.Overall)
        {
            throw new ArgumentException("An aggregate evaluation must specify a non-overall aggregated population.", nameof(aggregatedOver));
        }

        Scope = scope;
        Metric = metric;
        Aggregation = aggregation;
        AggregatedOver = aggregatedOver;
        ActualPercentage = actualPercentage;
        RequiredPercentage = requiredPercentage;
        HasCoverableData = hasCoverableData;
        TreatNoDataAsFailure = treatNoDataAsFailure;
        ProducerId = producerId;
        CustomMetricName = customMetricName;

        static void ValidatePercentage(double value, string paramName)
        {
            if (double.IsNaN(value) || value < 0d || value > 100d)
            {
                throw new ArgumentOutOfRangeException(paramName, value, "Percentage must be a number in the range 0–100.");
            }
        }
    }

    public CoverageScope Scope { get; }         // enables per-scope thresholds

    public CoverageMetric Metric { get; }

    public string? CustomMetricName { get; }

    /// <summary>Identifies the collector that produced this evaluation (required, correlation key).</summary>
    public string ProducerId { get; }

    public CoverageAggregation Aggregation { get; }

    /// <summary>
    /// The child scope level that <see cref="Aggregation"/> was computed over (e.g. the minimum
    /// across <c>Module</c> vs. across <c>File</c>). Non-null iff <see cref="Aggregation"/> is
    /// not <see cref="CoverageAggregation.None"/>, so a <c>(scope, metric, aggregation)</c>
    /// triple is unambiguous.
    /// </summary>
    public CoverageScopeLevel? AggregatedOver { get; }

    /// <summary>Actual coverage, 0–100; normalized to 0 when <see cref="HasCoverableData"/> is false.</summary>
    public double ActualPercentage { get; }

    public double RequiredPercentage { get; }   // 0–100, validated

    /// <summary>
    /// <see langword="false"/> when the evaluated scope had nothing coverable (an empty module,
    /// generated-only code, …). This is kept distinct from a genuine 0% so a no-data policy can
    /// be applied explicitly rather than being conflated with real 0% coverage.
    /// </summary>
    public bool HasCoverableData { get; }

    /// <summary>The no-data policy: whether an evaluation with no coverable data counts as failed.</summary>
    public bool TreatNoDataAsFailure { get; }

    /// <summary>
    /// <see langword="true"/> when the threshold is satisfied. With no coverable data the outcome
    /// follows <see cref="TreatNoDataAsFailure"/>; otherwise it is a plain numeric comparison.
    /// </summary>
    public bool Passed => HasCoverableData
        ? ActualPercentage >= RequiredPercentage
        : !TreatNoDataAsFailure;
}
```

### `TestCoverageReportMessage` — typed pointer to the rich artifact

```csharp
namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>The on-disk format of a coverage report artifact. Append-only.</summary>
public enum CoverageReportFormat
{
    Unknown = 0,
    Cobertura = 1,
    OpenCover = 2,
    Lcov = 3,
    CoverageXml = 4,        // Microsoft.CodeCoverage XML
    Custom = 255,
}

/// <summary>
/// References a coverage report artifact so deep consumers (HTML/UI generators) can parse
/// full-fidelity per-line data that the summary intentionally does not carry.
/// </summary>
public sealed class TestCoverageReportMessage : DataWithSessionUid
{
    public TestCoverageReportMessage(
        SessionUid sessionUid,
        string reportPath,
        CoverageReportFormat format,
        string producerId,
        string? customFormatName = null)
        : base("Test coverage report", "References a coverage report artifact.", sessionUid)
    {
        ReportPath = string.IsNullOrEmpty(reportPath)
            ? throw new ArgumentException("A report path is required.", nameof(reportPath))
            : reportPath;

        if (string.IsNullOrEmpty(producerId))
        {
            throw new ArgumentException("A stable, non-empty producer id is required.", nameof(producerId));
        }

        // Discriminated pair: a custom format requires a name; a standard format forbids one.
        if (format == CoverageReportFormat.Custom && string.IsNullOrWhiteSpace(customFormatName))
        {
            throw new ArgumentException("A custom format name is required when format is Custom.", nameof(customFormatName));
        }

        if (format != CoverageReportFormat.Custom && customFormatName is not null)
        {
            throw new ArgumentException("A custom format name is only valid when format is Custom.", nameof(customFormatName));
        }

        Format = format;
        ProducerId = producerId;
        CustomFormatName = customFormatName;
    }

    public string ReportPath { get; }

    public CoverageReportFormat Format { get; }

    /// <summary>Set if and only if <see cref="Format"/> is <see cref="CoverageReportFormat.Custom"/>.</summary>
    public string? CustomFormatName { get; }

    /// <summary>Id of the collector that produced the report (required, correlation key).</summary>
    public string ProducerId { get; }
}
```

### Consumer read model — the primary surface for report generators

The platform registers one internal consumer that subscribes to the three message types,
correlates/groups the data **once**, and exposes a snapshot at session end. Third-party
extensions bind to this instead of re-aggregating raw messages.

```csharp
namespace Microsoft.Testing.Platform.Services;

/// <summary>Correlated, session-scoped view of all coverage data for consumers.</summary>
public interface ITestCoverageResult
{
    /// <summary>Overall (whole-run) summary for a session, if reported.</summary>
    CoverageScopeSummary? GetOverall(SessionUid sessionUid);

    /// <summary>Per-scope summaries with all metrics correlated per scope.</summary>
    IReadOnlyList<CoverageScopeSummary> Scopes { get; }

    /// <summary>All threshold evaluations (passed and failed).</summary>
    IReadOnlyList<TestCoverageThresholdMessage> Thresholds { get; }

    /// <summary>Pointers to rich report artifacts for deep parsing.</summary>
    IReadOnlyList<CoverageReportReference> Reports { get; }

    /// <summary><see langword="true"/> when any threshold failed (drives exit code).</summary>
    bool HasThresholdFailure { get; }
}

/// <summary>
/// Describes coverage-message support and the enabled producers in the current test application.
/// </summary>
public interface ITestCoverageCapabilities
{
    /// <summary>Whether this platform supports the first-class coverage message contract.</summary>
    bool SupportsTestCoverageMessages { get; }

    /// <summary>
    /// UIDs of enabled data producers that declare at least one coverage message type. This is a
    /// live application capability; query it from a session lifecycle callback after extensions
    /// have been constructed, not from an extension factory or constructor.
    /// </summary>
    IReadOnlyCollection<string> EnabledProducerUids { get; }
}

public sealed class CoverageScopeSummary
{
    // Public so third-party report generators can construct snapshots in their own unit tests
    // and build fakes of ITestCoverageResult without reflection. Get-only properties still
    // prevent mutation after construction.
    public CoverageScopeSummary(
        SessionUid sessionUid,
        CoverageScope scope,
        IReadOnlyList<CoverageMetricResult> metrics)
    {
        SessionUid = sessionUid;
        Scope = scope;
        Metrics = Array.AsReadOnly((metrics ?? throw new ArgumentNullException(nameof(metrics))).ToArray());
    }

    public SessionUid SessionUid { get; }

    public CoverageScope Scope { get; }

    public IReadOnlyList<CoverageMetricResult> Metrics { get; }

    /// <summary>
    /// Convenience lookup for a well-known metric, e.g. <c>summary[CoverageMetric.Line]</c>;
    /// null if absent. Throws for <see cref="CoverageMetric.Custom"/>, because every custom
    /// metric shares that enum value and is distinguished only by its name — use
    /// <see cref="GetCustom(string)"/> instead. If a scope carries the same metric from multiple
    /// producers, this returns the first; use <see cref="Metrics"/> to disambiguate by
    /// <see cref="CoverageMetricResult.ProducerId"/>.
    /// </summary>
    public CoverageMetricResult? this[CoverageMetric metric]
    {
        get
        {
            if (metric == CoverageMetric.Custom)
            {
                throw new ArgumentException("Use GetCustom(name) to look up a custom metric.", nameof(metric));
            }

            foreach (CoverageMetricResult result in Metrics)
            {
                if (result.Metric == metric)
                {
                    return result;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Looks up a custom metric by name. If multiple producers report the same name, returns the
    /// first in stable first-seen order; use Metrics to disambiguate by ProducerId.
    /// </summary>
    public CoverageMetricResult? GetCustom(string customMetricName)
    {
        foreach (CoverageMetricResult result in Metrics)
        {
            if (result.Metric == CoverageMetric.Custom
                && string.Equals(result.CustomMetricName, customMetricName, StringComparison.Ordinal))
            {
                return result;
            }
        }

        return null;
    }
}

public sealed class CoverageMetricResult
{
    public CoverageMetricResult(
        CoverageMetric metric,
        long coveredCount,
        long coverableCount,
        string producerId,
        string? customMetricName = null)
    {
        Metric = metric;
        CoveredCount = coveredCount;
        CoverableCount = coverableCount;
        ProducerId = producerId;
        CustomMetricName = customMetricName;
    }

    public CoverageMetric Metric { get; }

    public string? CustomMetricName { get; }

    /// <summary>The collector that produced this measurement; part of the correlation key.</summary>
    public string ProducerId { get; }

    public long CoveredCount { get; }

    public long CoverableCount { get; }

    public bool HasCoverableData => CoverableCount > 0;

    public double Percentage => HasCoverableData ? (double)CoveredCount / CoverableCount * 100d : 0d;
}

public sealed class CoverageReportReference
{
    public CoverageReportReference(
        SessionUid sessionUid,
        string path,
        CoverageReportFormat format,
        string producerId,
        string? customFormatName = null)
    {
        SessionUid = sessionUid;
        Path = path;
        Format = format;
        ProducerId = producerId;
        CustomFormatName = customFormatName;
    }

    public SessionUid SessionUid { get; }

    public string Path { get; }

    public CoverageReportFormat Format { get; }

    public string? CustomFormatName { get; }

    public string ProducerId { get; }
}
```

`ITestCoverageCapabilities.SupportsTestCoverageMessages` describes the platform's in-process message
and consumer contract. It is distinct from the JSON-RPC server-mode capability with the same suffix:
`capabilities.testing.supportsTestCoverageMessages` remains `false` until those first-class messages
are forwarded over the server-mode wire.

None of these public properties use `init`, per platform guidelines.

## How consumers use it — two tiers

The terminal renderer and any third-party report generator are **both** consumers of the same
`ITestCoverageResult`, so there is a single source of truth and no duplicated buffering.

```csharp
internal sealed class HtmlCoverageReportGenerator(ITestCoverageResult coverage)
    : IDataConsumer, ITestSessionLifetimeHandler
{
    // IExtension metadata (inherited by both interfaces). Uid MUST be a stable string literal,
    // never nameof(...): a class rename would otherwise silently change the extension identity.
    public string Uid => "HtmlCoverageReportGenerator";
    public string Version => "1.0.0";
    public string DisplayName => "HTML coverage report";
    public string Description => "Generates an HTML coverage report from platform coverage data.";
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    // This consumer only needs the session-end hook; it reads the correlated read model rather
    // than the raw messages, so DataTypesConsumed is empty and ConsumeAsync is a no-op.
    public Type[] DataTypesConsumed => [];
    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task OnTestSessionStartingAsync(ITestSessionContext context) => Task.CompletedTask;

    public async Task OnTestSessionFinishingAsync(ITestSessionContext context)
    {
        // Tier 1 — summary tables / badges straight from the correlated read model:
        foreach (CoverageScopeSummary scope in coverage.Scopes)
        {
            double? line   = scope[CoverageMetric.Line]?.Percentage;
            double? branch = scope[CoverageMetric.Branch]?.Percentage;
            // render a row — no group-by, no percentage math needed
        }

        // Tier 2 — need annotated source / per-line hits? Parse the referenced artifact:
        foreach (CoverageReportReference report in coverage.Reports)
        {
            if (report.Format == CoverageReportFormat.Cobertura)
            {
                // parse report.Path for the line-level detail the summary omits
            }
        }
    }
}
```

## Correlation key

The read model correlates messages into `CoverageScopeSummary` / threshold / report entries
using the **full** key for each entry type:

```text
Measurement:
(SessionUid, ProducerId, Scope.Level, Scope.Name, Metric, CustomMetricName when Metric == Custom)

Threshold:
(SessionUid, ProducerId, Scope.Level, Scope.Name, Metric, CustomMetricName when Metric == Custom,
 Aggregation, AggregatedOver)

Report:
(SessionUid, ProducerId, Path)
```

- **`SessionUid`** and **`ProducerId`** are included so a multi-session host and multiple
  concurrent collectors never collapse into each other.
- **`Scope.Level`** and **`Scope.Name`** identify the measured or evaluated scope. Report
  references use their artifact **`Path`** instead because they have no scope or metric.
- **`CustomMetricName`** is part of the key whenever `Metric == Custom`. Without it, two
  producers each reporting a different proprietary metric (e.g. MC/DC vs. a vendor metric) would
  both key as `Custom` and overwrite one another.
- Threshold keys additionally include **`Aggregation`** and **`AggregatedOver`**, so distinct
  threshold evaluations for the same scope and metric do not overwrite one another.
- The public `CoverageScopeSummary` and `CoverageReportReference` projections retain
  `SessionUid`, so entries from different sessions remain attributable after correlation.
- **Duplicate full keys:** if the same full key is published more than once in a session, the
  **last write wins** and the correlator logs a diagnostic. Collectors are expected to emit one
  final measurement, threshold evaluation, or report per key; re-emission is treated as a
  correction, not an aggregation.

## Platform wiring

- Register a single `TestCoverageResult : ITestCoverageResult, IDataConsumer` as a common
  service + consumer in both the framework and test-host-controller pipelines. It is the single
  source of truth; the terminal reads from it rather than maintaining its own coverage lists.
- Register `ITestCoverageCapabilities` as a public service for extensions. It advertises protocol
  support and snapshots the UIDs of enabled `IDataProducer` extensions whose
  `DataTypesProduced` include a coverage measurement, threshold, or report message. A report
  generator can query this during a session lifecycle callback and warn when no other producer is
  enabled.
- Remote negotiation advertises protocol support separately from producer availability:
  - the JSON-RPC initialize response includes
    `testing.supportsTestCoverageMessages`.
  Producer UIDs are intentionally not negotiated because framework and per-session extensions may
  not have been instantiated when the pipe handshake or JSON-RPC initialize request occurs.
- `TerminalOutputDevice` consumes `ITestCoverageResult` at session end and renders via
  `TerminalTestReporter.AppendCoverageSummary`, using **localized** `TerminalResources` strings
  and `CultureInfo.InvariantCulture` numeric formatting. The terminal summary renders only
  `Overall` and `Module` measurements to keep CI output bounded; detailed namespace/type/file
  measurements remain available through `ITestCoverageResult`.
- New exit code `CoverageThresholdFailed = 14`, returned from `ConsoleTestHost` and
  `TestHostControllersTestHost` when `HasThresholdFailure` is true and the run would otherwise
  succeed. The check is extracted into a shared helper to avoid the duplication present in the
  prototype.

## Relationship to PR #9896

| Dimension | #9896 (prototype) | This RFC |
|---|---|---|
| Metric kind | closed enum Line/Branch/Method (3) | closed, seeded, append-only (~11) + `Custom` escape |
| Metric value | lone `double` percentage | `covered`/`coverable` counts → derived % |
| Granularity | `ModuleName` only | `CoverageScope` across Overall→File (overlapping dimensions, not a forced tree) |
| Threshold identity | global only | scope-aware (per-scope possible) |
| Threshold status | redundant enum | computed `bool Passed` |
| "No data" | silent 100% | explicit `HasCoverableData` |
| Deep report access | none | `TestCoverageReportMessage` + `CoverageReportReference` |
| Consumer API | raw messages, buffered twice | `ITestCoverageResult` correlated read model, single source |
| Localization | hardcoded English | `TerminalResources.resx` + `.xlf` |
| Number formatting | current culture (`:F1`) | `CultureInfo.InvariantCulture` |

## Open questions

1. **One metric per message vs. a bundle per scope.** This RFC proposes one `(scope, metric)`
   measurement per message (composable, stream-friendly; correlation happens in the read
   model). Bundling all metrics per scope is an alternative — a conscious call is needed.
2. **Property bag population.** The messages now derive from `DataWithSessionUid` (which itself
   extends `PropertyBagData`). Should they also populate the property bag with their typed values
   so generic bag consumers see the data, or leave the bag empty and rely on the strongly-typed
   properties?
3. **Escape-hatch surface.** `Custom` enum member + `CustomMetricName` string (chosen here for
   being closed-by-default) vs. an open readonly-struct metric type — confirm.
4. **Value range convention.** This RFC standardizes on **0–100**; some tools use 0–1. Lock it
   and document on every percentage property.

## dotnet test summary transport: research and decisions

### Problem and contract

[dotnet/sdk#56796](https://github.com/dotnet/sdk/issues/56796) requires the SDK to render
coverage summaries without understanding collector-specific report formats. The TestFX
portion transports measurements and couples merged measurements to the artifact that
produced them. SDK rendering, collector merging, and composed-product validation are
separately owned. Threshold evaluation and process exit-code policy remain unchanged.

The application forwards only `TestCoverageMessage` measurements at `Overall` and `Module`
scope. Each entry is an update, not a delta. Consumers use last-write-wins identity including
the execution, process instance, session, producer, scope level/name, metric, and custom
metric name. The existing handshake identifies the producing test module. Measurements
from different applications, sessions, or collectors must not be added or averaged into a
run-level total.

For post-processing, the existing four-argument `ProcessedArtifact` constructor remains.
An additive five-argument overload accepts
`IReadOnlyList<Microsoft.Testing.Platform.Services.CoverageMetricResult>? coverageSummary`,
exposed as a read-only `CoverageSummary` property. The list is snapshotted. Each metric
describes the complete output artifact, computed by its producer from the actual merged
data. `null` means unavailable; an explicit `0/0` measurement means no coverable data, not
100 percent coverage. The platform does not parse coverage reports or merge measurements.

| Wire addition | Contract |
| --- | --- |
| Handshake property 20, `CoverageSummaryVersion` | Both peers send the exact string `"1"`. Missing or unknown versions disable coverage transport; a compatible base-protocol handshake is also required. |
| Serializer 15, `CoverageSummaryMessages` | Fields 1 `ExecutionId`, 2 `InstanceId`, 3 `Summaries`, using the existing counted-list framing. |
| `CoverageSummaryMessage` entry | Fields 1 `SessionUid` (string), 2 `ProducerId` (string), 3 `ScopeLevel` (int32), 4 `ScopeName` (string), 5 `Metric` (int32), 6 `CustomMetricName` (string), 7 `CoveredCount` (int64), 8 `CoverableCount` (int64). All fields are nullable on the wire, so absent numbers cannot become invented zeros. Enum values match RFC 019. |
| `FileArtifactMessage` field 9, `CoverageSummary` | Optional list of the same entries, after existing field 8 `InputArtifactPaths`. Entries describe the complete output at `Overall` scope, with no session or scope name. |

Base protocol versions are unchanged. Coverage support does not enable the 1.4 server-control
feature or reuse serializer IDs 13/14. Old SDKs receive the existing artifact payload without
coverage metadata and never receive serializer 15. Old producers leave the new capability
and field absent. Unknown optional fields retain the existing skip behavior.

The dispatcher sends metadata only with a validated output artifact, after every processor
has finished, and omits all coverage metadata for a failed or truncated job. Original
artifacts and existing processor failure exits remain intact. Cancellation must prevent a
pending metadata send. The SDK must stage metadata with its candidate artifact and commit it
only after successful job completion and its existing artifact/provenance checks. A
multi-application authoritative `Total` additionally requires complete coverage input
provenance; missing, incompatible, opted-out, partial, unmerged, or cancelled inputs never
authorize a fabricated total. A genuinely complete coverage result can accompany a failing
test run without changing that run's exit code.

Regular test hosts and controller-process lifetime handlers use the push-only data consumer.
The controller drains messages after coverage finalization. Tool mode does not construct
the session message bus, so merged metadata is sent directly by the artifact dispatcher.
The SDK owns process launch, completion/cancellation, candidate acceptance, and rendering;
the platform owns its connection and existing disposal behavior.

### Research and experiments

- 2026-10-27: Inspected the issue, this RFC, `DotnetTestDataConsumer`,
  `DotnetTestConnection`, the IPC serializers, and artifact dispatcher at TestFX base
  `b6e7268c80ed13ec33c3a1f736140bc8169b194c`. Existing streamed data excludes coverage;
  `ProcessedArtifact` has no measurement metadata; the dispatcher publishes validated
  artifact messages with the exact represented input paths.
- 2026-10-27: Verified regular-host registration in `TestHostBuilder.Framework`, controller
  registration/draining in `TestHostControllersTestHost`, and the separate `ToolsHost`
  dispatcher path. No new tool-mode data bus is needed.
- 2026-10-27: The SDK coordinator accepted capability 20, serializer 15, entry fields 1-8,
  artifact field 9, and the additive public API before consumer implementation. The
  collector owner confirmed it can attach counts from the exact merged output using
  `CoverageMetricResult`; its merged producer ID need not equal a standalone producer ID.
- 2026-10-27: The pinned SDK bootstrap succeeded, but Arcade's runtime-install `.cmd` could
  not resolve `powershell` on this execution host. Installing the selected pinned .NET
  runtime directly through `eng\common\dotnet-install.ps1` and passing
  `/p:SkipRuntimesInstall=true` to the bounded build avoided that local wrapper failure
  without changing build infrastructure or substituting an SDK/runtime version.
- 2026-10-27: Before production edits, built and executed the existing focused IPC,
  connection, consumer, and dispatcher selection on .NET 8.0.32 with SDK
  `12.0.100-alpha.1.26509.101`, Release/net8.0. The selection executed 214 cases:
  214 passed, zero failed/skipped; build and test exits were zero. Assembly method-level
  parallelization remained enabled (32 workers on this host).
- 2026-10-27: The first producer compilation found an alphabetic-using-order diagnostic,
  corrected without changing the contract. The next core build passed with zero warnings
  and errors. Reflection confirmed both `ProcessedArtifact` constructors and the expected
  `IReadOnlyList<CoverageMetricResult>` property. This is compilation/API-shape evidence,
  not composed-product validation.
- 2026-10-27: Evaluated local package versions with the pinned SDK and compared them with
  `NuGet.Versioning`. A bare `coverage-summary.20261027.1` suffix sorts below existing
  numerical `preview` dependencies and was rejected before packing. The explicit local
  suffix `preview.coverage-summary.20261027.1` sorts above those previews without
  impersonating a published build. Collector consumers of the new constructor must depend
  on the feature-bearing platform version; handshake gating cannot supply an API missing
  from an older assembly.
- 2026-10-27: A first isolated pack compiled all five TFMs and created a package, but its
  post-build verifier could not launch `powershell` through the tool host's inherited
  `PATH`. Setting `PATH` inside the same native `cmd` process before launching PowerShell
  resolved the launcher issue. The repeated isolated build/pack and the unmodified
  repository package verifier then exited zero, with zero warnings/errors. Product
  binaries/intermediates were isolated from the concurrent unit-test build; the verified
  package was written to the normal Release shipping feed.

### Alternatives and decision

- **No change:** preserves compatibility but leaves CI log consumers without a summary.
- **Forward rendered console text:** cannot identify complete merged provenance, distinguish
  collectors reliably, or give the SDK ownership of stable numeric formatting.
- **Parse collector reports in the SDK:** duplicates collector knowledge and fails the
  format-independent contract.
- **Add or average application counts:** double-counts assemblies loaded by multiple test
  applications and cannot reproduce the actual merged result.
- **Separate merged-summary message:** can outlive a rejected or failed artifact job.
- **Chosen:** one negotiated measurement message plus optional artifact-coupled metadata,
  reusing the existing validated count model. A separate capability avoids enabling
  unrelated protocol features; the extra wire record keeps SDK-shared source independent
  of public platform types. Detailed scopes, threshold transport, and new exit-code policy
  are deliberately excluded.

### Validation and demonstration

Producer-level tests and packed-package checks do not establish real SDK/collector
integration. The SDK coordinator owns the composed command and its stable invariant-culture
output demonstration; this transport has no independent user-facing media.

The local candidate is
`artifacts\packages\Release\Shipping\Microsoft.Testing.Platform.2.6.0-preview.coverage-summary.20261027.1.nupkg`,
SHA-256 `34FDAB56A1228C5DACA3BEC45B0CC7B48E44D41D8E1F4DCB6668AF1D2311F6AD`.
It contains the complete declared `net8.0`, `net9.0`, `net10.0`, `net11.0`, and
`netstandard2.0` payloads. Each packed DLL's hash matches its source-built counterpart, and
each TFM's XML documentation includes the new coverage metadata API. This is a genuine
source pack, not a modified published package.

The effective pack invocation, from the worktree root, was:

```powershell
$root = (Get-Location).Path
& .\eng\build.ps1 -restore -build -pack -c Release `
    -projects src\Platform\Microsoft.Testing.Platform\Microsoft.Testing.Platform.csproj `
    -msbuildEngine dotnet -nodeReuse:$false `
    -properties @(
        "/p:ArtifactsDir=$root\artifacts\coverage-package-bootstrap\",
        "/p:PackageOutputPath=$root\artifacts\packages\Release\Shipping",
        '/p:VersionSuffix=preview.coverage-summary.20261027.1',
        '/p:SkipRuntimesInstall=true',
        '/bl:<task-owned-evidence-directory>\coverage-producer-pack.binlog'
    )
```

Result: build, pack, and repository package verification exited **0**. The binlog destination
above denotes the retained session evidence directory, not an additional build property.
This local environment used the native-process `PATH` setup described above and the
repository-pinned SDK/runtime bootstrap; no global or source build settings were changed.

The final implementation/test patch against base
`b6e7268c80ed13ec33c3a1f736140bc8169b194c`, excluding this decision record, has SHA-256
`8B63DF5B2825CD1F6791AB456D17D2117ECDEC2E84782BCBEFCC31DFDEC6DC4C`.
The implementation and tests are committed as
`479eb249a33830d9c0d2c26b7ceb3349d65771c1`; later decision-record-only updates do not
change those tested source files.
The package's exact producer-source patch has SHA-256
`3C7D00CB9F0321C26A0CDDDC8EC81041A0AEECFE41746F4EE2C01851977B6888`.
The package was built from that uncommitted source state, not from a later commit stamped
into its metadata.

| Requirement | Evidence |
| --- | --- |
| Exact capability, compatible base, reset and opt-out | `IsCompatibleProtocolAsync_CoverageSummaryRequiresExactVersionAndCompatibleBase`, `IsCompatibleProtocolAsync_CoverageSummaryRenegotiationClearsPreviousSupport`, `IsCompatibleProtocolAsync_LocalCoverageOptOutCannotBeEnabledByPeer`. |
| Identity, 64-bit counts, update semantics and bounded scopes | `ConsumeAsync_CoverageSummaryPreservesIdentityAndInt64Counts`, `ConsumeAsync_CoverageUpdatesRemainReplacementsWithDistinctIdentities`, `ConsumeAsync_NarrowCoverageScopesAreIgnored`. |
| Stable wire fields, missing counts, malformed input and legacy readers | `CoverageSummaryMessages_IndependentWireFixturePinsAllFields`, `CoverageSummaryMessages_AbsentCountsAreNotDefaultedToZero`, `CoverageSummaryPayload_InvalidCollectionLengthIsBounded`, `FileArtifactMessages_LegacyReaderSkipsCoverageAndPreservesFollowingArtifacts`. |
| Metadata snapshot, successful acceptance, failure and cancellation | `ProcessedArtifact_CoverageSummaryIsReadOnlySnapshot`, `ValidateProcessedArtifact_NormalizesPathWithoutDroppingCoverage`, `DispatcherRun_AnyProcessorFailureStripsEveryOutputCoverage`, `DispatcherRun_CancellationAfterOutputCreationPreventsPublication`. |
| Real packed regular/controller finalization and failing-test exit preservation | `SessionFinalization_NegotiatedCoverageReachesPackedConsumer`, `ControllerFinalization_CoverageIsDrainedToPackedConsumer`, `SessionFinalization_UnsupportedPeerReceivesNoCoverage`. |
| Real packed metadata/provenance and partial-run suppression | `Dispatcher_CompleteFixtureMergeCouplesCoverageToExactProvenance`, `Dispatcher_UnsupportedOrTruncatedJobOmitsCoverageMetadata`. The producer and SDK peer are explicitly synthetic fixtures, not a real coverage collector or SDK. |

The focused unit build and discovery/execution exited **0** on Release/net8.0:
**308 executed, 308 passed, zero failed/skipped**, comprising the 214 existing cases and
94 new cases. The package-consuming acceptance build and execution exited **0** on
Release/net11.0: **14 planned/discovered/executed, 14 passed, zero failed/skipped**.
Both assemblies retained method-level parallelization, observed with 32 workers. New
resources use per-test streams, mocks, GUID paths and child processes; no global-state
mutation or scheduling exclusion was added.

The acceptance assets use only the real packed MTP dependency and C# 12. They compile and
execute both the original four-argument constructor and the new five-argument constructor.
Before cleanup, every case verifies its `project.assets.json` package identity and effective
cache path, hashes the cached `.nupkg` against the local shipping package, compares the
output DLL to the package's net11.0 payload, and asserts the child reported that actual loaded
assembly path. All 14 reports recorded net11.0 DLL SHA-256
`118F19919CC4C9AE41869AFE4D34A00EDE8FFB8B8EF1A927CE2F361241D7D594`.
The execution gate also reconciled all 14 source-declared names with fresh TRX results.

The successful execution commands, after the configuration-matched build/pack, were:

```powershell
$unitFilter = 'ClassName=Microsoft.Testing.Platform.UnitTests.DotnetTestConnectionTests|ClassName=Microsoft.Testing.Platform.UnitTests.DotnetTestDataConsumerTests|ClassName=Microsoft.Testing.Platform.UnitTests.Extensions.ArtifactPostProcessing.ArtifactPostProcessingTests|ClassName=Microsoft.Testing.Platform.UnitTests.ProtocolTests|ClassName=Microsoft.Testing.Platform.UnitTests.ProtocolEdgeCaseTests'
& .\.dotnet\dotnet.exe artifacts\bin\Microsoft.Testing.Platform.UnitTests\Release\net8.0\Microsoft.Testing.Platform.UnitTests.dll --filter $unitFilter --report-trx --report-trx-filename coverage-units.trx --results-directory $unitEvidence --show-test-results failed

$acceptanceFilter = 'ClassName=Microsoft.Testing.Platform.Acceptance.IntegrationTests.DotnetTestPipe.DotnetTestPipeCoverageSummaryTests'
& .\.dotnet\dotnet.exe artifacts\bin\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Release\net11.0\Microsoft.Testing.Platform.Acceptance.IntegrationTests.dll --filter $acceptanceFilter --report-trx --report-trx-filename coverage-acceptance.trx --results-directory $acceptanceEvidence --show-test-results failed
```

`$unitEvidence` and `$acceptanceEvidence` identify the retained task-owned result directories.
The acceptance host was built through `eng\build.ps1 -restore -build -c Release` with its
specific project, `SupportedNetFrameworks=net11.0`, `SkipUapTargetFramework=true`,
`EnableCodeCoverage=false`, `SkipRuntimesInstall=true`, and a unique binlog. The pinned
11.0.0-rc.2.26504.105 runtime was bootstrapped locally before execution.

#### Test-value evidence

The behavior-removal experiment retained all APIs, tests and assertions. It removed only
the measurement send from `DotnetTestDataConsumer` and the metadata argument when
`ValidateProcessedArtifact` reconstructs a normalized artifact. A task-owned comparison
checkout used the same five-class selector and Release/net8.0 toolchain. The comparison
feature patch has SHA-256
`C26BDC3366C1972109F39B662AFBF7664783F40A792DD762DB4A1201073EBCF2`;
the mutation/harness patch has SHA-256
`879B5DAFE7215865D3EDBBD251DA195259E02081DD8EC3E3D17976038AB16413`.
All five changed unit-test source files were byte-identical between variants.

| Contract / test case | Counterfactual | Without behavior | Correct implementation | Evidence / gap |
| --- | --- | --- | --- | --- |
| Streamed identity/counts: `OverallCoverageIdentity`, `ModuleCoverageIdentity`, `ConsumeAsync_CoverageUpdatesRemainReplacementsWithDistinctIdentities` | Omit the negotiated measurement send, keeping registration and API shape. | **Executed: detected.** All three cases fail on absent message/cardinality assertions. | All three pass. | Same source, selector and assertions; no setup/compiler failure counted as red. |
| Validated artifact metadata: `ValidateProcessedArtifact_NormalizesPathWithoutDroppingCoverage`, `CompleteNegotiatedCoverage`, `EmptyListDoesNotInventZeroCounts`, `DispatcherRun_CompleteCoverageDoesNotOverrideFailingRunSummary` | Normalize through the legacy constructor, dropping only metadata. | **Executed: detected.** All four cases fail on missing expected metadata. | All four pass. | Both variants compiled with zero warnings/errors; original inputs and existing verdict policy remain protected. |

The red run exited **2** with **308 executed, 301 passed, 7 failed, zero skipped**.
After restoring only the two owned production edits and forcing a comparison rebuild,
the green run exited **0** with **308 executed, 308 passed, zero failed/skipped**.
Stable method identities and data-row multiplicities match; one pre-existing timing-property
case includes the current timestamp in its display label and was reconciled by method
identity rather than requiring identical timestamps. Every one of the seven intended failing
case labels uniquely passes in the green report.

The counterfactual DLL SHA-256 was
`EF67ACFC10BCF0800D61EF5CC0CD6B6489E0533AD364CAB385016C80CF0EA9DC`;
the freshly rebuilt restored DLL was
`EAA493D9BC4FEBE9079A163F7863EEC0C8DC46F503085B2C2E52F9AE5CD94F44`.
An initial restored-source attempt reused the mutant output because copying preserved older
timestamps; that attempt is not green evidence. The forced rebuild and changed binary hash
establish that the final green run used the restored implementation.

Initial external-source injection attempts hit analyzer/import setup failures and were
abandoned, not counted as behavioral failures. The standard comparison checkout needed only
an explicit higher `global_level` for its identical `.globalconfig` value because it is nested
under the original checkout; this harness-only setting remained identical in red and green.
Raw binlogs, TRX reports, source hashes and patches are retained in session artifacts. No
counterfactual production change is delivered.

The coordinating SDK session subsequently completed **11 real composed-product cases**
using the built SDK redist, this MTP `2.6.0-preview.coverage-summary.20261027.1` package, and
the genuine local collector `18.13.0-preview.coverage-summary.20261027.1` package. Inspection
of its retained `aligned-feature-verified-20261027-141856/evidence.json` found all checks
passing, with SHA-256
`96013CEB641AE57509CA95D2C813B0B8EF2F768872FAD36D9126A9444E4542E9`.
These are coordinator-owned executions, not results produced by the synthetic fixture tests.

The real single-app result is `50.0%` (6/12). The two applications each cover half of the same
library; the actual merged artifact contains 12/12 and prints `Total - Line: 100.0%` exactly
once in solution, managed-module, and Native AOT module paths. Both original artifacts
remain. Post-processing suppression and no-coverage paths invent no total; test failures
retain exit 2, zero-test selection retains exit 8, and cancellation retains exit 3. The
coordinator also reports 245 managed and 30 ILC-compiled SDK coverage cases passing.

The earlier stock collector pack's missing ARM64 native inputs no longer block this local
composed proof. No published package was patched and no global Visual Studio installation
was attempted. Official package release/flow was not requested and remains outside this
local implementation handoff.

### Open questions and delivery ownership

- Local source validation targets the current TestFX `main` baseline. Official package flow,
  release-branch validation, rollout, and any servicing/backport decision remain with the SDK
  coordinator; unpublished local packages are not shipping-readiness evidence.
- The coordinated SDK initially references MTP `2.6.0-preview.26519.1` and MSTest
  `4.6.0-preview.26519.1`. Local validation must align those dependencies with the actual
  TestFX and collector packages produced for this contract and record their provenance.
