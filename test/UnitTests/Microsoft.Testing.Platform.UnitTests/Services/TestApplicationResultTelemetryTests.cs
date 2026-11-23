// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.UnitTests.Helpers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TestApplicationResultTelemetryTests
{
    [TestMethod]
    public async Task ConsumeAsync_FailedTest_NotifiesTheTelemetryHandlerOnce()
    {
        (TestApplicationResult result, Mock<ICounter<int>> resultCounter, _) = Create();
        using (result)
        {
            await ConsumeAsync(result, new FailedTestNodeStateProperty("failed"));
        }

        Assert.ContainsSingle(resultCounter.Invocations.Where(i => i.Method.Name == "Add"));
    }

    [TestMethod]
    public async Task ConsumeAsync_SkippedTest_NotifiesTheTelemetryHandlerOnce()
    {
        (TestApplicationResult result, Mock<ICounter<int>> resultCounter, _) = Create();
        using (result)
        {
            await ConsumeAsync(result, new SkippedTestNodeStateProperty("skipped"));
        }

        Assert.ContainsSingle(resultCounter.Invocations.Where(i => i.Method.Name == "Add"));
    }

    [TestMethod]
    public async Task ReportRunTelemetry_ReportsTheSkippedAndFailedCountsOnTheRunActivity()
    {
        (TestApplicationResult result, _, _) = Create();
        Mock<IPlatformActivity> runActivity = new();
        runActivity.Setup(a => a.SetTag(It.IsAny<string>(), It.IsAny<object?>())).Returns(runActivity.Object);
        using (result)
        {
            await ConsumeAsync(result, new SkippedTestNodeStateProperty("skipped"));
            await ConsumeAsync(result, new SkippedTestNodeStateProperty("skipped"));
            await ConsumeAsync(result, new FailedTestNodeStateProperty("failed"));

            result.ReportRunTelemetry(runActivity.Object, (int)ExitCode.AtLeastOneTestFailed);
        }

        runActivity.Verify(a => a.SetTag("test.run.skipped", 2), Times.Once);
        runActivity.Verify(a => a.SetTag("test.run.failed", 1), Times.Once);
        runActivity.Verify(a => a.SetTag("test.run.total", 1), Times.Once);
    }

    private static (TestApplicationResult Result, Mock<ICounter<int>> ResultCounter, Mock<IPlatformOpenTelemetryService> Service) Create()
    {
        Mock<ICounter<int>> resultCounter = new();
        Mock<IPlatformOpenTelemetryService> service = new();
        service.Setup(s => s.CreateCounter<int>(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>())).Returns(resultCounter.Object);
        service.Setup(s => s.CreateUpDownCounter<int>(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>())).Returns(new Mock<IUpDownCounter<int>>().Object);
        service.Setup(s => s.CreateHistogram<double>(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>())).Returns(new Mock<IHistogram<double>>().Object);
        var result = new TestApplicationResult(
            Mock.Of<IOutputDevice>(),
            new TestCommandLineOptions([]),
            Mock.Of<IEnvironment>(),
            Mock.Of<IStopPoliciesService>(),
            service.Object);
        return (result, resultCounter, service);
    }

    private static Task ConsumeAsync(TestApplicationResult result, IProperty state)
        => result.ConsumeAsync(
            new TelemetryProducer(),
            new TestNodeUpdateMessage(default, new TestNode { Uid = "uid", DisplayName = "test", Properties = new PropertyBag(state) }),
            CancellationToken.None);

    private sealed class TelemetryProducer : IDataProducer
    {
        public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];

        public string Uid => nameof(TelemetryProducer);

        public string Version => "1.0.0";

        public string DisplayName => nameof(TelemetryProducer);

        public string Description => nameof(TelemetryProducer);

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }
}
