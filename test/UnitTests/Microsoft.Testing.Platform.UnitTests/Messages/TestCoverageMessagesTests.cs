// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TestCoverageMessagesTests
{
    private static readonly SessionUid SessionUid = new("session");

    [TestMethod]
    public void TestCoverageThresholdMessage_AggregatedOverOverall_ThrowsArgumentException()
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => new TestCoverageThresholdMessage(
                SessionUid,
                CoverageScope.Overall,
                CoverageMetric.Line,
                CoverageAggregation.Minimum,
                actualPercentage: 80,
                requiredPercentage: 75,
                hasCoverableData: true,
                producerId: "producer",
                aggregatedOver: CoverageScopeLevel.Overall));

        Assert.AreEqual("aggregatedOver", exception.ParamName);
    }

    [TestMethod]
    [DataRow(double.NaN)]
    [DataRow(-0.1)]
    [DataRow(100.1)]
    public void TestCoverageThresholdMessage_InvalidRequiredPercentage_ThrowsArgumentOutOfRangeException(double requiredPercentage)
    {
        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => CreateThreshold(actualPercentage: 80, requiredPercentage: requiredPercentage));

        Assert.AreEqual("requiredPercentage", exception.ParamName);
    }

    [TestMethod]
    [DataRow(double.NaN)]
    [DataRow(-0.1)]
    [DataRow(100.1)]
    public void TestCoverageThresholdMessage_InvalidActualPercentageWithCoverableData_ThrowsArgumentOutOfRangeException(double actualPercentage)
    {
        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => CreateThreshold(actualPercentage, requiredPercentage: 80));

        Assert.AreEqual("actualPercentage", exception.ParamName);
    }

    [TestMethod]
    public void TestCoverageThresholdMessage_NonAggregateWithPopulation_ThrowsArgumentException()
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => CreateThreshold(
                actualPercentage: 80,
                requiredPercentage: 75,
                aggregation: CoverageAggregation.None,
                aggregatedOver: CoverageScopeLevel.Module));

        Assert.AreEqual("aggregatedOver", exception.ParamName);
    }

    [TestMethod]
    public void TestCoverageThresholdMessage_AggregateWithoutPopulation_ThrowsArgumentException()
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => CreateThreshold(
                actualPercentage: 80,
                requiredPercentage: 75,
                aggregation: CoverageAggregation.Minimum,
                aggregatedOver: null));

        Assert.AreEqual("aggregatedOver", exception.ParamName);
    }

    [TestMethod]
    [DataRow(-1L, -1L, "coverableCount")]
    [DataRow(-1L, 0L, "coveredCount")]
    [DataRow(2L, 1L, "coveredCount")]
    public void CoverageMetricResult_InvalidCounts_ThrowsArgumentOutOfRangeException(
        long coveredCount,
        long coverableCount,
        string expectedParamName)
    {
        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new CoverageMetricResult(CoverageMetric.Line, coveredCount, coverableCount, "producer"));

        Assert.AreEqual(expectedParamName, exception.ParamName);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void CoverageMetricResult_MissingProducerId_ThrowsArgumentException(string? producerId)
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => new CoverageMetricResult(CoverageMetric.Line, 1, 1, producerId!));

        Assert.AreEqual("producerId", exception.ParamName);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public void CoverageMetricResult_CustomMetricWithoutName_ThrowsArgumentException(string? customMetricName)
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => new CoverageMetricResult(CoverageMetric.Custom, 1, 1, "producer", customMetricName));

        Assert.AreEqual("customMetricName", exception.ParamName);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("custom")]
    public void CoverageMetricResult_WellKnownMetricWithCustomName_ThrowsArgumentException(string customMetricName)
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => new CoverageMetricResult(CoverageMetric.Line, 1, 1, "producer", customMetricName));

        Assert.AreEqual("customMetricName", exception.ParamName);
    }

    [TestMethod]
    public void CoverageMetricResult_ValidValues_PopulatesPropertiesAndPercentage()
    {
        var result = new CoverageMetricResult(CoverageMetric.Custom, 3, 4, "producer", "MC/DC");

        Assert.AreEqual(CoverageMetric.Custom, result.Metric);
        Assert.AreEqual("MC/DC", result.CustomMetricName);
        Assert.AreEqual("producer", result.ProducerId);
        Assert.AreEqual(3, result.CoveredCount);
        Assert.AreEqual(4, result.CoverableCount);
        Assert.IsTrue(result.HasCoverableData);
        Assert.AreEqual(75d, result.Percentage);
    }

    [TestMethod]
    public void CoverageMetricResult_NoCoverableData_HasZeroPercentage()
    {
        var result = new CoverageMetricResult(CoverageMetric.Line, 0, 0, "producer");

        Assert.IsFalse(result.HasCoverableData);
        Assert.AreEqual(0d, result.Percentage);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void CoverageReportReference_MissingPath_ThrowsArgumentException(string? path)
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => new CoverageReportReference(SessionUid, path!, CoverageReportFormat.Cobertura, "producer"));

        Assert.AreEqual("path", exception.ParamName);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void CoverageReportReference_MissingProducerId_ThrowsArgumentException(string? producerId)
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => new CoverageReportReference(SessionUid, "coverage.xml", CoverageReportFormat.Cobertura, producerId!));

        Assert.AreEqual("producerId", exception.ParamName);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public void CoverageReportReference_CustomFormatWithoutName_ThrowsArgumentException(string? customFormatName)
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => new CoverageReportReference(SessionUid, "coverage.data", CoverageReportFormat.Custom, "producer", customFormatName));

        Assert.AreEqual("customFormatName", exception.ParamName);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("custom")]
    public void CoverageReportReference_WellKnownFormatWithCustomName_ThrowsArgumentException(string customFormatName)
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => new CoverageReportReference(SessionUid, "coverage.xml", CoverageReportFormat.Cobertura, "producer", customFormatName));

        Assert.AreEqual("customFormatName", exception.ParamName);
    }

    [TestMethod]
    public void CoverageReportReference_ValidValues_PopulatesProperties()
    {
        var report = new CoverageReportReference(SessionUid, "coverage.data", CoverageReportFormat.Custom, "producer", "binary");

        Assert.AreEqual(SessionUid, report.SessionUid);
        Assert.AreEqual("coverage.data", report.Path);
        Assert.AreEqual(CoverageReportFormat.Custom, report.Format);
        Assert.AreEqual("producer", report.ProducerId);
        Assert.AreEqual("binary", report.CustomFormatName);
    }

    [TestMethod]
    public void TestCoverageMessage_ToString_ReturnsWellFormedRepresentation()
    {
        TestCoverageMessage message = new(
            SessionUid,
            CoverageScope.Overall,
            CoverageMetric.Line,
            coveredCount: 8,
            coverableCount: 10,
            producerId: "producer");

        Assert.AreEqual(
            "DataWithSessionUid { DisplayName = Test coverage, Description = Reports a code coverage measurement for a scope., Properties = [] }",
            message.ToString());
    }

    [TestMethod]
    public void TestCoverageThresholdMessage_ToString_ReturnsWellFormedRepresentation()
    {
        TestCoverageThresholdMessage message = new(
            SessionUid,
            CoverageScope.Overall,
            CoverageMetric.Line,
            CoverageAggregation.None,
            actualPercentage: 80,
            requiredPercentage: 75,
            hasCoverableData: true,
            producerId: "producer");

        Assert.AreEqual(
            "DataWithSessionUid { DisplayName = Test coverage threshold, Description = Reports the result of a coverage threshold evaluation., Properties = [] }",
            message.ToString());
    }

    [TestMethod]
    public void TestCoverageReportMessage_ToString_ReturnsWellFormedRepresentation()
    {
        TestCoverageReportMessage message = new(
            SessionUid,
            "coverage.xml",
            CoverageReportFormat.Cobertura,
            "producer");

        Assert.AreEqual(
            "DataWithSessionUid { DisplayName = Test coverage report, Description = References a coverage report artifact., Properties = [] }",
            message.ToString());
    }

    private static TestCoverageThresholdMessage CreateThreshold(
        double actualPercentage,
        double requiredPercentage,
        CoverageAggregation aggregation = CoverageAggregation.None,
        CoverageScopeLevel? aggregatedOver = null)
        => new(
            SessionUid,
            CoverageScope.Overall,
            CoverageMetric.Line,
            aggregation,
            actualPercentage,
            requiredPercentage,
            hasCoverableData: true,
            producerId: "producer",
            aggregatedOver: aggregatedOver);
}
