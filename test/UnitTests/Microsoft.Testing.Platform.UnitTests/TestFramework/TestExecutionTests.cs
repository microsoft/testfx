// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TestExecutionTests
{
    private readonly AsyncLocal<string?> _ambientExecution = new();

    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task StartTestExecutionAsync_PublishesExactInProgressMessage()
    {
        List<(IDataProducer Producer, IData Data)> published = [];
        Mock<IMessageBus> messageBus = CreateMessageBus(published);
        IDataProducer producer = Mock.Of<IDataProducer>();
        TestNodeUpdateMessage startMessage = CreateInProgressMessage();
        TestExecutionActivityBroker broker = CreateBroker(out Mock<IPlatformOpenTelemetryServiceWithTestExecutionActivities> service, out _);
        using (broker)
        {
            ExecuteRequestContext context = CreateContext(messageBus.Object, broker, TestContext.CancellationToken);

            using TestExecution execution = await context.StartTestExecutionAsync(producer, startMessage);

            Assert.HasCount(1, published);
            Assert.AreSame(producer, published[0].Producer);
            Assert.AreSame(startMessage, published[0].Data);
            Assert.IsTrue(startMessage.TestNode.Properties.Any<InProgressTestNodeStateProperty>());
            TestExecutionActivityProperty property = startMessage.TestNode.Properties.Single<TestExecutionActivityProperty>();
            Assert.IsFalse(property.IsFinalResult);
            service.Verify(
                s => s.StartTestExecutionActivity(
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<DateTimeOffset>()),
                Times.Once);
        }
    }

    [TestMethod]
    public async Task StartTestExecutionAsync_ActivatesOnlyAfterPublishCompletes()
    {
        TaskCompletionSource<bool> publishStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> allowPublishToComplete = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool publishCompleted = false;
        Mock<IMessageBus> messageBus = new();
        messageBus.Setup(m => m.PublishAsync(It.IsAny<IDataProducer>(), It.IsAny<IData>()))
            .Returns(async () =>
            {
                publishStarted.SetResult(true);
                await allowPublishToComplete.Task;
                publishCompleted = true;
            });
        TestExecutionActivityBroker broker = CreateBroker(
            out Mock<IPlatformOpenTelemetryServiceWithTestExecutionActivities> service,
            out Mock<IPlatformTestExecutionActivity> activity);
        service.Setup(s => s.StartTestExecutionActivity(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTimeOffset>()))
            .Returns(() =>
            {
                Assert.IsTrue(publishCompleted);
                return activity.Object;
            });
        using (broker)
        {
            ExecuteRequestContext context = CreateContext(messageBus.Object, broker, TestContext.CancellationToken);

            Task<TestExecution> startTask = context.StartTestExecutionAsync(
                Mock.Of<IDataProducer>(),
                CreateInProgressMessage());
            await publishStarted.Task;

            service.Verify(
                s => s.StartTestExecutionActivity(
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<DateTimeOffset>()),
                Times.Never);

            allowPublishToComplete.SetResult(true);
            using TestExecution execution = await startTask;

            service.Verify(
                s => s.StartTestExecutionActivity(
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<DateTimeOffset>()),
                Times.Once);
        }
    }

    [TestMethod]
    public async Task RunAndRunOfT_EnterAndRestoreAmbientContext()
    {
        Mock<IMessageBus> messageBus = CreateMessageBus([]);
        IDataProducer producer = Mock.Of<IDataProducer>();
        TestExecutionActivityBroker broker = CreateBroker(out _, out Mock<IPlatformTestExecutionActivity> activity);
        activity.Setup(a => a.Enter()).Returns(() => new AmbientScope(_ambientExecution, "execution"));
        using (broker)
        using (TestExecution execution = await StartExecutionAsync(messageBus.Object, producer, broker))
        {
            _ambientExecution.Value = "outer";
            bool actionCalled = false;

            execution.Run(() =>
            {
                Assert.AreEqual("execution", _ambientExecution.Value);
                actionCalled = true;
            });
            int result = execution.Run(() =>
            {
                Assert.AreEqual("execution", _ambientExecution.Value);
                return 42;
            });

            Assert.IsTrue(actionCalled);
            Assert.AreEqual(42, result);
            Assert.AreEqual("outer", _ambientExecution.Value);
            activity.Verify(a => a.Enter(), Times.Exactly(2));
        }
    }

    [TestMethod]
    public async Task RunAsyncAndRunAsyncOfT_EnterAndRestoreAmbientContext()
    {
        Mock<IMessageBus> messageBus = CreateMessageBus([]);
        IDataProducer producer = Mock.Of<IDataProducer>();
        TestExecutionActivityBroker broker = CreateBroker(out _, out Mock<IPlatformTestExecutionActivity> activity);
        activity.Setup(a => a.Enter()).Returns(() => new AmbientScope(_ambientExecution, "execution"));
        using (broker)
        using (TestExecution execution = await StartExecutionAsync(messageBus.Object, producer, broker))
        {
            _ambientExecution.Value = "outer";
            bool callbackCompleted = false;

            await execution.RunAsync(async () =>
            {
                Assert.AreEqual("execution", _ambientExecution.Value);
                await Task.Yield();
                Assert.AreEqual("execution", _ambientExecution.Value);
                callbackCompleted = true;
            });
            int result = await execution.RunAsync(async () =>
            {
                Assert.AreEqual("execution", _ambientExecution.Value);
                await Task.Yield();
                Assert.AreEqual("execution", _ambientExecution.Value);
                return 42;
            });

            Assert.IsTrue(callbackCompleted);
            Assert.AreEqual(42, result);
            Assert.AreEqual("outer", _ambientExecution.Value);
            activity.Verify(a => a.Enter(), Times.Exactly(2));
        }
    }

    [TestMethod]
    public async Task Run_WhenCallbackThrows_RestoresAmbientContext()
    {
        Mock<IMessageBus> messageBus = CreateMessageBus([]);
        TestExecutionActivityBroker broker = CreateBroker(out _, out Mock<IPlatformTestExecutionActivity> activity);
        activity.Setup(a => a.Enter()).Returns(() => new AmbientScope(_ambientExecution, "execution"));
        using (broker)
        using (TestExecution execution = await StartExecutionAsync(messageBus.Object, Mock.Of<IDataProducer>(), broker))
        {
            _ambientExecution.Value = "outer";

            InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
                execution.Run(() =>
                {
                    Assert.AreEqual("execution", _ambientExecution.Value);
                    throw new InvalidOperationException("callback failed");
                }));

            Assert.AreEqual("callback failed", exception.Message);
            Assert.AreEqual("outer", _ambientExecution.Value);
        }
    }

    [TestMethod]
    public async Task RunAsync_WhenCallbackThrows_RestoresAmbientContext()
    {
        Mock<IMessageBus> messageBus = CreateMessageBus([]);
        TestExecutionActivityBroker broker = CreateBroker(out _, out Mock<IPlatformTestExecutionActivity> activity);
        activity.Setup(a => a.Enter()).Returns(() => new AmbientScope(_ambientExecution, "execution"));
        using (broker)
        using (TestExecution execution = await StartExecutionAsync(messageBus.Object, Mock.Of<IDataProducer>(), broker))
        {
            _ambientExecution.Value = "outer";

            InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
                execution.RunAsync(async () =>
                {
                    Assert.AreEqual("execution", _ambientExecution.Value);
                    await Task.Yield();
                    throw new InvalidOperationException("async callback failed");
                }));

            Assert.AreEqual("async callback failed", exception.Message);
            Assert.AreEqual("outer", _ambientExecution.Value);
        }
    }

    [TestMethod]
    public async Task CompleteAsync_OneResult_PublishesFinalMarkerAndUsesProducerEndTime()
    {
        List<(IDataProducer Producer, IData Data)> published = [];
        Mock<IMessageBus> messageBus = CreateMessageBus(
            published,
            (_, data) =>
            {
                if (data is TestNodeUpdateMessage message
                    && message.TestNode.Properties.Any<PassedTestNodeStateProperty>())
                {
                    TestExecutionActivityProperty property = message.TestNode.Properties.Single<TestExecutionActivityProperty>();
                    Assert.IsTrue(property.Reservation.ProcessResult(
                        TestingPlatformSemanticConventions.TestResultStatus.Pass,
                        contributesToAggregate: true,
                        property.IsFinalResult,
                        (_, _, _, _) => { }));
                }

                return Task.CompletedTask;
            });
        IDataProducer producer = Mock.Of<IDataProducer>();
        TestExecutionActivityBroker broker = CreateBroker(out _, out Mock<IPlatformTestExecutionActivity> activity);
        using (broker)
        {
            TestNodeUpdateMessage startMessage = CreateInProgressMessage();
            using TestExecution execution = await StartExecutionAsync(messageBus.Object, producer, broker, startMessage);
            TestNodeUpdateMessage resultMessage = CreateResultMessage(startMessage, PassedTestNodeStateProperty.CachedInstance);
            DateTimeOffset executionEnd = DateTimeOffset.UtcNow.AddSeconds(1);

            await execution.CompleteAsync([resultMessage], executionEnd);

            Assert.HasCount(2, published);
            Assert.AreSame(startMessage, published[0].Data);
            Assert.AreSame(resultMessage, published[1].Data);
            Assert.AreSame(producer, published[1].Producer);
            TestExecutionActivityProperty startProperty = startMessage.TestNode.Properties.Single<TestExecutionActivityProperty>();
            TestExecutionActivityProperty resultProperty = resultMessage.TestNode.Properties.Single<TestExecutionActivityProperty>();
            Assert.AreSame(startProperty.Reservation, resultProperty.Reservation);
            Assert.IsFalse(startProperty.IsFinalResult);
            Assert.IsTrue(resultProperty.IsFinalResult);
            activity.Verify(a => a.Stop(executionEnd), Times.Once);
        }
    }

    [TestMethod]
    public async Task CompleteAsync_MultipleResults_PublishesInOrderAndMarksOnlyLastAsFinal()
    {
        List<(IDataProducer Producer, IData Data)> published = [];
        Mock<IMessageBus> messageBus = CreateMessageBus(published);
        IDataProducer producer = Mock.Of<IDataProducer>();
        TestExecutionActivityBroker broker = CreateBroker(out _, out _);
        using (broker)
        {
            TestNodeUpdateMessage startMessage = CreateInProgressMessage();
            using TestExecution execution = await StartExecutionAsync(messageBus.Object, producer, broker, startMessage);
            TestNodeUpdateMessage firstResult = CreateResultMessage(startMessage, new FailedTestNodeStateProperty("first"));
            TestNodeUpdateMessage finalResult = CreateResultMessage(startMessage, PassedTestNodeStateProperty.CachedInstance);

            await execution.CompleteAsync([firstResult, finalResult], DateTimeOffset.UtcNow.AddSeconds(1));

            Assert.HasCount(3, published);
            Assert.AreSame(startMessage, published[0].Data);
            Assert.AreSame(firstResult, published[1].Data);
            Assert.AreSame(finalResult, published[2].Data);
            Assert.AreSame(producer, published[1].Producer);
            Assert.AreSame(producer, published[2].Producer);

            TestExecutionActivityProperty startProperty = startMessage.TestNode.Properties.Single<TestExecutionActivityProperty>();
            TestExecutionActivityProperty firstProperty = firstResult.TestNode.Properties.Single<TestExecutionActivityProperty>();
            TestExecutionActivityProperty finalProperty = finalResult.TestNode.Properties.Single<TestExecutionActivityProperty>();
            Assert.AreSame(startProperty.Reservation, firstProperty.Reservation);
            Assert.AreSame(startProperty.Reservation, finalProperty.Reservation);
            Assert.IsFalse(startProperty.IsFinalResult);
            Assert.IsFalse(firstProperty.IsFinalResult);
            Assert.IsTrue(finalProperty.IsFinalResult);
        }
    }

    [TestMethod]
    public async Task CompleteAsync_EmptyResults_PublishesCompletionWithoutInventingResult()
    {
        List<(IDataProducer Producer, IData Data)> published = [];
        Mock<IMessageBus> messageBus = CreateMessageBus(published);
        TestExecutionActivityBroker broker = CreateBroker(out _, out Mock<IPlatformTestExecutionActivity> activity);
        using (broker)
        {
            using TestExecution execution = await StartExecutionAsync(messageBus.Object, Mock.Of<IDataProducer>(), broker);
            DateTimeOffset executionEnd = DateTimeOffset.UtcNow.AddSeconds(1);

            await execution.CompleteAsync([], executionEnd);

            Assert.HasCount(2, published);
            var completionMessage = (TestNodeUpdateMessage)published[1].Data;
            Assert.IsTrue(completionMessage.TestNode.Properties.Any<TestNodeExecutionCompletedProperty>());
            Assert.IsFalse(completionMessage.TestNode.Properties.Any<TestNodeStateProperty>());
            activity.Verify(a => a.Stop(executionEnd), Times.Once);
        }
    }

    [TestMethod]
    public async Task Dispose_AbandonsOnceWithoutThrowingOrInventingResult()
    {
        List<(IDataProducer Producer, IData Data)> published = [];
        Mock<IMessageBus> messageBus = CreateMessageBus(published);
        TestExecutionActivityBroker broker = CreateBroker(out _, out Mock<IPlatformTestExecutionActivity> activity);
        activity.Setup(a => a.Stop(It.IsAny<DateTimeOffset>())).Throws(new InvalidOperationException("stop failed"));
        using (broker)
        {
            TestExecution execution = await StartExecutionAsync(messageBus.Object, Mock.Of<IDataProducer>(), broker);

            execution.Dispose();
            execution.Dispose();

            Assert.HasCount(1, published);
            Assert.IsTrue(((TestNodeUpdateMessage)published[0].Data).TestNode.Properties.Any<InProgressTestNodeStateProperty>());
            activity.Verify(a => a.Stop(It.IsAny<DateTimeOffset>()), Times.Once);
        }
    }

    [TestMethod]
    public async Task CompleteAsync_WhenAlreadyCompleted_ThrowsWithoutPublishingAgain()
    {
        List<(IDataProducer Producer, IData Data)> published = [];
        Mock<IMessageBus> messageBus = CreateMessageBus(published);
        TestNodeUpdateMessage startMessage = CreateInProgressMessage();
        using TestExecution execution = await StartExecutionAsync(messageBus.Object, Mock.Of<IDataProducer>(), broker: null, startMessage);
        await execution.CompleteAsync([], DateTimeOffset.UtcNow);
        TestNodeUpdateMessage resultMessage = CreateResultMessage(startMessage, PassedTestNodeStateProperty.CachedInstance);

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => execution.CompleteAsync([resultMessage], DateTimeOffset.UtcNow));

        Assert.AreEqual("The test execution has already been completed.", exception.Message);
        Assert.HasCount(2, published);
    }

    [TestMethod]
    public async Task CompleteAsync_AfterDispose_ThrowsWithoutPublishingResult()
    {
        List<(IDataProducer Producer, IData Data)> published = [];
        Mock<IMessageBus> messageBus = CreateMessageBus(published);
        TestNodeUpdateMessage startMessage = CreateInProgressMessage();
        TestExecution execution = await StartExecutionAsync(messageBus.Object, Mock.Of<IDataProducer>(), broker: null, startMessage);
        execution.Dispose();
        TestNodeUpdateMessage resultMessage = CreateResultMessage(startMessage, PassedTestNodeStateProperty.CachedInstance);

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
            () => execution.CompleteAsync([resultMessage], DateTimeOffset.UtcNow));

        Assert.HasCount(1, published);
    }

    [TestMethod]
    public async Task LegacyPublicConstructor_PassesThroughWithoutExecutionActivityBroker()
    {
        List<(IDataProducer Producer, IData Data)> published = [];
        Mock<IMessageBus> messageBus = CreateMessageBus(published);
        Mock<IRequest> request = new();
        Mock<IExecuteRequestCompletionNotifier> completionNotifier = new();
        using CancellationTokenSource cancellationSource = new();
        ExecuteRequestContext context = new(
            request.Object,
            messageBus.Object,
            completionNotifier.Object,
            cancellationSource.Token);
        IDataProducer producer = Mock.Of<IDataProducer>();
        TestNodeUpdateMessage startMessage = CreateInProgressMessage();

        Assert.AreSame(request.Object, context.Request);
        Assert.AreSame(messageBus.Object, context.MessageBus);
        Assert.AreEqual(cancellationSource.Token, context.CancellationToken);
        context.Complete();
        using TestExecution execution = await context.StartTestExecutionAsync(producer, startMessage);
        int callbackResult = execution.Run(() => 42);
        TestNodeUpdateMessage resultMessage = CreateResultMessage(startMessage, PassedTestNodeStateProperty.CachedInstance);
        await execution.CompleteAsync([resultMessage], DateTimeOffset.UtcNow);

        completionNotifier.Verify(n => n.Complete(), Times.Once);
        Assert.AreEqual(42, callbackResult);
        Assert.HasCount(2, published);
        Assert.AreSame(startMessage, published[0].Data);
        Assert.AreSame(resultMessage, published[1].Data);
        Assert.IsFalse(startMessage.TestNode.Properties.Any<TestExecutionActivityProperty>());
        Assert.IsFalse(resultMessage.TestNode.Properties.Any<TestExecutionActivityProperty>());
    }

    [TestMethod]
    public async Task SampledOutActivity_StillAllowsExecutionAndCanonicalCompletion()
    {
        List<(IDataProducer Producer, IData Data)> published = [];
        Mock<IMessageBus> messageBus = CreateMessageBus(published);
        TestExecutionActivityBroker broker = CreateBroker(
            out Mock<IPlatformOpenTelemetryServiceWithTestExecutionActivities> service,
            out _,
            sampledOut: true);
        using (broker)
        {
            TestNodeUpdateMessage startMessage = CreateInProgressMessage();
            using TestExecution execution = await StartExecutionAsync(
                messageBus.Object,
                Mock.Of<IDataProducer>(),
                broker,
                startMessage);
            bool callbackCalled = false;

            execution.Run(() => callbackCalled = true);
            await execution.CompleteAsync([], DateTimeOffset.UtcNow);

            Assert.IsTrue(callbackCalled);
            Assert.HasCount(2, published);
            Assert.IsNotNull(startMessage.TestNode.Properties.SingleOrDefault<TestExecutionActivityProperty>());
            service.Verify(
                s => s.StartTestExecutionActivity(
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<DateTimeOffset>()),
                Times.Once);
        }
    }

    [TestMethod]
    public async Task StartTestExecutionAsync_WhenContextIsCanceled_DoesNotPublishOrReserve()
    {
        Mock<IMessageBus> messageBus = new(MockBehavior.Strict);
        TestExecutionActivityBroker broker = CreateBroker(out Mock<IPlatformOpenTelemetryServiceWithTestExecutionActivities> service, out _);
        using CancellationTokenSource cancellationSource = new();
        cancellationSource.Cancel();
        using (broker)
        {
            ExecuteRequestContext context = CreateContext(messageBus.Object, broker, cancellationSource.Token);

            await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                () => context.StartTestExecutionAsync(Mock.Of<IDataProducer>(), CreateInProgressMessage()));

            service.Verify(
                s => s.StartTestExecutionActivity(
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<DateTimeOffset>()),
                Times.Never);
            messageBus.VerifyNoOtherCalls();
        }
    }

    [TestMethod]
    public async Task StartTestExecutionAsync_WhenContextIsCanceledDuringPublish_DoesNotActivate()
    {
        using CancellationTokenSource cancellationSource = new();
        List<(IDataProducer Producer, IData Data)> published = [];
        Mock<IMessageBus> messageBus = CreateMessageBus(
            published,
            (_, _) =>
            {
                cancellationSource.Cancel();
                return Task.CompletedTask;
            });
        TestExecutionActivityBroker broker = CreateBroker(
            out Mock<IPlatformOpenTelemetryServiceWithTestExecutionActivities> service,
            out Mock<IPlatformTestExecutionActivity> activity);
        using (broker)
        {
            ExecuteRequestContext context = CreateContext(messageBus.Object, broker, cancellationSource.Token);

            await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                () => context.StartTestExecutionAsync(Mock.Of<IDataProducer>(), CreateInProgressMessage()));

            Assert.HasCount(1, published);
            service.Verify(
                s => s.StartTestExecutionActivity(
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<DateTimeOffset>()),
                Times.Never);
            activity.Verify(a => a.Stop(It.IsAny<DateTimeOffset>()), Times.Never);
        }
    }

    [TestMethod]
    public async Task RequestScopeBrokerDisposal_FinalizesOutstandingExecutionOnce()
    {
        List<(IDataProducer Producer, IData Data)> published = [];
        Mock<IMessageBus> messageBus = CreateMessageBus(published);
        TestExecutionActivityBroker broker = CreateBroker(out _, out Mock<IPlatformTestExecutionActivity> activity);
        TestExecution execution = await StartExecutionAsync(messageBus.Object, Mock.Of<IDataProducer>(), broker);

        broker.Dispose();
        broker.Dispose();
        execution.Dispose();

        Assert.HasCount(1, published);
        activity.Verify(a => a.Stop(It.IsAny<DateTimeOffset>()), Times.Once);
    }

    private static ExecuteRequestContext CreateContext(
        IMessageBus messageBus,
        TestExecutionActivityBroker? broker,
        CancellationToken cancellationToken = default)
        => new(
            Mock.Of<IRequest>(),
            messageBus,
            Mock.Of<IExecuteRequestCompletionNotifier>(),
            cancellationToken,
            broker);

    private async Task<TestExecution> StartExecutionAsync(
        IMessageBus messageBus,
        IDataProducer producer,
        TestExecutionActivityBroker? broker,
        TestNodeUpdateMessage? startMessage = null)
    {
        ExecuteRequestContext context = CreateContext(messageBus, broker, TestContext.CancellationToken);
        return await context.StartTestExecutionAsync(producer, startMessage ?? CreateInProgressMessage());
    }

    private static Mock<IMessageBus> CreateMessageBus(
        List<(IDataProducer Producer, IData Data)> published,
        Func<IDataProducer, IData, Task>? onPublish = null)
    {
        Mock<IMessageBus> messageBus = new();
        messageBus.Setup(m => m.PublishAsync(It.IsAny<IDataProducer>(), It.IsAny<IData>()))
            .Returns<IDataProducer, IData>(async (producer, data) =>
            {
                published.Add((producer, data));
                if (onPublish is not null)
                {
                    await onPublish(producer, data);
                }
            });
        return messageBus;
    }

    private static TestExecutionActivityBroker CreateBroker(
        out Mock<IPlatformOpenTelemetryServiceWithTestExecutionActivities> service,
        out Mock<IPlatformTestExecutionActivity> activity,
        bool sampledOut = false)
    {
        service = new();
        activity = new();
        service.Setup(s => s.StartTestExecutionActivity(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTimeOffset>()))
            .Returns(sampledOut ? null : activity.Object);
        return new TestExecutionActivityBroker(service.Object, PlatformOpenTelemetryOptions.Default);
    }

    private static TestNodeUpdateMessage CreateInProgressMessage()
        => new(
            new SessionUid("session"),
            new TestNode
            {
                Uid = new TestNodeUid("test"),
                DisplayName = "Test",
                Properties = new PropertyBag(InProgressTestNodeStateProperty.CachedInstance),
            },
            new TestNodeUid("parent"));

    private static TestNodeUpdateMessage CreateResultMessage(
        TestNodeUpdateMessage startMessage,
        TestNodeStateProperty state)
        => new(
            startMessage.SessionUid,
            new TestNode
            {
                Uid = startMessage.TestNode.Uid,
                DisplayName = startMessage.TestNode.DisplayName,
                Properties = new PropertyBag(state),
            },
            startMessage.ParentTestNodeUid);

    private sealed class AmbientScope : IDisposable
    {
        private readonly AsyncLocal<string?> _ambientExecution;
        private readonly string? _previousValue;
        private bool _disposed;

        public AmbientScope(AsyncLocal<string?> ambientExecution, string value)
        {
            _ambientExecution = ambientExecution;
            _previousValue = ambientExecution.Value;
            ambientExecution.Value = value;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _ambientExecution.Value = _previousValue;
        }
    }
}
