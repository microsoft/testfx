// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TestCoverageResultTests
{
    private static readonly IDataProducer DummyProducer = new MockDataProducer();

    [TestMethod]
    public async Task ConsumeAsync_DuplicateMeasurement_ReplacesValueAndPreservesFirstSeenOrder()
    {
        TestCoverageResult result = new();
        SessionUid session = new("session");

        await ConsumeAsync(result, new TestCoverageMessage(session, CoverageScope.Overall, CoverageMetric.Line, 4, 10, "producer"));
        await ConsumeAsync(result, new TestCoverageMessage(session, CoverageScope.Overall, CoverageMetric.Branch, 2, 8, "producer"));
        await ConsumeAsync(result, new TestCoverageMessage(session, CoverageScope.Overall, CoverageMetric.Line, 9, 10, "producer"));

        CoverageScopeSummary overall = Assert.ContainsSingle(result.Scopes);
        Assert.HasCount(2, overall.Metrics);
        Assert.AreEqual(CoverageMetric.Line, overall.Metrics[0].Metric);
        Assert.AreEqual(9, overall.Metrics[0].CoveredCount);
        Assert.AreEqual(10, overall.Metrics[0].CoverableCount);
        Assert.AreEqual(CoverageMetric.Branch, overall.Metrics[1].Metric);
    }

    [TestMethod]
    public async Task Scopes_MultipleSessionsAndScopes_GroupsBySessionAndScopeInFirstSeenOrder()
    {
        TestCoverageResult result = new();
        SessionUid firstSession = new("first-session");
        SessionUid secondSession = new("second-session");
        CoverageScope module = new(CoverageScopeLevel.Module, "app.dll");

        await ConsumeAsync(result, new TestCoverageMessage(firstSession, CoverageScope.Overall, CoverageMetric.Line, 8, 10, "producer"));
        await ConsumeAsync(result, new TestCoverageMessage(secondSession, CoverageScope.Overall, CoverageMetric.Branch, 3, 5, "producer"));
        await ConsumeAsync(result, new TestCoverageMessage(firstSession, module, CoverageMetric.Method, 6, 7, "producer"));
        await ConsumeAsync(result, new TestCoverageMessage(firstSession, CoverageScope.Overall, CoverageMetric.Statement, 9, 10, "producer"));

        Assert.HasCount(3, result.Scopes);
        Assert.AreEqual(firstSession, result.Scopes[0].SessionUid);
        Assert.AreEqual(CoverageScopeLevel.Overall, result.Scopes[0].Scope.Level);
        Assert.HasCount(2, result.Scopes[0].Metrics);
        Assert.AreEqual(CoverageMetric.Line, result.Scopes[0].Metrics[0].Metric);
        Assert.AreEqual(CoverageMetric.Statement, result.Scopes[0].Metrics[1].Metric);
        Assert.AreEqual(secondSession, result.Scopes[1].SessionUid);
        Assert.AreEqual(CoverageScopeLevel.Overall, result.Scopes[1].Scope.Level);
        Assert.AreEqual(CoverageMetric.Branch, Assert.ContainsSingle(result.Scopes[1].Metrics).Metric);
        Assert.AreEqual(module, result.Scopes[2].Scope);
        Assert.IsNotNull(result.Overall);
        Assert.AreEqual(8, result.Overall[CoverageMetric.Line]!.CoveredCount);
        Assert.AreEqual(10, result.Overall[CoverageMetric.Line]!.CoverableCount);
    }

    [TestMethod]
    public async Task Scopes_SeparateProducersAndCustomMetrics_PreservesEachMeasurement()
    {
        TestCoverageResult result = new();
        SessionUid session = new("session");

        await ConsumeAsync(result, new TestCoverageMessage(session, CoverageScope.Overall, CoverageMetric.Line, 8, 10, "producer-a"));
        await ConsumeAsync(result, new TestCoverageMessage(session, CoverageScope.Overall, CoverageMetric.Line, 7, 10, "producer-b"));
        await ConsumeAsync(result, new TestCoverageMessage(session, CoverageScope.Overall, CoverageMetric.Custom, 3, 4, "producer-a", "mcdc"));
        await ConsumeAsync(result, new TestCoverageMessage(session, CoverageScope.Overall, CoverageMetric.Custom, 2, 4, "producer-a", "safety"));

        CoverageScopeSummary overall = Assert.ContainsSingle(result.Scopes);
        Assert.HasCount(4, overall.Metrics);
        Assert.AreEqual("producer-a", overall.Metrics[0].ProducerId);
        Assert.AreEqual("producer-b", overall.Metrics[1].ProducerId);
        Assert.AreEqual("mcdc", overall.Metrics[2].CustomMetricName);
        Assert.AreEqual(3, overall.Metrics[2].CoveredCount);
        Assert.AreEqual("safety", overall.Metrics[3].CustomMetricName);
        Assert.AreSame(overall.Metrics[2], overall.GetCustom("mcdc"));
    }

    [TestMethod]
    public async Task ConsumeAsync_DuplicateReport_ReplacesValueAndPreservesFirstSeenOrder()
    {
        TestCoverageResult result = new();
        SessionUid session = new("session");

        await ConsumeAsync(result, new TestCoverageReportMessage(session, "first.xml", CoverageReportFormat.Cobertura, "producer"));
        await ConsumeAsync(result, new TestCoverageReportMessage(session, "second.info", CoverageReportFormat.Lcov, "producer"));
        await ConsumeAsync(result, new TestCoverageReportMessage(session, "first.xml", CoverageReportFormat.OpenCover, "producer"));
        await ConsumeAsync(result, new TestCoverageReportMessage(session, "first.xml", CoverageReportFormat.Cobertura, "other-producer"));
        await ConsumeAsync(result, new TestCoverageReportMessage(new SessionUid("other-session"), "first.xml", CoverageReportFormat.Lcov, "producer"));

        Assert.HasCount(4, result.Reports);
        Assert.AreEqual("first.xml", result.Reports[0].Path);
        Assert.AreEqual(CoverageReportFormat.OpenCover, result.Reports[0].Format);
        Assert.AreEqual(session, result.Reports[0].SessionUid);
        Assert.AreEqual("second.info", result.Reports[1].Path);
        Assert.AreEqual(CoverageReportFormat.Lcov, result.Reports[1].Format);
        Assert.AreEqual("other-producer", result.Reports[2].ProducerId);
        Assert.AreEqual(CoverageReportFormat.Cobertura, result.Reports[2].Format);
        Assert.AreEqual("producer", result.Reports[3].ProducerId);
        Assert.AreEqual(new SessionUid("other-session"), result.Reports[3].SessionUid);
        Assert.AreEqual(CoverageReportFormat.Lcov, result.Reports[3].Format);
    }

    [TestMethod]
    public async Task ConsumeAsync_DuplicateThreshold_ReplacesValueAndPreservesFirstSeenOrder()
    {
        TestCoverageResult result = new();
        SessionUid session = new("session");

        await ConsumeAsync(result, CreateThreshold(session, CoverageMetric.Line, actualPercentage: 70));
        await ConsumeAsync(result, CreateThreshold(session, CoverageMetric.Branch, actualPercentage: 60));
        await ConsumeAsync(result, CreateThreshold(session, CoverageMetric.Line, actualPercentage: 90));

        Assert.HasCount(2, result.Thresholds);
        Assert.AreEqual(CoverageMetric.Line, result.Thresholds[0].Metric);
        Assert.AreEqual(90, result.Thresholds[0].ActualPercentage);
        Assert.IsTrue(result.Thresholds[0].Passed);
        Assert.AreEqual(CoverageMetric.Branch, result.Thresholds[1].Metric);
        Assert.IsFalse(result.Thresholds[1].Passed);
    }

    [TestMethod]
    public async Task ConsumeAsync_DuplicateKeys_LogsLastWriteWinsCorrections()
    {
        RecordingLoggerFactory loggerFactory = new();
        TestCoverageResult result = new(loggerFactory);
        SessionUid session = new("session");

        TestCoverageMessage measurement = new(session, CoverageScope.Overall, CoverageMetric.Line, 8, 10, "producer");
        TestCoverageThresholdMessage threshold = CreateThreshold(session, CoverageMetric.Line, actualPercentage: 80);
        TestCoverageReportMessage report = new(session, "coverage.xml", CoverageReportFormat.Cobertura, "producer");

        await ConsumeAsync(result, measurement);
        await ConsumeAsync(result, measurement);
        await ConsumeAsync(result, threshold);
        await ConsumeAsync(result, threshold);
        await ConsumeAsync(result, report);
        await ConsumeAsync(result, report);

        Assert.HasCount(3, loggerFactory.Messages);
        foreach (string message in loggerFactory.Messages)
        {
            Assert.Contains("last-write-wins", message);
        }

        _ = Assert.ContainsSingle(loggerFactory.Messages.Where(message => message.Contains(nameof(TestCoverageMessage), StringComparison.Ordinal)));
        _ = Assert.ContainsSingle(loggerFactory.Messages.Where(message => message.Contains(nameof(TestCoverageThresholdMessage), StringComparison.Ordinal)));
        _ = Assert.ContainsSingle(loggerFactory.Messages.Where(message => message.Contains(nameof(TestCoverageReportMessage), StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task Reset_AccumulatedData_ClearsAllState()
    {
        TestCoverageResult result = new();
        SessionUid session = new("session");

        await ConsumeAsync(result, new TestCoverageMessage(session, CoverageScope.Overall, CoverageMetric.Line, 8, 10, "producer"));
        await ConsumeAsync(result, CreateNoDataThreshold(session, treatNoDataAsFailure: true));
        await ConsumeAsync(result, new TestCoverageReportMessage(session, "coverage.xml", CoverageReportFormat.Cobertura, "producer"));

        result.Reset();

        Assert.IsEmpty(result.Scopes);
        Assert.IsNull(result.Overall);
        Assert.IsEmpty(result.Thresholds);
        Assert.IsFalse(result.HasThresholdFailure);
        Assert.IsEmpty(result.Reports);
    }

    [TestMethod]
    public async Task HasThresholdFailure_NoCoverableData_UsesThresholdNoDataPolicy()
    {
        TestCoverageResult result = new();
        SessionUid session = new("session");

        await ConsumeAsync(result, CreateNoDataThreshold(session, treatNoDataAsFailure: false));

        Assert.IsFalse(result.HasThresholdFailure);
        Assert.IsTrue(result.Thresholds[0].Passed);

        await ConsumeAsync(result, CreateNoDataThreshold(session, treatNoDataAsFailure: true));

        Assert.IsTrue(result.HasThresholdFailure);
        Assert.HasCount(1, result.Thresholds);
        Assert.IsFalse(result.Thresholds[0].Passed);
    }

    private static Task ConsumeAsync(TestCoverageResult result, IData data)
        => result.ConsumeAsync(DummyProducer, data, CancellationToken.None);

    private static TestCoverageThresholdMessage CreateNoDataThreshold(SessionUid session, bool treatNoDataAsFailure)
        => new(
            session,
            CoverageScope.Overall,
            CoverageMetric.Line,
            CoverageAggregation.None,
            actualPercentage: 100,
            requiredPercentage: 80,
            hasCoverableData: false,
            producerId: "producer",
            treatNoDataAsFailure: treatNoDataAsFailure);

    private static TestCoverageThresholdMessage CreateThreshold(
        SessionUid session,
        CoverageMetric metric,
        double actualPercentage)
        => new(
            session,
            CoverageScope.Overall,
            metric,
            CoverageAggregation.Total,
            actualPercentage,
            requiredPercentage: 80,
            hasCoverableData: true,
            producerId: "producer",
            aggregatedOver: CoverageScopeLevel.Module);

    private sealed class MockDataProducer : IDataProducer
    {
        public Type[] DataTypesProduced => [];

        public string Uid => nameof(MockDataProducer);

        public string Version => "1.0.0";

        public string DisplayName => nameof(MockDataProducer);

        public string Description => nameof(MockDataProducer);

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }

    private sealed class RecordingLoggerFactory : ILoggerFactory, ILogger
    {
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => this;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));

        public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Log(logLevel, state, exception, formatter);
            return Task.CompletedTask;
        }
    }
}
