// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.Requests;

[TestClass]
public sealed class TestHostTestFrameworkInvokerTests
{
    private const string CleanupOperation = "TestExecutionScopeCleanupException";

    [TestMethod]
    public void AddSecondaryExceptionOrThrow_WithWritableData_AttachesCleanupException()
    {
        var primaryException = new InvalidOperationException("framework failure");
        var cleanupException = new InvalidOperationException("cleanup failure");

        InvokeAddSecondaryExceptionOrThrow(primaryException, cleanupException);

        Assert.AreSame(cleanupException, primaryException.Data[CleanupOperation]);
    }

    [TestMethod]
    public void AddSecondaryExceptionOrThrow_WithUnavailableData_PreservesBothExceptions()
    {
        var primaryException = new ThrowingDataException("framework failure");
        var cleanupException = new InvalidOperationException("cleanup failure");

        AggregateException exception = Assert.ThrowsExactly<AggregateException>(
            () => InvokeAddSecondaryExceptionOrThrow(primaryException, cleanupException));

        Assert.HasCount(2, exception.InnerExceptions);
        Assert.AreSame(primaryException, exception.InnerExceptions[0]);
        Assert.AreSame(cleanupException, exception.InnerExceptions[1]);
    }

    private static void InvokeAddSecondaryExceptionOrThrow(Exception primaryException, Exception cleanupException)
    {
        MethodInfo method = typeof(TestHostTestFrameworkInvoker).GetMethod(
            "AddSecondaryExceptionOrThrow",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var action = (Action<Exception, string, Exception>)method.CreateDelegate(
            typeof(Action<Exception, string, Exception>));
        action(primaryException, CleanupOperation, cleanupException);
    }

    private sealed class ThrowingDataException(string message) : Exception(message)
    {
        public override IDictionary Data => throw new NotSupportedException("Exception data is unavailable.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExecuteAsync_SessionResultsPreserveWarningsFailuresAndCompletion(bool fallbackError)
    {
        using CancellationTokenSource cancellation = new();
        List<string> lifecycle = [];
        List<string> warnings = [];
        List<string> errors = [];
        ServiceProvider services = new();
        services.AddService(new Mock<IEnvironment>().Object);
        Mock<ILogger> logger = new();
        logger.Setup(l => l.LogAsync(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<Func<string, Exception?, string>>()))
            .Returns(Task.CompletedTask);
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(logger.Object);
        services.AddService(loggerFactory.Object);
        Mock<ITestNodesTreeFilterTestFrameworkCapability> treeCapability = new();
        treeCapability.SetupGet(c => c.IsSupported).Returns(true);
        services.AddService(new TestFrameworkCapabilities(treeCapability.Object));
        Mock<ITestSessionContext> session = new();
        session.SetupGet(s => s.SessionUid).Returns(new SessionUid("session-17"));
        services.AddService(session.Object);
        Mock<ITestExecutionRequestFactory> requests = new();
        requests.Setup(r => r.CreateRequestAsync(It.IsAny<Microsoft.Testing.Platform.TestHost.TestSessionContext>(), cancellation.Token))
            .Returns<Microsoft.Testing.Platform.TestHost.TestSessionContext, CancellationToken>((context, _) => Task.FromResult<TestExecutionRequest>(new RunTestExecutionRequest(context)));
        services.AddService(requests.Object);
        Mock<IMessageBus> bus = new();
        List<IData> published = [];
        bus.Setup(b => b.PublishAsync(It.IsAny<IDataProducer>(), It.IsAny<IData>()))
            .Callback<IDataProducer, IData>((_, data) => published.Add(data))
            .Returns(Task.CompletedTask);
        services.AddService(bus.Object);
        Mock<IOutputDevice> output = new();
        output.Setup(o => o.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), cancellation.Token))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>((_, data, _) => warnings.Add(Assert.IsInstanceOfType<Microsoft.Testing.Platform.OutputDevice.WarningMessageOutputDeviceData>(data).Message))
            .Returns(Task.CompletedTask);
        services.AddService(output.Object);
        Mock<ITestApplicationProcessExitCode> verdict = new();
        verdict.Setup(v => v.SetTestAdapterTestSessionFailureAsync(It.IsAny<string>(), cancellation.Token))
            .Callback<string, CancellationToken>((error, _) => errors.Add(error))
            .Returns(Task.CompletedTask);
        services.AddService(verdict.Object);
        Mock<ITestFramework> framework = new();
        framework.Setup(f => f.CreateTestSessionAsync(It.IsAny<CreateTestSessionContext>()))
            .Callback<CreateTestSessionContext>(context =>
            {
                Assert.AreEqual("session-17", context.SessionUid.Value);
                Assert.AreEqual(cancellation.Token, context.CancellationToken);
                lifecycle.Add("create");
            })
            .ReturnsAsync(new CreateTestSessionResult { IsSuccess = false, WarningMessage = "create warning", ErrorMessage = fallbackError ? null : "create rejected" });
        framework.Setup(f => f.ExecuteRequestAsync(It.IsAny<ExecuteRequestContext>()))
            .Callback<ExecuteRequestContext>(context =>
            {
                Assert.AreEqual("session-17", Assert.IsInstanceOfType<RunTestExecutionRequest>(context.Request).Session.SessionUid.Value);
                Assert.AreEqual(cancellation.Token, context.CancellationToken);
                Assert.AreSame(bus.Object, context.MessageBus);
                lifecycle.Add("execute");
                context.Complete();
            })
            .Returns(Task.CompletedTask);
        framework.Setup(f => f.CloseTestSessionAsync(It.IsAny<CloseTestSessionContext>()))
            .Callback<CloseTestSessionContext>(context =>
            {
                Assert.AreEqual("session-17", context.SessionUid.Value);
                Assert.AreEqual(cancellation.Token, context.CancellationToken);
                lifecycle.Add("close");
            })
            .ReturnsAsync(new CloseTestSessionResult { IsSuccess = false, WarningMessage = "close warning", ErrorMessage = "close rejected" });
        TestHostTestFrameworkInvoker invoker = new(services);

        await invoker.ExecuteAsync(framework.Object, new ClientInfo("client", "1.0"), cancellation.Token);

        Assert.AreSequenceEqual(["create", "execute", "close"], lifecycle);
        Assert.AreSequenceEqual(["create warning", "close warning"], warnings);
        Assert.AreSequenceEqual([fallbackError ? "Test adapter test session failure" : "create rejected", "close rejected"], errors);
        Assert.HasCount(1, published);
#pragma warning disable CS0618 // The invoker intentionally publishes this legacy compatibility message.
        Assert.IsInstanceOfType<TestRequestExecutionTimeInfo>(published[0]);
        Assert.AreSequenceEqual(new[] { typeof(TestRequestExecutionTimeInfo) }, invoker.DataTypesProduced);
#pragma warning restore CS0618
        Assert.AreEqual("TestHostTestFrameworkInvoker", invoker.Uid);
        Assert.AreEqual("2.6.0-dev", invoker.Version);
        Assert.AreEqual(string.Empty, invoker.DisplayName);
        Assert.AreEqual(string.Empty, invoker.Description);
        Assert.IsTrue(await invoker.IsEnabledAsync());
        logger.Verify(l => l.LogAsync(LogLevel.Information, "ITestNodesTreeFilterCapability.IsSupported: True", null, LoggingExtensions.Formatter), Times.Once);
    }

    [TestMethod]
    [DataRow(true, false, false, false)]
    [DataRow(false, true, false, false)]
    [DataRow(true, true, false, false)]
    [DataRow(true, true, true, false)]
    [DataRow(true, false, false, true)]
    public async Task CompleteScope_DrainAndDisposeFailuresPreservePrimaryAndSecondaryExceptions(
        bool failDrain, bool failDispose, bool hasPrimary, bool canceled)
    {
        InvalidOperationException drainFailure = new("drain failed");
        IOException disposeFailure = new("stop failed");
        InvalidOperationException primary = new("execution failed");
        ServiceProvider services = new();
        services.AddService(new Mock<IEnvironment>().Object);
        Mock<BaseMessageBus> bus = new();
        bus.Setup(b => b.DrainDataAsync()).Returns(failDrain ? Task.FromException(drainFailure) : Task.CompletedTask);
        services.AddService(bus.Object);
        Mock<IPlatformTestExecutionActivity> activity = new();
        if (failDispose)
        {
            activity.Setup(a => a.Stop(It.IsAny<DateTimeOffset>())).Throws(disposeFailure);
        }

        Mock<IPlatformOpenTelemetryServiceWithTestExecutionActivities> otel = new();
        otel.Setup(o => o.StartTestExecutionActivity(It.IsAny<string>(), It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(), It.IsAny<string?>(), It.IsAny<DateTimeOffset>()))
            .Returns(activity.Object);
        TestExecutionActivityBroker broker = new(otel.Object, PlatformOpenTelemetryOptions.Default);
        TestExecutionActivityReservation reservation = broker.Reserve(new TestNode { Uid = "test-17", DisplayName = "Case 17" })!;
        reservation.Activate();
        ScopeInvoker invoker = new(services);
        Exception? observed = null;
        try
        {
            await invoker.CompleteAsync(broker, hasPrimary ? primary : null, new CancellationToken(canceled));
        }
        catch (Exception exception)
        {
            observed = exception;
        }

        bus.Verify(b => b.DrainDataAsync(), canceled ? Times.Never() : Times.Once());
        activity.Verify(a => a.Stop(It.IsAny<DateTimeOffset>()), Times.Once);
        if (hasPrimary)
        {
            Assert.IsNull(observed);
            observed = (Exception?)primary.Data[CleanupOperation];
        }

        if (failDrain && !canceled && failDispose)
        {
            AggregateException aggregate = Assert.IsInstanceOfType<AggregateException>(observed);
            Assert.AreSequenceEqual<Exception>([drainFailure, disposeFailure], aggregate.InnerExceptions);
        }
        else if (failDispose)
        {
            Assert.AreSame(disposeFailure, observed);
        }
        else if (failDrain && !canceled)
        {
            Assert.AreSame(drainFailure, observed);
        }
        else
        {
            Assert.IsNull(observed);
        }

        broker.Dispose();
        activity.Verify(a => a.Stop(It.IsAny<DateTimeOffset>()), Times.Once);
    }

    private sealed class ScopeInvoker(IServiceProvider services) : TestHostTestFrameworkInvoker(services)
    {
        public Task CompleteAsync(TestExecutionActivityBroker broker, Exception? primary, CancellationToken token)
            => CompleteTestExecutionScopeAsync(broker, primary, token);
    }
}
