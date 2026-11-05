// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias serverclient;

using ClientMessages = serverclient::Microsoft.Testing.Platform.Extensions.Messages;
using SessionUid = serverclient::Microsoft.Testing.Platform.TestHost.SessionUid;

namespace Microsoft.Testing.Platform.ServerMode.Client.Sources.UnitTests;

[TestClass]
public sealed class CoverageModelMutationTests
{
    [TestMethod]
    [DataRow(0L, 0L, false, 0d)]
    [DataRow(0L, 1L, true, 0d)]
    [DataRow(1L, 4L, true, 25d)]
    [DataRow(4L, 4L, true, 100d)]
    [DataRow(long.MaxValue, long.MaxValue, true, 100d)]
    public void Measurement_Counts_DistinguishNoDataZeroPartialAndFullCoverage(long covered, long coverable, bool hasData, double percentage)
    {
        var scope = new ClientMessages.CoverageScope(ClientMessages.CoverageScopeLevel.File, "file.cs", "module");
        var measurement = new ClientMessages.TestCoverageMessage(
            new SessionUid("session"), scope, ClientMessages.CoverageMetric.Line, covered, coverable, "collector");

        Assert.AreEqual(covered, measurement.CoveredCount);
        Assert.AreEqual(coverable, measurement.CoverableCount);
        Assert.AreEqual(hasData, measurement.HasCoverableData);
        Assert.AreEqual(percentage, measurement.Percentage);
        Assert.AreEqual(scope, measurement.Scope);
        Assert.AreEqual(ClientMessages.CoverageMetric.Line, measurement.Metric);
        Assert.AreEqual("collector", measurement.ProducerId);
        Assert.IsNull(measurement.CustomMetricName);
        Assert.AreEqual("session", measurement.SessionUid.Value);
        Assert.AreEqual("Test coverage", measurement.DisplayName);
        Assert.AreEqual("Reports a code coverage measurement for a scope.", measurement.Description);
    }

    [TestMethod]
    [DataRow(-1L, 0L, "coveredCount")]
    [DataRow(0L, -1L, "coverableCount")]
    [DataRow(2L, 1L, "coveredCount")]
    [DataRow(long.MaxValue, long.MaxValue - 1, "coveredCount")]
    public void Measurement_InvalidCounts_RejectTheOffendingParameter(long covered, long coverable, string parameter)
    {
        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new ClientMessages.TestCoverageMessage(new SessionUid("session"), ClientMessages.CoverageScope.Overall,
                ClientMessages.CoverageMetric.Branch, covered, coverable, "collector"));

        Assert.AreEqual(parameter, exception.ParamName);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "Missing producer")]
    [DataRow("", DisplayName = "Empty producer")]
    public void CoverageMessages_MissingProducer_RejectUncorrelatedData(string? producer)
    {
        ArgumentException measurement = Assert.ThrowsExactly<ArgumentException>(() =>
            new ClientMessages.TestCoverageMessage(new SessionUid("session"), ClientMessages.CoverageScope.Overall,
                ClientMessages.CoverageMetric.Line, 0, 0, producer!));
        ArgumentException report = Assert.ThrowsExactly<ArgumentException>(() =>
            new ClientMessages.TestCoverageReportMessage(new SessionUid("session"), "coverage.xml",
                ClientMessages.CoverageReportFormat.Cobertura, producer!));
        ArgumentException threshold = Assert.ThrowsExactly<ArgumentException>(() =>
            CreateThreshold(50, 50, producer: producer!));

        foreach (ArgumentException exception in new[] { measurement, report, threshold })
        {
            Assert.AreEqual("producerId", exception.ParamName);
            Assert.StartsWith("A stable, non-empty producer id is required.", exception.Message);
        }
    }

    [TestMethod]
    [DataRow(null, DisplayName = "Missing custom metric")]
    [DataRow("", DisplayName = "Empty custom metric")]
    [DataRow(" ", DisplayName = "Whitespace custom metric")]
    [DataRow("\t", DisplayName = "Tab custom metric")]
    public void CoverageMessages_CustomMetricRequiresNonWhitespaceName(string? name)
    {
        ArgumentException measurement = Assert.ThrowsExactly<ArgumentException>(() =>
            new ClientMessages.TestCoverageMessage(new SessionUid("session"), ClientMessages.CoverageScope.Overall,
                ClientMessages.CoverageMetric.Custom, 1, 2, "collector", name));
        ArgumentException threshold = Assert.ThrowsExactly<ArgumentException>(() =>
            CreateThreshold(50, 50, metric: ClientMessages.CoverageMetric.Custom, customMetricName: name));

        foreach (ArgumentException exception in new[] { measurement, threshold })
        {
            Assert.AreEqual("customMetricName", exception.ParamName);
            Assert.StartsWith("A custom metric name is required when metric is Custom.", exception.Message);
        }
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("custom")]
    public void CoverageMessages_StandardMetricForbidsCustomName(string name)
    {
        ArgumentException measurement = Assert.ThrowsExactly<ArgumentException>(() =>
            new ClientMessages.TestCoverageMessage(new SessionUid("session"), ClientMessages.CoverageScope.Overall,
                ClientMessages.CoverageMetric.Branch, 1, 2, "collector", name));
        ArgumentException threshold = Assert.ThrowsExactly<ArgumentException>(() =>
            CreateThreshold(50, 50, customMetricName: name));

        foreach (ArgumentException exception in new[] { measurement, threshold })
        {
            Assert.AreEqual("customMetricName", exception.ParamName);
            Assert.StartsWith("A custom metric name is only valid when metric is Custom.", exception.Message);
        }
    }

    [TestMethod]
    public void CoverageMessages_NamedCustomMetric_PreservesIdentity()
    {
        var measurement = new ClientMessages.TestCoverageMessage(new SessionUid("session"), ClientMessages.CoverageScope.Overall,
            ClientMessages.CoverageMetric.Custom, 1, 2, "collector", "mcdc");
        ClientMessages.TestCoverageThresholdMessage threshold = CreateThreshold(50, 50,
            metric: ClientMessages.CoverageMetric.Custom, customMetricName: "mcdc");

        Assert.AreEqual(ClientMessages.CoverageMetric.Custom, measurement.Metric);
        Assert.AreEqual("mcdc", measurement.CustomMetricName);
        Assert.AreEqual(ClientMessages.CoverageMetric.Custom, threshold.Metric);
        Assert.AreEqual("mcdc", threshold.CustomMetricName);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "Missing report path")]
    [DataRow("", DisplayName = "Empty report path")]
    public void Report_MissingPath_RejectsTheReportPath(string? path)
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() =>
            new ClientMessages.TestCoverageReportMessage(new SessionUid("session"), path!,
                ClientMessages.CoverageReportFormat.Cobertura, "collector"));

        Assert.AreEqual("reportPath", exception.ParamName);
        Assert.StartsWith("A report path is required.", exception.Message);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "Missing custom format")]
    [DataRow("", DisplayName = "Empty custom format")]
    [DataRow(" ", DisplayName = "Whitespace custom format")]
    public void Report_CustomFormatRequiresNonWhitespaceName(string? name)
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() =>
            new ClientMessages.TestCoverageReportMessage(new SessionUid("session"), "coverage.bin",
                ClientMessages.CoverageReportFormat.Custom, "collector", name));

        Assert.AreEqual("customFormatName", exception.ParamName);
        Assert.StartsWith("A custom format name is required when format is Custom.", exception.Message);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("custom")]
    public void Report_StandardFormatForbidsCustomName(string name)
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() =>
            new ClientMessages.TestCoverageReportMessage(new SessionUid("session"), "coverage.xml",
                ClientMessages.CoverageReportFormat.Cobertura, "collector", name));

        Assert.AreEqual("customFormatName", exception.ParamName);
        Assert.StartsWith("A custom format name is only valid when format is Custom.", exception.Message);
    }

    [TestMethod]
    [DataRow(ClientMessages.CoverageReportFormat.Cobertura, null)]
    [DataRow(ClientMessages.CoverageReportFormat.Custom, "custom-format")]
    public void Report_ValidFormat_PreservesArtifactAndCollectorIdentity(ClientMessages.CoverageReportFormat format, string? name)
    {
        var report = new ClientMessages.TestCoverageReportMessage(new SessionUid("session"), "coverage.xml", format, "collector", name);

        Assert.AreEqual("coverage.xml", report.ReportPath);
        Assert.AreEqual(format, report.Format);
        Assert.AreEqual(name, report.CustomFormatName);
        Assert.AreEqual("collector", report.ProducerId);
        Assert.AreEqual("session", report.SessionUid.Value);
        Assert.AreEqual("Test coverage report", report.DisplayName);
        Assert.AreEqual("References a coverage report artifact.", report.Description);
    }

    [TestMethod]
    public void Scope_Identity_IsOrdinalLevelAndNameButNotContainerHint()
    {
        var scope = new ClientMessages.CoverageScope(ClientMessages.CoverageScopeLevel.Module, "module", "first");
        var equal = new ClientMessages.CoverageScope(ClientMessages.CoverageScopeLevel.Module, "module", "second");
        var differentCase = new ClientMessages.CoverageScope(ClientMessages.CoverageScopeLevel.Module, "MODULE");
        var differentLevel = new ClientMessages.CoverageScope(ClientMessages.CoverageScopeLevel.File, "module");

        Assert.AreEqual(ClientMessages.CoverageScopeLevel.Module, scope.Level);
        Assert.AreEqual("module", scope.Name);
        Assert.AreEqual("first", scope.ContainerHint);
        Assert.IsTrue(scope.Equals(equal));
        Assert.IsTrue(scope.Equals((object)equal));
        Assert.IsTrue(scope == equal);
        Assert.IsFalse(scope != equal);
        Assert.AreEqual(scope.GetHashCode(), equal.GetHashCode());
        foreach (ClientMessages.CoverageScope different in new[] { differentCase, differentLevel, ClientMessages.CoverageScope.Overall })
        {
            Assert.IsFalse(scope.Equals(different));
            Assert.IsFalse(scope.Equals((object)different));
            Assert.IsFalse(scope == different);
            Assert.IsTrue(scope != different);
        }

        Assert.IsFalse(scope.Equals(null));
        Assert.IsFalse(scope.Equals("module"));
        Assert.AreEqual(ClientMessages.CoverageScopeLevel.Overall, ClientMessages.CoverageScope.Overall.Level);
        Assert.IsNull(ClientMessages.CoverageScope.Overall.Name);
    }

    [TestMethod]
    [DataRow(ClientMessages.CoverageScopeLevel.Overall, "name", "The Overall scope must not have a name.")]
    [DataRow(ClientMessages.CoverageScopeLevel.Overall, "", "The Overall scope must not have a name.")]
    [DataRow(ClientMessages.CoverageScopeLevel.File, null, "A name is required for scope level 'File'.")]
    [DataRow(ClientMessages.CoverageScopeLevel.File, "", "A name is required for scope level 'File'.")]
    public void Scope_InvalidName_RejectsInvalidScopeIdentity(ClientMessages.CoverageScopeLevel level, string? name, string message)
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => new ClientMessages.CoverageScope(level, name));

        Assert.AreEqual("name", exception.ParamName);
        Assert.StartsWith(message, exception.Message);
    }

    [TestMethod]
    [DataRow(0d, 0d, true)]
    [DataRow(0d, 1d, false)]
    [DataRow(49d, 50d, false)]
    [DataRow(50d, 50d, true)]
    [DataRow(51d, 50d, true)]
    [DataRow(100d, 100d, true)]
    public void Threshold_PercentageComparison_IncludesExactBoundary(double expectedActualPercentage, double expectedRequiredPercentage, bool passed)
    {
        ClientMessages.TestCoverageThresholdMessage threshold = CreateThreshold(expectedActualPercentage, expectedRequiredPercentage);

        Assert.AreEqual(expectedActualPercentage, threshold.ActualPercentage);
        Assert.AreEqual(expectedRequiredPercentage, threshold.RequiredPercentage);
        Assert.AreEqual(passed, threshold.Passed);
        Assert.IsTrue(threshold.HasCoverableData);
        Assert.IsTrue(threshold.TreatNoDataAsFailure);
        Assert.AreEqual(ClientMessages.CoverageScope.Overall, threshold.Scope);
        Assert.AreEqual(ClientMessages.CoverageMetric.Line, threshold.Metric);
        Assert.AreEqual(ClientMessages.CoverageAggregation.None, threshold.Aggregation);
        Assert.IsNull(threshold.AggregatedOver);
        Assert.AreEqual("collector", threshold.ProducerId);
        Assert.AreEqual("session", threshold.SessionUid.Value);
        Assert.AreEqual("Test coverage threshold", threshold.DisplayName);
        Assert.AreEqual("Reports the result of a coverage threshold evaluation.", threshold.Description);
    }

    [TestMethod]
    [DataRow(-1d)]
    [DataRow(101d)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    public void Threshold_InvalidPercentages_RejectActualAndRequiredIndependently(double value)
    {
        ArgumentOutOfRangeException actual = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CreateThreshold(value, 50));
        ArgumentOutOfRangeException required = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CreateThreshold(50, value));
        ArgumentOutOfRangeException noDataRequired = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CreateThreshold(0, value, hasData: false));

        Assert.AreEqual("actualPercentage", actual.ParamName);
        Assert.AreEqual("requiredPercentage", required.ParamName);
        Assert.AreEqual("requiredPercentage", noDataRequired.ParamName);
        foreach (ArgumentOutOfRangeException exception in new[] { actual, required, noDataRequired })
        {
            Assert.AreEqual(value, exception.ActualValue);
            Assert.StartsWith("Percentage must be a number in the range", exception.Message);
        }
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void Threshold_NoCoverableData_NormalizesPercentageAndUsesExplicitPolicy(bool noDataIsFailure, bool passed)
    {
        ClientMessages.TestCoverageThresholdMessage threshold = CreateThreshold(
            double.NaN, 90, hasData: false, noDataIsFailure: noDataIsFailure);

        Assert.AreEqual(0d, threshold.ActualPercentage);
        Assert.AreEqual(90d, threshold.RequiredPercentage);
        Assert.IsFalse(threshold.HasCoverableData);
        Assert.AreEqual(noDataIsFailure, threshold.TreatNoDataAsFailure);
        Assert.AreEqual(passed, threshold.Passed);
    }

    [TestMethod]
    [DataRow(ClientMessages.CoverageAggregation.None, ClientMessages.CoverageScopeLevel.Module)]
    [DataRow(ClientMessages.CoverageAggregation.Minimum, null)]
    [DataRow(ClientMessages.CoverageAggregation.Minimum, ClientMessages.CoverageScopeLevel.Overall)]
    public void Threshold_InvalidPopulation_RejectsInconsistentAggregation(ClientMessages.CoverageAggregation aggregation, ClientMessages.CoverageScopeLevel? population)
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() =>
            CreateThreshold(75, 50, aggregation: aggregation, population: population));

        Assert.AreEqual("aggregatedOver", exception.ParamName);
        Assert.StartsWith(
            aggregation == ClientMessages.CoverageAggregation.None
            ? "A non-aggregate evaluation must not specify a population."
            : "An aggregate evaluation must specify a non-overall aggregated population.", exception.Message);
    }

    [TestMethod]
    [DataRow(ClientMessages.CoverageAggregation.Total)]
    [DataRow(ClientMessages.CoverageAggregation.Minimum)]
    [DataRow(ClientMessages.CoverageAggregation.Average)]
    [DataRow(ClientMessages.CoverageAggregation.Maximum)]
    public void Threshold_Aggregate_PreservesNamedPopulation(ClientMessages.CoverageAggregation aggregation)
    {
        ClientMessages.TestCoverageThresholdMessage threshold = CreateThreshold(
            75, 50, aggregation: aggregation, population: ClientMessages.CoverageScopeLevel.File);

        Assert.AreEqual(aggregation, threshold.Aggregation);
        Assert.AreEqual(ClientMessages.CoverageScopeLevel.File, threshold.AggregatedOver);
        Assert.IsTrue(threshold.Passed);
    }

    [TestMethod]
    public void Threshold_ExactCountResult_IsNotReplacedByRoundedDoubleComparison()
    {
        const decimal actual = 66.666666666666666666666666666m;
        const decimal required = 66.666666666666666666666666667m;
        var threshold = new ClientMessages.TestCoverageThresholdMessage(
            new SessionUid("session"), ClientMessages.CoverageMetric.Branch, actual, required, true, "collector", false);

        Assert.AreEqual(actual, threshold.ExactActualPercentage);
        Assert.AreEqual(required, threshold.ExactRequiredPercentage);
        Assert.AreEqual((double)actual, threshold.ActualPercentage);
        Assert.AreEqual((double)required, threshold.RequiredPercentage);
        Assert.IsFalse(threshold.Passed);
    }

    private static ClientMessages.TestCoverageThresholdMessage CreateThreshold(
        double actual,
        double required,
        bool hasData = true,
        bool noDataIsFailure = true,
        string producer = "collector",
        ClientMessages.CoverageMetric metric = ClientMessages.CoverageMetric.Line,
        string? customMetricName = null,
        ClientMessages.CoverageAggregation aggregation = ClientMessages.CoverageAggregation.None,
        ClientMessages.CoverageScopeLevel? population = null)
        => new(new SessionUid("session"), ClientMessages.CoverageScope.Overall, metric,
            aggregation, actual, required, hasData, producer, population, noDataIsFailure, customMetricName);
}
