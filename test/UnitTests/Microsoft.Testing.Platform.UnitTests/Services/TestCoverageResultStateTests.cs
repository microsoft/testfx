// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TestCoverageResultStateTests
{
    private static readonly SessionUid Session = new("session");
    private static readonly IDataProducer Producer = Mock.Of<IDataProducer>();

    [TestMethod]
    public async Task Reset_ClearsReports_SoTheSameReportCanBeAddedAgain()
    {
        TestCoverageResult result = new();
        TestCoverageReportMessage report = new(Session, "coverage.xml", CoverageReportFormat.Cobertura, "producer");
        await result.ConsumeAsync(Producer, report, CancellationToken.None);
        Assert.HasCount(1, result.Reports);

        result.Reset();
        Assert.IsEmpty(result.Reports);

        await result.ConsumeAsync(Producer, report, CancellationToken.None);

        CoverageReportReference reference = Assert.ContainsSingle(result.Reports);
        Assert.AreEqual("coverage.xml", reference.Path);
    }

    [TestMethod]
    public async Task Thresholds_CustomMetricsWithDifferentNames_AreKeptSeparatelyAndSameNameIsReplaced()
    {
        TestCoverageResult result = new();

        await result.ConsumeAsync(Producer, new TestCoverageThresholdMessage(Session, CoverageScope.Overall, CoverageMetric.Custom, CoverageAggregation.None, 90, 80, true, "producer", customMetricName: "a"), CancellationToken.None);
        await result.ConsumeAsync(Producer, new TestCoverageThresholdMessage(Session, CoverageScope.Overall, CoverageMetric.Custom, CoverageAggregation.None, 70, 80, true, "producer", customMetricName: "b"), CancellationToken.None);
        await result.ConsumeAsync(Producer, new TestCoverageThresholdMessage(Session, CoverageScope.Overall, CoverageMetric.Custom, CoverageAggregation.None, 50, 80, true, "producer", customMetricName: "a"), CancellationToken.None);

        Assert.HasCount(2, result.Thresholds);
        Assert.AreEqual("a", result.Thresholds[0].CustomMetricName);
        Assert.AreEqual(50d, result.Thresholds[0].ActualPercentage);
        Assert.AreEqual("b", result.Thresholds[1].CustomMetricName);
        Assert.AreEqual(70d, result.Thresholds[1].ActualPercentage);
    }

    [TestMethod]
    public async Task Thresholds_NonCustomMetricsOfSameKey_AreReplaced()
    {
        TestCoverageResult result = new();

        await result.ConsumeAsync(Producer, new TestCoverageThresholdMessage(Session, CoverageScope.Overall, CoverageMetric.Line, CoverageAggregation.None, 90, 80, true, "producer"), CancellationToken.None);
        await result.ConsumeAsync(Producer, new TestCoverageThresholdMessage(Session, CoverageScope.Overall, CoverageMetric.Line, CoverageAggregation.None, 10, 80, true, "producer"), CancellationToken.None);

        TestCoverageThresholdMessage threshold = Assert.ContainsSingle(result.Thresholds);
        Assert.AreEqual(10d, threshold.ActualPercentage);
    }

    [TestMethod]
    public async Task ConsumeAsync_DuplicateMessages_CreateTheLoggerOnlyOnce()
    {
        Mock<ILoggerFactory> factory = new();
        factory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        TestCoverageResult result = new(factory.Object);
        TestCoverageMessage message = new(Session, CoverageScope.Overall, CoverageMetric.Line, 1, 2, "producer");

        await result.ConsumeAsync(Producer, message, CancellationToken.None);
        await result.ConsumeAsync(Producer, message, CancellationToken.None);
        await result.ConsumeAsync(Producer, message, CancellationToken.None);

        factory.Verify(f => f.CreateLogger(It.IsAny<string>()), Times.Once);
    }
}
