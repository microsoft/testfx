// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.Hosts;

[TestClass]
public sealed class ConsoleTestHostTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task RunAsync_ConsoleVerdictAndTelemetryPreserveRequestOutcomes(bool telemetry, bool cancelRequest)
    {
        using CancellationTokenSource cancellation = new();
        ConsoleFixture fixture = new(telemetry, cancellation.Token, cancelRequest);

        int exitCode = await fixture.Host.RunAsync();

        Assert.AreEqual(cancelRequest ? 3 : 17, exitCode);
        Assert.IsNotNull(fixture.BuilderData);
        Assert.AreSame(fixture.Services, fixture.BuilderData.ServiceProvider);
        Assert.IsFalse(fixture.BuilderData.IsForDiscoveryRequest);
        Assert.AreSame(fixture.Invoker.Object, fixture.BuilderData.TestExecutionRequestInvoker);
        Assert.AreEqual("testingplatform-console", fixture.Client!.Id);
        Assert.AreEqual(PlatformTestMetadata.Version, fixture.Client.Version);
        Assert.AreEqual(cancellation.Token, fixture.RequestToken);
        fixture.Output.Verify(o => o.DisplayBeforeSessionStartAsync(cancellation.Token), Times.Once);
        fixture.Output.Verify(o => o.DisplayAfterSessionEndRunAsync(CancellationToken.None), cancelRequest ? Times.Never() : Times.Once());
        fixture.Policies.Verify(p => p.NotifyTestExecutionCompleted(), Times.Once);
        Assert.IsTrue(fixture.Disposed);
        if (!telemetry)
        {
            Assert.IsEmpty(fixture.Events);
            return;
        }

        (string eventName, IDictionary<string, object> values) = Assert.ContainsSingle(fixture.Events);
        Assert.AreEqual("dotnet/testingplatform/host/consoletesthostexit", eventName);
        Assert.AreEqual(cancelRequest ? "3" : "17", values["exit code"]);
        Assert.AreEqual(new DateTimeOffset(2025, 2, 3, 4, 5, 0, TimeSpan.Zero), values["run start"]);
        Assert.AreEqual(new DateTimeOffset(2025, 2, 3, 4, 5, 1, TimeSpan.Zero), values["adapter load start"]);
        Assert.AreEqual(new DateTimeOffset(2025, 2, 3, 4, 5, 2, TimeSpan.Zero), values["adapter load stop"]);
        Assert.AreEqual(new DateTimeOffset(2025, 2, 3, 4, 5, 3, TimeSpan.Zero), values["request execute start"]);
        Assert.AreEqual(new DateTimeOffset(2025, 2, 3, 4, 5, 4, TimeSpan.Zero), values["request execute stop"]);
        Assert.AreEqual(new DateTimeOffset(2025, 2, 3, 4, 5, 5, TimeSpan.Zero), values["run stop"]);
        if (cancelRequest)
        {
            Assert.HasCount(7, values);
            Assert.IsFalse(values.ContainsKey("total failed"));
            Assert.IsFalse(values.ContainsKey("total ran"));
            Assert.IsFalse(values.ContainsKey("extensions"));
        }
        else
        {
            Assert.HasCount(10, values);
            Assert.AreEqual(2, values["total failed"]);
            Assert.AreEqual(19, values["total ran"]);
            Assert.Contains("\"Version\":\"7.8.9\"", ((string)values["extensions"]).Replace(" ", string.Empty));
        }
    }

    [TestMethod]
    public async Task RunAsync_ConsoleUnexpectedFailurePreservesExceptionAndStillDisposesServices()
    {
        using CancellationTokenSource application = new();
        using CancellationTokenSource unrelated = new();
        OperationCanceledException expected = new("unrelated cancellation", unrelated.Token);
        ConsoleFixture fixture = new(true, application.Token, failure: expected);

        OperationCanceledException failure = await Assert.ThrowsExactlyAsync<OperationCanceledException>(fixture.Host.RunAsync);

        Assert.AreSame(expected, failure);
        Assert.AreEqual(unrelated.Token, failure.CancellationToken);
        Assert.IsTrue(fixture.Disposed);
        Assert.IsEmpty(fixture.Events);
        fixture.Policies.Verify(p => p.NotifyTestExecutionCompleted(), Times.Once);
    }

    private sealed class ConsoleFixture
    {
        public ConsoleFixture(bool telemetry, CancellationToken token, bool cancelRequest = false, Exception? failure = null)
        {
            Services.AddService(Mock.Of<ITestApplicationCancellationTokenSource>(source => source.CancellationToken == token));
            Services.AddService(new NopLoggerFactory());
            Mock<IClock> clock = new();
            clock.SetupSequence(c => c.UtcNow)
                .Returns(new DateTimeOffset(2025, 2, 3, 4, 5, 0, TimeSpan.Zero))
                .Returns(new DateTimeOffset(2025, 2, 3, 4, 5, 1, TimeSpan.Zero))
                .Returns(new DateTimeOffset(2025, 2, 3, 4, 5, 2, TimeSpan.Zero))
                .Returns(new DateTimeOffset(2025, 2, 3, 4, 5, 3, TimeSpan.Zero))
                .Returns(new DateTimeOffset(2025, 2, 3, 4, 5, 4, TimeSpan.Zero))
                .Returns(new DateTimeOffset(2025, 2, 3, 4, 5, 5, TimeSpan.Zero));
            Services.AddService(clock.Object);
            Services.AddService(new Mock<ICommandLineOptions>().Object);
            Services.AddService(new Mock<IEnvironment>().Object);
            Services.AddService(new TestCoverageResult());
            Services.AddService(Policies.Object);
            Output.SetupGet(o => o.Uid).Returns("output");
            Output.SetupGet(o => o.Version).Returns("7.8.9");
            Output.Setup(o => o.IsEnabledAsync()).ReturnsAsync(true);
            Output.Setup(o => o.DisplayBeforeSessionStartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Output.Setup(o => o.DisplayAfterSessionEndRunAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Services.AddService(new ProxyOutputDevice(Output.Object, null));
            Services.AddService(Output.Object);
            Mock<BaseMessageBus> bus = new();
            bus.Setup(b => b.DrainDataAsync()).Returns(Task.CompletedTask);
            bus.Setup(b => b.DisableAsync()).Returns(Task.CompletedTask);
            bus.SetupGet(b => b.DataConsumerServices).Returns([]);
            bus.SetupGet(b => b.ConsumersStillRunning).Returns([]);
            Services.AddService(bus.Object);
            Invoker.SetupGet(i => i.Uid).Returns("invoker");
            Invoker.SetupGet(i => i.Version).Returns("7.8.9");
            Invoker.Setup(i => i.IsEnabledAsync()).ReturnsAsync(true);
            Invoker.Setup(i => i.ExecuteAsync(It.IsAny<ITestFramework>(), It.IsAny<ClientInfo>(), It.IsAny<CancellationToken>()))
                .Returns<ITestFramework, ClientInfo, CancellationToken>((_, client, requestToken) =>
                {
                    Client = client;
                    RequestToken = requestToken;
                    Exception? error = failure ?? (cancelRequest ? new OperationCanceledException(requestToken) : null);
                    return error is null ? Task.CompletedTask : Task.FromException(error);
                });
            Services.AddService(Invoker.Object);
            Mock<ITestApplicationProcessExitCode> verdict = new();
            verdict.SetupGet(v => v.Uid).Returns("verdict");
            verdict.SetupGet(v => v.Version).Returns("7.8.9");
            verdict.Setup(v => v.IsEnabledAsync()).ReturnsAsync(true);
            verdict.Setup(v => v.GetProcessExitCode()).Returns(17);
            verdict.Setup(v => v.GetStatistics()).Returns(new Statistics { TotalFailedTests = 2, TotalRanTests = 19 });
            Services.AddService(verdict.Object);
            Mock<ITelemetryInformation> information = new();
            information.SetupGet(i => i.IsEnabled).Returns(telemetry);
            Services.AddService(information.Object);
            Mock<ITelemetryCollector> collector = new();
            collector.Setup(c => c.LogEventAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), token))
                .Callback<string, IDictionary<string, object>, CancellationToken>((name, values, _) =>
                {
                    Assert.IsTrue(Disposed, "Owned services must be finalized before the console exit event.");
                    Events.Add((name, values));
                })
                .Returns(Task.CompletedTask);
            Services.AddService(collector.Object);
            Mock<IDisposable> owned = new();
            owned.Setup(d => d.Dispose()).Callback(() => Disposed = true);
            Services.AddService(owned.Object);
            Host = new(
                Services,
                data =>
                {
                    BuilderData = data;
                    return Task.FromResult(new Mock<ITestFramework>().Object);
                },
                new TestFrameworkManager(
                    (_, _) => new Mock<ITestFramework>().Object,
                    _ => new Microsoft.Testing.Platform.Capabilities.TestFramework.TestFrameworkCapabilities()),
                new TestHostManager());
        }

        public ServiceProvider Services { get; } = new();

        public Mock<IPlatformOutputDevice> Output { get; } = new();

        public Mock<ITestFrameworkInvoker> Invoker { get; } = new();

        public Mock<IStopPoliciesService> Policies { get; } = new();

        public ConsoleTestHost Host { get; }

        public TestFrameworkBuilderData? BuilderData { get; private set; }

        public ClientInfo? Client { get; private set; }

        public CancellationToken RequestToken { get; private set; }

        public bool Disposed { get; private set; }

        public List<(string Name, IDictionary<string, object> Values)> Events { get; } = [];
    }

    private sealed class NopLoggerFactory : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName) => new NopLogger();
    }
}
