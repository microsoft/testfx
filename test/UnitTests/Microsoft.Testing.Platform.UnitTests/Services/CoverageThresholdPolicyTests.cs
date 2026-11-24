// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.UnitTests.Helpers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class CoverageThresholdPolicyTests
{
    private static readonly SessionUid Session = new("session");

    [TestMethod]
    public void Evaluate_NoOptions_DoesNotRequireCoverage()
    {
        (IReadOnlyList<TestCoverageThresholdMessage> Thresholds, IReadOnlyList<string> Errors) result = CoverageThresholdPolicy.Evaluate(new TestCommandLineOptions([]), [], Session);

        Assert.IsEmpty(result.Thresholds);
        Assert.IsEmpty(result.Errors);
    }

    [TestMethod]
    [DataRow(80L, 100L, "80", true)]
    [DataRow(79L, 100L, "80", false)]
    [DataRow(15_999L, 20_000L, "80", false)]
    [DataRow(15_999L, 20_000L, "79.995", true)]
    [DataRow(0L, 100L, "0", true)]
    [DataRow(100L, 100L, "100", true)]
    [DataRow(0L, 0L, "0", false)]
    [DataRow(312L, 625L, "49.92", true)]
    [DataRow(312L, 625L, "49.920000000000000000000000001", false)]
    [DataRow(long.MaxValue - 1, long.MaxValue, "100", false)]
    public void Evaluate_UsesUnroundedInclusivePercentages(long covered, long coverable, string required, bool passed)
    {
        CoverageScopeSummary summary = new(Session, CoverageScope.Overall, [new(CoverageMetric.Line, covered, coverable, "collector")]);
        (IReadOnlyList<TestCoverageThresholdMessage> Thresholds, IReadOnlyList<string> Errors) result = CoverageThresholdPolicy.Evaluate(Options(line: required), [summary], Session);

        TestCoverageThresholdMessage threshold = Assert.ContainsSingle(result.Thresholds);
        Assert.AreEqual(passed, threshold.Passed);
        Assert.AreEqual(CoverageMetric.Line, threshold.Metric);
        Assert.AreEqual(CoverageAggregation.None, threshold.Aggregation);
        Assert.AreEqual(Session, threshold.SessionUid);
        Assert.AreEqual(!passed, result.Errors.Count > 0);
    }

    [TestMethod]
    public void Evaluate_BothMetrics_EvaluatesIndependently()
    {
        CoverageScopeSummary summary = new(Session, CoverageScope.Overall,
            [new(CoverageMetric.Line, 8, 10, "collector"), new(CoverageMetric.Branch, 6, 10, "collector")]);

        (IReadOnlyList<TestCoverageThresholdMessage> Thresholds, IReadOnlyList<string> Errors) result = CoverageThresholdPolicy.Evaluate(Options(line: "80", branch: "70"), [summary], Session);

        Assert.HasCount(2, result.Thresholds);
        Assert.IsTrue(result.Thresholds[0].Passed);
        Assert.IsFalse(result.Thresholds[1].Passed);
        Assert.Contains("--coverage-threshold-branch", Assert.ContainsSingle(result.Errors));
    }

    [TestMethod]
    public void Evaluate_BranchOnly_DoesNotRequireLineCoverage()
    {
        CoverageScopeSummary summary = new(Session, CoverageScope.Overall, [new(CoverageMetric.Branch, 7, 10, "collector")]);

        (IReadOnlyList<TestCoverageThresholdMessage> Thresholds, IReadOnlyList<string> Errors) result = CoverageThresholdPolicy.Evaluate(Options(branch: "70"), [summary], Session);

        Assert.IsTrue(Assert.ContainsSingle(result.Thresholds).Passed);
        Assert.IsEmpty(result.Errors);
    }

    [TestMethod]
    public void Evaluate_DifferentMetricsFromDifferentProviders_AreIndependent()
    {
        CoverageScopeSummary summary = new(Session, CoverageScope.Overall,
            [new(CoverageMetric.Line, 8, 10, "line-collector"), new(CoverageMetric.Branch, 7, 10, "branch-collector")]);

        (IReadOnlyList<TestCoverageThresholdMessage> Thresholds, IReadOnlyList<string> Errors) lineOnly = CoverageThresholdPolicy.Evaluate(Options(line: "80"), [summary], Session);
        (IReadOnlyList<TestCoverageThresholdMessage> Thresholds, IReadOnlyList<string> Errors) both = CoverageThresholdPolicy.Evaluate(Options(line: "80", branch: "70"), [summary], Session);

        Assert.IsTrue(Assert.ContainsSingle(lineOnly.Thresholds).Passed);
        Assert.IsEmpty(lineOnly.Errors);
        Assert.HasCount(2, both.Thresholds);
        Assert.IsTrue(both.Thresholds.All(threshold => threshold.Passed));
        Assert.IsEmpty(both.Errors);
    }

    [TestMethod]
    public void Evaluate_MissingMetric_FailsExplicitly()
    {
        CoverageScopeSummary summary = new(Session, CoverageScope.Overall, [new(CoverageMetric.Line, 10, 10, "collector")]);

        (IReadOnlyList<TestCoverageThresholdMessage> Thresholds, IReadOnlyList<string> Errors) result = CoverageThresholdPolicy.Evaluate(Options(branch: "0"), [summary], Session);

        Assert.IsFalse(Assert.ContainsSingle(result.Thresholds).Passed);
        Assert.AreEqual(
            string.Format(CultureInfo.InvariantCulture, PlatformResources.CoverageThresholdMissingMeasurement, PlatformCommandLineProvider.CoverageThresholdBranchOptionKey),
            Assert.ContainsSingle(result.Errors));
    }

    [TestMethod]
    public void Evaluate_ModuleMeasurements_DoNotReplaceOverallMeasurement()
    {
        CoverageScopeSummary summary = new(Session, new(CoverageScopeLevel.Module, "module.dll"), [new(CoverageMetric.Line, 10, 10, "collector")]);

        (IReadOnlyList<TestCoverageThresholdMessage> Thresholds, IReadOnlyList<string> Errors) result = CoverageThresholdPolicy.Evaluate(Options(line: "80"), [summary], Session);

        Assert.IsFalse(Assert.ContainsSingle(result.Thresholds).Passed);
        Assert.AreEqual(
            string.Format(CultureInfo.InvariantCulture, PlatformResources.CoverageThresholdMissingMeasurement, PlatformCommandLineProvider.CoverageThresholdLineOptionKey),
            Assert.ContainsSingle(result.Errors));
    }

    [TestMethod]
    public void Evaluate_DifferentSession_DoesNotSatisfyRequirement()
    {
        CoverageScopeSummary summary = new(new SessionUid("other"), CoverageScope.Overall, [new(CoverageMetric.Line, 10, 10, "collector")]);

        (IReadOnlyList<TestCoverageThresholdMessage> Thresholds, IReadOnlyList<string> Errors) result = CoverageThresholdPolicy.Evaluate(Options(line: "80"), [summary], Session);

        Assert.IsFalse(Assert.ContainsSingle(result.Thresholds).Passed);
        Assert.AreEqual(
            string.Format(CultureInfo.InvariantCulture, PlatformResources.CoverageThresholdMissingMeasurement, PlatformCommandLineProvider.CoverageThresholdLineOptionKey),
            Assert.ContainsSingle(result.Errors));
    }

    [TestMethod]
    public void Evaluate_MultipleProviders_FailsRatherThanChoosingOrMerging()
    {
        CoverageScopeSummary summary = new(Session, CoverageScope.Overall,
            [new(CoverageMetric.Line, 10, 10, "first"), new(CoverageMetric.Line, 1, 10, "second")]);

        (IReadOnlyList<TestCoverageThresholdMessage> Thresholds, IReadOnlyList<string> Errors) result = CoverageThresholdPolicy.Evaluate(Options(line: "80"), [summary], Session);

        Assert.IsFalse(Assert.ContainsSingle(result.Thresholds).Passed);
        Assert.AreEqual(
            string.Format(CultureInfo.InvariantCulture, PlatformResources.CoverageThresholdAmbiguous, PlatformCommandLineProvider.CoverageThresholdLineOptionKey),
            Assert.ContainsSingle(result.Errors));
    }

    [TestMethod]
    public void Evaluate_ControllerWithMultipleSessions_DoesNotAggregate()
    {
        CoverageScopeSummary first = new(Session, CoverageScope.Overall, [new(CoverageMetric.Line, 10, 10, "collector")]);
        CoverageScopeSummary second = new(new SessionUid("second"), CoverageScope.Overall, [new(CoverageMetric.Line, 10, 10, "collector")]);

        (IReadOnlyList<TestCoverageThresholdMessage> Thresholds, IReadOnlyList<string> Errors) result = CoverageThresholdPolicy.Evaluate(Options(line: "80"), [first, second], sessionUid: null);

        Assert.IsFalse(Assert.ContainsSingle(result.Thresholds).Passed);
        Assert.AreEqual(
            string.Format(CultureInfo.InvariantCulture, PlatformResources.CoverageThresholdAmbiguous, PlatformCommandLineProvider.CoverageThresholdLineOptionKey),
            Assert.ContainsSingle(result.Errors));
    }

    [TestMethod]
    public async Task CoverageResult_LatestMeasurementAndReset_UpdateSharedVerdict()
    {
        TestCoverageResult result = new();
        result.ConfigureThresholds(Options(line: "80"), Session);
        IDataProducer producer = Mock.Of<IDataProducer>();

        Assert.IsTrue(result.HasThresholdFailure);

        await result.ConsumeAsync(producer, new TestCoverageMessage(Session, CoverageScope.Overall, CoverageMetric.Line, 8, 10, "collector"), CancellationToken.None);

        Assert.IsFalse(result.HasThresholdFailure);
        Assert.IsTrue(Assert.ContainsSingle(result.Thresholds).Passed);
        Assert.IsEmpty(result.GetThresholdErrors());

        await result.ConsumeAsync(producer, new TestCoverageMessage(Session, CoverageScope.Overall, CoverageMetric.Line, 7, 10, "collector"), CancellationToken.None);

        Assert.IsTrue(result.HasThresholdFailure);
        Assert.IsFalse(Assert.ContainsSingle(result.Thresholds).Passed);
        Assert.HasCount(1, result.GetThresholdErrors());

        result.Reset();

        Assert.IsFalse(result.HasThresholdFailure);
        Assert.IsEmpty(result.Thresholds);
        Assert.IsEmpty(result.GetThresholdErrors());
    }

    private static TestCommandLineOptions Options(string? line = null, string? branch = null)
    {
        Dictionary<string, string[]> options = [];
        if (line is not null)
        {
            options[PlatformCommandLineProvider.CoverageThresholdLineOptionKey] = [line];
        }

        if (branch is not null)
        {
            options[PlatformCommandLineProvider.CoverageThresholdBranchOptionKey] = [branch];
        }

        return new(options);
    }
}
