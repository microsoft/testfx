# RFC 019 - Code coverage messages & consumer model for Microsoft.Testing.Platform

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
per-line/per-branch** layer the model collapses and is genuinely closed and universal —
method/class/statement/region/block coverage are all different *bucketings/aggregations* of the
same per-line/per-branch primitives.

Therefore the open-vs-closed question is answered by **choosing the layer**:

- **Raw layer** → closed and universal, but it *is* Cobertura/lcov. Don't reinvent it in
  messages — reference the artifact and let deep consumers parse it.
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
6. **Scope-aware, but not a strict tree.** Scopes are addressed by `(level, name)` across
   Overall / Module / Assembly / Namespace / Type / File. These levels do **not** form a
   single parent tree — a file can contain multiple types and a partial type can span several
   files — so the contract models scope identity, not a forced hierarchy (see the scope value
   type below).
7. **Guideline compliance.** No `init` accessors on new public API. User-facing terminal
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
public sealed class TestCoverageMessage : PropertyBagData
{
    public TestCoverageMessage(
        CoverageScope scope,
        CoverageMetric metric,
        long coveredCount,
        long coverableCount,
        string producerId,
        string? customMetricName = null)
        : base("Test coverage", "Reports a code coverage measurement for a scope.")
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
public sealed class TestCoverageThresholdMessage : PropertyBagData
{
    public TestCoverageThresholdMessage(
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
        : base("Test coverage threshold", "Reports the result of a coverage threshold evaluation.")
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

        if (aggregation is not CoverageAggregation.None && aggregatedOver is null)
        {
            throw new ArgumentException("An aggregate evaluation must specify the aggregated population.", nameof(aggregatedOver));
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
public sealed class TestCoverageReportMessage : PropertyBagData
{
    public TestCoverageReportMessage(
        string reportPath,
        CoverageReportFormat format,
        string producerId,
        string? customFormatName = null)
        : base("Test coverage report", "References a coverage report artifact.")
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
    /// <summary>Overall (whole-run) summary, if reported.</summary>
    CoverageScopeSummary? Overall { get; }

    /// <summary>Per-scope summaries with all metrics correlated per scope.</summary>
    IReadOnlyList<CoverageScopeSummary> Scopes { get; }

    /// <summary>All threshold evaluations (passed and failed).</summary>
    IReadOnlyList<TestCoverageThresholdMessage> Thresholds { get; }

    /// <summary>Pointers to rich report artifacts for deep parsing.</summary>
    IReadOnlyList<CoverageReportReference> Reports { get; }

    /// <summary><see langword="true"/> when any threshold failed (drives exit code).</summary>
    bool HasThresholdFailure { get; }
}

public sealed class CoverageScopeSummary
{
    // Constructed only by the platform correlator; consumers receive it read-only.
    internal CoverageScopeSummary(CoverageScope scope, IReadOnlyList<CoverageMetricResult> metrics)
    {
        Scope = scope;
        Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    public CoverageScope Scope { get; }

    public IReadOnlyList<CoverageMetricResult> Metrics { get; }

    /// <summary>
    /// Convenience lookup for a well-known metric, e.g. <c>summary[CoverageMetric.Line]</c>;
    /// null if absent. Throws for <see cref="CoverageMetric.Custom"/>, because every custom
    /// metric shares that enum value and is distinguished only by its name — use
    /// <see cref="GetCustom(string)"/> instead. If a scope somehow carries the same metric from
    /// multiple producers, this returns the first; use <see cref="Metrics"/> to disambiguate by
    /// <see cref="CoverageMetricResult.ProducerId"/>.
    /// </summary>
    public CoverageMetricResult? this[CoverageMetric metric] { get; }

    /// <summary>Looks up a custom (proprietary) metric by its name; null if absent.</summary>
    public CoverageMetricResult? GetCustom(string customMetricName);
}

public sealed class CoverageMetricResult
{
    internal CoverageMetricResult(
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
    internal CoverageReportReference(
        string path,
        CoverageReportFormat format,
        string producerId,
        string? customFormatName = null)
    {
        Path = path;
        Format = format;
        ProducerId = producerId;
        CustomFormatName = customFormatName;
    }

    public string Path { get; }

    public CoverageReportFormat Format { get; }

    public string? CustomFormatName { get; }

    public string ProducerId { get; }
}
```

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

## Platform wiring

- Register a single `TestCoverageResult : ITestCoverageResult, IDataConsumer` as a common
  service + consumer in both the framework and test-host-controller pipelines. It is the single
  source of truth; the terminal reads from it rather than maintaining its own coverage lists.
- `TerminalOutputDevice` consumes `ITestCoverageResult` at session end and renders via
  `TerminalTestReporter.AppendCoverageSummary`, using **localized** `TerminalResources` strings
  and `CultureInfo.InvariantCulture` numeric formatting.
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
2. **`PropertyBagData` base.** If kept, should the messages actually populate the property bag
   (so generic bag consumers see the data), or move to a lighter base?
3. **Escape-hatch surface.** `Custom` enum member + `CustomMetricName` string (chosen here for
   being closed-by-default) vs. an open readonly-struct metric type — confirm.
4. **Value range convention.** This RFC standardizes on **0–100**; some tools use 0–1. Lock it
   and document on every percentage property.
