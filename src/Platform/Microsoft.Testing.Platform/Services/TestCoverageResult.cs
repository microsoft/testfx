// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Services;

/// <summary>
/// The single, application-scoped consumer that correlates the raw coverage messages
/// (<see cref="TestCoverageMessage"/>, <see cref="TestCoverageThresholdMessage"/>,
/// <see cref="TestCoverageReportMessage"/>) into the <see cref="ITestCoverageResult"/> read model. It is
/// the single source of truth for coverage; the terminal output device and the exit-code policy both
/// read from it rather than buffering their own copies.
/// </summary>
internal sealed class TestCoverageResult : ITestCoverageResult, IDataConsumer
{
    // Full correlation key per RFC 019: (SessionUid, ProducerId, Scope, Metric, CustomMetricName when
    // Metric == Custom). Last write wins for a duplicate full key.
    private readonly Dictionary<MeasurementKey, TestCoverageMessage> _measurements = [];

    // Preserves first-seen order so the rendered summary is deterministic across runs.
    private readonly List<MeasurementKey> _measurementOrder = [];

    private readonly List<TestCoverageThresholdMessage> _thresholds = [];

    private readonly Dictionary<ReportKey, CoverageReportReference> _reports = [];
    private readonly List<ReportKey> _reportOrder = [];

    public string Uid => nameof(TestCoverageResult);

    public string Version => PlatformVersion.Version;

    public string DisplayName => "Test coverage result";

    public string Description => "Consumes and correlates test coverage data, threshold results, and report references.";

    public Type[] DataTypesConsumed { get; } =
        [typeof(TestCoverageMessage), typeof(TestCoverageThresholdMessage), typeof(TestCoverageReportMessage)];

    public IReadOnlyList<TestCoverageThresholdMessage> Thresholds => _thresholds;

    public bool HasThresholdFailure
    {
        get
        {
            foreach (TestCoverageThresholdMessage threshold in _thresholds)
            {
                if (!threshold.Passed)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public CoverageScopeSummary? Overall
    {
        get
        {
            foreach (CoverageScopeSummary summary in Scopes)
            {
                if (summary.Scope.Level == CoverageScopeLevel.Overall)
                {
                    return summary;
                }
            }

            return null;
        }
    }

    public IReadOnlyList<CoverageScopeSummary> Scopes
    {
        get
        {
            // Group correlated measurements by (SessionUid, Scope) preserving first-seen order. Two
            // sessions reporting the same scope are kept as separate summaries; ContainerHint is not part
            // of scope identity so hints from different producers never split a scope.
            var order = new List<(string Session, CoverageScope Scope)>();
            var groups = new Dictionary<(string Session, CoverageScope Scope), List<CoverageMetricResult>>();

            foreach (MeasurementKey key in _measurementOrder)
            {
                TestCoverageMessage message = _measurements[key];
                (string, CoverageScope) groupKey = (message.SessionUid.Value, message.Scope);
                if (!groups.TryGetValue(groupKey, out List<CoverageMetricResult>? metrics))
                {
                    metrics = [];
                    groups[groupKey] = metrics;
                    order.Add(groupKey);
                }

                metrics.Add(new CoverageMetricResult(
                    message.Metric,
                    message.CoveredCount,
                    message.CoverableCount,
                    message.ProducerId,
                    message.CustomMetricName));
            }

            var result = new List<CoverageScopeSummary>(order.Count);
            foreach ((string Session, CoverageScope Scope) groupKey in order)
            {
                result.Add(new CoverageScopeSummary(groupKey.Scope, groups[groupKey]));
            }

            return result;
        }
    }

    public IReadOnlyList<CoverageReportReference> Reports
    {
        get
        {
            var result = new List<CoverageReportReference>(_reportOrder.Count);
            foreach (ReportKey key in _reportOrder)
            {
                result.Add(_reports[key]);
            }

            return result;
        }
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    /// <summary>
    /// Clears all accumulated coverage data. Used to reset the per-session state between hot-reload
    /// cycles, which reuse the same application-scoped instance.
    /// </summary>
    public void Reset()
    {
        _measurements.Clear();
        _measurementOrder.Clear();
        _thresholds.Clear();
        _reports.Clear();
        _reportOrder.Clear();
    }

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        switch (value)
        {
            case TestCoverageMessage coverage:
                var measurementKey = new MeasurementKey(
                    coverage.SessionUid.Value,
                    coverage.ProducerId,
                    coverage.Scope.Level,
                    coverage.Scope.Name,
                    coverage.Metric,
                    coverage.Metric == CoverageMetric.Custom ? coverage.CustomMetricName : null);

                // Last write wins on a duplicate full key, keeping the original position for stable order.
                if (!_measurements.ContainsKey(measurementKey))
                {
                    _measurementOrder.Add(measurementKey);
                }

                _measurements[measurementKey] = coverage;
                break;

            case TestCoverageThresholdMessage threshold:
                _thresholds.Add(threshold);
                break;

            case TestCoverageReportMessage report:
                var reportKey = new ReportKey(report.SessionUid.Value, report.ProducerId, report.ReportPath);
                if (!_reports.ContainsKey(reportKey))
                {
                    _reportOrder.Add(reportKey);
                }

                _reports[reportKey] = new CoverageReportReference(
                    report.ReportPath,
                    report.Format,
                    report.ProducerId,
                    report.CustomFormatName);
                break;
        }

        return Task.CompletedTask;
    }

    private readonly record struct MeasurementKey(
        string Session,
        string ProducerId,
        CoverageScopeLevel Level,
        string? Name,
        CoverageMetric Metric,
        string? CustomMetricName);

    private readonly record struct ReportKey(string Session, string ProducerId, string Path);
}
