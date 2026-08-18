// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class AbortAtDeadlineExtensionTests : IDisposable
{
    // Far in the past, so the extension clamps its timer to a zero due time and it fires as soon as it is armed.
    private const string ElapsedDeadline = "2020-01-01T00:00:00Z";

    // Generous upper bound for an already-elapsed timer to fire. Only used to fail a hung test, never to pass one.
    private static readonly TimeSpan FireTimeout = TimeSpan.FromSeconds(30);

    // How long an elapsed deadline is given to (wrongly) fire before we conclude the completion gate suppressed it.
    // WhenDeadlineElapsesBeforeExecutionCompletes_RequestsGracefulStop is the control showing that an unsuppressed
    // timer fires far inside this budget.
    private static readonly TimeSpan SuppressionObservationWindow = TimeSpan.FromSeconds(2);

    private readonly CancellationTokenSource _cts = new();

    public TestContext TestContext { get; set; } = null!;

    public void Dispose() => _cts.Dispose();

    [TestMethod]
    public async Task WhenDeadlineElapsesBeforeExecutionCompletes_RequestsGracefulStop()
    {
        TaskCompletionSource<bool> stopRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
        StopPoliciesService policies = CreatePoliciesService();

        using (CreateExtension(policies, stopRequested))
        {
            Task winner = await Task.WhenAny(stopRequested.Task, Task.Delay(FireTimeout, TestContext.CancellationToken));
            Assert.AreSame(stopRequested.Task, winner, "The elapsed deadline should have requested a graceful stop.");
        }

        Assert.IsTrue(policies.IsDeadlineTriggered, "A deadline that fires during execution marks the run as truncated.");
    }

    [TestMethod]
    public async Task WhenExecutionAlreadyCompleted_ElapsedDeadlineIsIgnored()
    {
        TaskCompletionSource<bool> stopRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
        StopPoliciesService policies = CreatePoliciesService();

        // The host sets this the moment the test framework invoker returns, before the session-end notification
        // drains the message bus and runs the reporters. Everything after it belongs to a run that already finished.
        policies.NotifyTestExecutionCompleted();

        using (CreateExtension(policies, stopRequested))
        {
            Task winner = await Task.WhenAny(stopRequested.Task, Task.Delay(SuppressionObservationWindow, TestContext.CancellationToken));
            Assert.AreNotSame(stopRequested.Task, winner, "A deadline elapsing after execution completed must not stop a finished run.");
        }

        Assert.IsFalse(policies.IsDeadlineTriggered, "A run that finished before the deadline must not be reported as deadline-truncated.");
    }

    [TestMethod]
    public async Task WhenGracefulStopIsRejected_RunIsNotReportedAsDeadlineTruncated()
    {
        TaskCompletionSource<bool> stopAttempted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        StopPoliciesService policies = CreatePoliciesService();

        AbortAtDeadlineExtension extension = CreateExtension(policies, stopAttempted, rejectStop: true);
        try
        {
            Task winner = await Task.WhenAny(stopAttempted.Task, Task.Delay(FireTimeout, TestContext.CancellationToken));
            Assert.AreSame(stopAttempted.Task, winner, "The elapsed deadline should have attempted a graceful stop.");
        }
        finally
        {
            // Disposal drains the in-flight deadline handler, so its rollback has run once this returns.
            // Only the netstandard2.0 build drains from Dispose; on .NET the drain lives in DisposeAsync.
#if NETCOREAPP
            await extension.DisposeAsync();
#else
            extension.Dispose();
#endif
        }

        Assert.IsFalse(
            policies.IsDeadlineTriggered,
            "A graceful stop that was rejected truncated nothing, so the run must not exit with TestExecutionStoppedAtDeadline.");
    }

    private StopPoliciesService CreatePoliciesService()
    {
        Mock<ITestApplicationCancellationTokenSource> cancellationTokenSource = new();
        cancellationTokenSource.SetupGet(x => x.CancellationToken).Returns(_cts.Token);
        return new StopPoliciesService(cancellationTokenSource.Object);
    }

    private AbortAtDeadlineExtension CreateExtension(IStopPoliciesService policies, TaskCompletionSource<bool> stopRequested, bool rejectStop = false)
    {
        Mock<IEnvironment> environment = new();
        environment.Setup(x => x.GetEnvironmentVariable(It.IsAny<string>())).Returns((string?)null);
        environment.Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE)).Returns(ElapsedDeadline);

        Mock<IClock> clock = new();
        clock.SetupGet(x => x.UtcNow).Returns(DateTimeOffset.UtcNow);

        Mock<IGracefulStopTestExecutionCapability> capability = new();
        capability.Setup(x => x.StopTestExecutionAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                stopRequested.TrySetResult(true);

                // StopTestExecutionAsync is a [TPEXP] extensibility point, so a third-party framework can
                // reject the request by throwing. Signal the attempt first so the test can observe it.
                return rejectStop
                    ? throw new InvalidOperationException("Graceful stop is not available.")
                    : Task.CompletedTask;
            });

        Mock<ITestApplicationCancellationTokenSource> cancellationTokenSource = new();
        cancellationTokenSource.SetupGet(x => x.CancellationToken).Returns(_cts.Token);

        Mock<IOutputDevice> outputDevice = new();
        outputDevice
            .Setup(x => x.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Mock<ILogger> logger = new();
        logger
            .Setup(x => x.LogAsync(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<Func<string, Exception?, string>>()))
            .Returns(Task.CompletedTask);

        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

        return new AbortAtDeadlineExtension(
            environment.Object,
            clock.Object,
            capability.Object,
            policies,
            cancellationTokenSource.Object,
            outputDevice.Object,
            loggerFactory.Object);
    }
}
