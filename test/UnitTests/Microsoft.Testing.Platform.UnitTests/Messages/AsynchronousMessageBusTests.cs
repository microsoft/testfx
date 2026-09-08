// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class AsynchronousMessageBusTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task UnexpectedTypePublished_ShouldFail()
    {
        using MessageBusProxy proxy = new();
        InvalidTypePublished consumer = new(proxy);
        var asynchronousMessageBus = new AsynchronousMessageBus(
            [consumer],
            new CTRLPlusCCancellationTokenSource(),
            new SystemTask(),
            new NopLoggerFactory(),
            new SystemEnvironment());
        await asynchronousMessageBus.InitAsync();
        proxy.SetBuiltMessageBus(asynchronousMessageBus);

        // Fire consume with a good message
        await proxy.PublishAsync(new DummyProducer("DummyProducer", typeof(InvalidTypePublished.ValidDataToProduce)), new InvalidTypePublished.ValidDataToProduce());
        consumer.Published.WaitOne();
        await Assert.ThrowsAsync<InvalidOperationException>(proxy.DrainDataAsync);
    }

    // This test relies on the background consumer tasks (started via ITask.Run) ping-ponging
    // messages between the two consumers so the drain keeps observing newly received payloads and
    // ultimately surfaces the publisher/consumer loop. On .NET Framework, under the assembly's
    // method-level parallelism (many concurrent test methods competing for the slowly-growing
    // ThreadPool), those background tasks can be starved long enough that the drain finishes its
    // attempts without seeing the loop, making the test intermittently fail (see #6892). Running it
    // non-parallel removes that contention and keeps the loop detection deterministic.
    [TestMethod]
    [DoNotParallelize]
    public async Task DrainDataAsync_Loop_ShouldFail()
    {
        using MessageBusProxy proxy = new();
        LoopConsumerA consumerA = new(proxy);
        ConsumerB consumerB = new(proxy);
        var asynchronousMessageBus = new AsynchronousMessageBus(
            [consumerA, consumerB],
            new CTRLPlusCCancellationTokenSource(),
            new SystemTask(),
            new NopLoggerFactory(),
            new SystemEnvironment());
        await asynchronousMessageBus.InitAsync();
        proxy.SetBuiltMessageBus(asynchronousMessageBus);

        await proxy.PublishAsync(consumerA, new LoopDataA());

        InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(asynchronousMessageBus.DrainDataAsync);
        Assert.Contains("Publisher/Consumer loop detected during the drain after", ex.Message);

        // Prevent loop to continue
        consumerA.StopConsume();
        consumerB.StopConsume();
    }

    [TestMethod]
    public async Task MessageBus_WhenConsumerProducesAndConsumesTheSameType_ShouldNotConsumeWhatProducedByItself()
    {
        using MessageBusProxy proxy = new();
        Consumer consumerA = new(proxy, "consumerA");
        Consumer consumerB = new(proxy, "consumerB");
        var asynchronousMessageBus = new AsynchronousMessageBus(
            [consumerA, consumerB],
            new CTRLPlusCCancellationTokenSource(),
            new SystemTask(),
            new NopLoggerFactory(),
            new SystemEnvironment());
        await asynchronousMessageBus.InitAsync();
        proxy.SetBuiltMessageBus(asynchronousMessageBus);

        Data consumerAData = new();
        Data consumerBData = new();

        await proxy.PublishAsync(consumerA, consumerAData);
        await proxy.PublishAsync(consumerB, consumerBData);

        await proxy.DrainDataAsync();

        // assert
        Assert.HasCount(1, consumerA.ConsumedData);
        Assert.AreEqual(consumerBData, consumerA.ConsumedData[0]);

        Assert.HasCount(1, consumerB.ConsumedData);
        Assert.AreEqual(consumerAData, consumerB.ConsumedData[0]);
    }

    [TestMethod]
    public async Task DisableAsync_WithConsumerSubscribedToMultipleDataTypes_ShouldCompleteProcessorOnce()
    {
        using MessageBusProxy proxy = new();
        MultiTypeConsumer consumer = new();
        using var asynchronousMessageBus = new AsynchronousMessageBus(
            [consumer],
            new CTRLPlusCCancellationTokenSource(),
            new SystemTask(),
            new NopLoggerFactory(),
            new SystemEnvironment());
        await asynchronousMessageBus.InitAsync();
        proxy.SetBuiltMessageBus(asynchronousMessageBus);

        DummyProducer producer = new("MultiTypeProducer", typeof(MultiTypeConsumer.DataTypeA), typeof(MultiTypeConsumer.DataTypeB));
        await proxy.PublishAsync(producer, new MultiTypeConsumer.DataTypeA());
        await proxy.PublishAsync(producer, new MultiTypeConsumer.DataTypeB());
        await proxy.DrainDataAsync();

        Assert.AreEqual(1, consumer.ReceivedTypeA);
        Assert.AreEqual(1, consumer.ReceivedTypeB);

        // DisableAsync must not throw even though the consumer is registered for 2 data types;
        // the single backing processor must be completed exactly once (not once per data type).
        await asynchronousMessageBus.DisableAsync();
    }

    [TestMethod]
    public async Task Consumers_ConsumeData_ShouldNotMissAnyPayload()
    {
        int totalConsumers = Environment.ProcessorCount;
        int totalPayloads = Environment.ProcessorCount * 3;
        using MessageBusProxy proxy = new();
        List<DummyConsumer> dummyConsumers = [];
        Random random = new();
        for (int i = 0; i < totalConsumers; i++)
        {
            DummyConsumer dummyConsumer = new(async _ => await Task.Delay(random.Next(40, 80), TestContext.CancellationToken));
            dummyConsumers.Add(dummyConsumer);
        }

        using var asynchronousMessageBus = new AsynchronousMessageBus(
            dummyConsumers.ToArray(),
            new CTRLPlusCCancellationTokenSource(),
            new SystemTask(),
            new NopLoggerFactory(),
            new SystemEnvironment());
        await asynchronousMessageBus.InitAsync();

        proxy.SetBuiltMessageBus(asynchronousMessageBus);

        DummyConsumer.DummyProducer producer = new();
        await Task.WhenAll([.. Enumerable.Range(1, totalPayloads).Select(i => Task.Run(async () => await proxy.PublishAsync(producer, new DummyConsumer.DummyData { Data = i }), TestContext.CancellationToken))]);

        await proxy.DrainDataAsync();

        Assert.HasCount(totalConsumers, dummyConsumers);
        foreach (DummyConsumer consumer in dummyConsumers)
        {
            Assert.HasCount(totalPayloads, consumer.DummyDataList);

            int i = 1;
            foreach (DummyConsumer.DummyData payload in consumer.DummyDataList.OrderBy(x => x.Data))
            {
                Assert.AreEqual(i, payload.Data);
                i++;
            }
        }
    }

    [TestMethod]
    public async Task BlockingDataConsumer_PublishAsync_DoesNotReturnUntilConsumeCompletes()
    {
        using MessageBusProxy proxy = new();
        BlockingConsumer consumer = new("BlockingConsumer");
        using var asynchronousMessageBus = new AsynchronousMessageBus(
            [consumer],
            new CTRLPlusCCancellationTokenSource(),
            new SystemTask(),
            new NopLoggerFactory(),
            new SystemEnvironment());
        await asynchronousMessageBus.InitAsync();
        proxy.SetBuiltMessageBus(asynchronousMessageBus);

        DummyProducer producer = new("BlockingProducer", typeof(BlockingData));

        // Start publishing. Because the consumer is blocking, PublishAsync must not complete until
        // ConsumeAsync completes, which we control through the gate below.
        Task publishTask = proxy.PublishAsync(producer, new BlockingData());

        // Wait for the consumer to be invoked inline. This proves the consumption happens as part of
        // the publish call rather than being deferred to a background loop.
        await consumer.ConsumeStarted.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);

        // The consumer is still gated, so the publish call cannot have returned yet.
        Assert.IsFalse(publishTask.IsCompleted);

        // Release the gate and let consumption (and therefore the publish) complete.
        consumer.AllowConsumeToComplete.SetResult(true);
        await publishTask;

        Assert.HasCount(1, consumer.ConsumedData);

        await asynchronousMessageBus.DisableAsync();
    }

    [TestMethod]
    public async Task BlockingDataConsumer_WhenConsumerIsProducer_ShouldNotConsumeOwnData()
    {
        using MessageBusProxy proxy = new();
        BlockingConsumer consumer = new("BlockingConsumer");

        // Release the gate upfront: if the consumer were (incorrectly) invoked for its own data, the
        // test would fail fast on the assertion below instead of hanging on the gate.
        consumer.AllowConsumeToComplete.SetResult(true);

        using var asynchronousMessageBus = new AsynchronousMessageBus(
            [consumer],
            new CTRLPlusCCancellationTokenSource(),
            new SystemTask(),
            new NopLoggerFactory(),
            new SystemEnvironment());
        await asynchronousMessageBus.InitAsync();
        proxy.SetBuiltMessageBus(asynchronousMessageBus);

        await proxy.PublishAsync(consumer, new BlockingData());

        Assert.IsEmpty(consumer.ConsumedData);

        await asynchronousMessageBus.DisableAsync();
    }

    [TestMethod]
    public async Task BlockingDataConsumer_SerializesConcurrentPublishes()
    {
        using MessageBusProxy proxy = new();
        BlockingConsumer consumer = new("BlockingConsumer")
        {
            ConsumeDelay = TimeSpan.FromMilliseconds(20),
        };

        // Don't gate consumption for this test; we want it to run as soon as it is invoked.
        consumer.AllowConsumeToComplete.SetResult(true);

        using var asynchronousMessageBus = new AsynchronousMessageBus(
            [consumer],
            new CTRLPlusCCancellationTokenSource(),
            new SystemTask(),
            new NopLoggerFactory(),
            new SystemEnvironment());
        await asynchronousMessageBus.InitAsync();
        proxy.SetBuiltMessageBus(asynchronousMessageBus);

        const int publishCount = 8;
        DummyProducer producer = new("BlockingProducer", typeof(BlockingData));
        await Task.WhenAll(Enumerable.Range(0, publishCount)
            .Select(_ => Task.Run(async () => await proxy.PublishAsync(producer, new BlockingData()), TestContext.CancellationToken)));

        // Even though publishes happen concurrently, the blocking processor must serialize the inline
        // consumption so the consumer never observes overlapping ConsumeAsync calls.
        Assert.AreEqual(1, consumer.MaxObservedConcurrency);
        Assert.HasCount(publishCount, consumer.ConsumedData);

        await asynchronousMessageBus.DisableAsync();
    }

    [TestMethod]
    public async Task BlockingDataConsumer_WhenConsumeThrows_PublishAsyncSurfacesTheException()
    {
        using MessageBusProxy proxy = new();
        ThrowingBlockingConsumer consumer = new();
        using var asynchronousMessageBus = new AsynchronousMessageBus(
            [consumer],
            new CTRLPlusCCancellationTokenSource(),
            new SystemTask(),
            new NopLoggerFactory(),
            new SystemEnvironment());
        await asynchronousMessageBus.InitAsync();
        proxy.SetBuiltMessageBus(asynchronousMessageBus);

        DummyProducer producer = new("BlockingProducer", typeof(BlockingData));

        // Because the consumer runs inline, its exception must propagate to the publishing producer.
        InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await proxy.PublishAsync(producer, new BlockingData()));
        Assert.AreEqual("Blocking consumer failure", ex.Message);

        await asynchronousMessageBus.DisableAsync();
    }

    [TestMethod]
    public async Task DisableAsync_CalledTwice_ShouldBeIdempotent()
    {
        using MessageBusProxy proxy = new();
        MultiTypeConsumer consumer = new();
        using var asynchronousMessageBus = new AsynchronousMessageBus(
            [consumer],
            new CTRLPlusCCancellationTokenSource(),
            new SystemTask(),
            new NopLoggerFactory(),
            new SystemEnvironment());
        await asynchronousMessageBus.InitAsync();
        proxy.SetBuiltMessageBus(asynchronousMessageBus);

        await asynchronousMessageBus.DisableAsync();

        // The session-end path disables the bus and the host teardown disables it again as a safety net,
        // so the second call has to be a no-op rather than an error.
        await asynchronousMessageBus.DisableAsync();

        Assert.AreEqual(0, consumer.ReceivedTypeA);

        // The repeat call must also leave the bus fully drained, not just avoid throwing.
        Assert.IsEmpty(asynchronousMessageBus.ConsumersStillRunning.ToList());
    }

    [TestMethod]
    public async Task DisableAsync_WhenSessionIsCanceled_ShouldWaitForTheInFlightConsumeToComplete()
    {
        using CTRLPlusCCancellationTokenSource cancellationTokenSource = new();
        using MessageBusProxy proxy = new();
        GatedConsumer consumer = new(nameof(GatedConsumer));
        using var asynchronousMessageBus = new AsynchronousMessageBus(
            [consumer],
            cancellationTokenSource,
            new SystemTask(),
            new NopLoggerFactory(),
            new SystemEnvironment());
        await asynchronousMessageBus.InitAsync();
        proxy.SetBuiltMessageBus(asynchronousMessageBus);

        DummyProducer producer = new("GatedProducer", typeof(GatedData));
        await proxy.PublishAsync(producer, new GatedData());
        await consumer.ConsumeStarted.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);

        // This is the cancellation path: the session-end notification is skipped and the platform closes the
        // message bus handshake from its teardown safety net instead.
        cancellationTokenSource.Cancel();

        Task disableTask = asynchronousMessageBus.DisableAsync();

        // The consumer is still inside ConsumeAsync. If disabling reported completion here, the platform
        // would go on to dispose the consumer while it is still consuming.
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.CancellationToken);
        Assert.IsFalse(disableTask.IsCompleted);

        consumer.AllowConsumeToComplete.SetResult(true);
        await disableTask.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);

        Assert.IsTrue(consumer.ConsumeCompleted);
    }

    [TestMethod]
    public async Task DisableAsync_WhenOneConsumerFaults_ShouldStillCompleteTheOtherConsumers()
    {
        using MessageBusProxy proxy = new();
        ThrowingConsumer throwingConsumer = new();
        GatedConsumer gatedConsumer = new(nameof(GatedConsumer));
        using var asynchronousMessageBus = new AsynchronousMessageBus(
            [throwingConsumer, gatedConsumer],
            new CTRLPlusCCancellationTokenSource(),
            new SystemTask(),
            new NopLoggerFactory(),
            new SystemEnvironment());
        await asynchronousMessageBus.InitAsync();
        proxy.SetBuiltMessageBus(asynchronousMessageBus);

        DummyProducer producer = new("GatedProducer", typeof(GatedData));
        await proxy.PublishAsync(producer, new GatedData());

        await throwingConsumer.ConsumeStarted.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
        await gatedConsumer.ConsumeStarted.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);

        Task disableTask = asynchronousMessageBus.DisableAsync();

        // The first processor has faulted, but a single misbehaving consumer must not make us abandon the
        // remaining ones: they would then keep running while they are being disposed.
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.CancellationToken);
        Assert.IsFalse(disableTask.IsCompleted);

        gatedConsumer.AllowConsumeToComplete.SetResult(true);

        InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await disableTask);
        Assert.AreEqual("Async consumer failure", ex.Message);
        Assert.IsTrue(gatedConsumer.ConsumeCompleted);
    }

    [TestMethod]
    public async Task PublishAsync_WhenBusIsDisabledAndSessionIsCanceled_ShouldBeANoOp()
    {
        using CTRLPlusCCancellationTokenSource cancellationTokenSource = new();
        using MessageBusProxy proxy = new();
        MultiTypeConsumer consumer = new();
        using var asynchronousMessageBus = new AsynchronousMessageBus(
            [consumer],
            cancellationTokenSource,
            new SystemTask(),
            new NopLoggerFactory(),
            new SystemEnvironment());
        await asynchronousMessageBus.InitAsync();
        proxy.SetBuiltMessageBus(asynchronousMessageBus);

        cancellationTokenSource.Cancel();
        await asynchronousMessageBus.DisableAsync();

        // Extensions that publish while they are being torn down must observe a silent no-op instead of a
        // failure that would mask the cancellation.
        DummyProducer producer = new("MultiTypeProducer", typeof(MultiTypeConsumer.DataTypeA));
        await proxy.PublishAsync(producer, new MultiTypeConsumer.DataTypeA());

        Assert.AreEqual(0, consumer.ReceivedTypeA);
    }

    [TestMethod]
    public async Task MessageBusProxy_DisableAsync_WhenConcreteBusWasNeverBuilt_ShouldBeANoOp()
    {
        using MessageBusProxy proxy = new();

        // The teardown safety net disables the bus for every outcome, including runs that failed before the
        // concrete bus was built. There is nothing to disable then, so this must not throw.
        await proxy.DisableAsync();

        // The other operations still require the concrete bus.
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(proxy.DrainDataAsync);
    }

    [TestMethod]
    public async Task ConsumeLoop_WhenSessionIsCanceled_ShouldStillDrainAlreadyQueuedPayloads()
    {
        using CTRLPlusCCancellationTokenSource cancellationTokenSource = new();
        using MessageBusProxy proxy = new();
        GatedConsumer consumer = new(nameof(GatedConsumer));
        using var asynchronousMessageBus = new AsynchronousMessageBus(
            [consumer],
            cancellationTokenSource,
            new SystemTask(),
            new NopLoggerFactory(),
            new SystemEnvironment());
        await asynchronousMessageBus.InitAsync();
        proxy.SetBuiltMessageBus(asynchronousMessageBus);

        // Two payloads: the consumer parks on the first one, so the second is still sitting in the channel
        // when the run gets canceled.
        DummyProducer producer = new("GatedProducer", typeof(GatedData));
        await proxy.PublishAsync(producer, new GatedData());
        await proxy.PublishAsync(producer, new GatedData());
        await consumer.ConsumeStarted.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);

        cancellationTokenSource.Cancel();
        consumer.AllowConsumeToComplete.SetResult(true);

        await asynchronousMessageBus.DisableAsync().TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);

        // The pump is not cancelable, so completing the channel drains what was already queued instead of
        // dropping it. Cancelling the read instead is how a canceled run used to lose its partial results.
        Assert.AreEqual(2, consumer.ConsumedCount);
    }

    [TestMethod]
    public async Task DisableAsync_WhenCanceledConsumerIgnoresTheToken_ShouldGiveUpAndReportItStillRunning()
    {
        using CTRLPlusCCancellationTokenSource cancellationTokenSource = new();
        using MessageBusProxy proxy = new();
        GatedConsumer consumer = new(nameof(GatedConsumer));

        // Keep the shutdown budget short so the test does not have to wait for the 30s default.
        Mock<IEnvironment> environmentMock = new();
        environmentMock
            .Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_MESSAGEBUS_CANCELED_SHUTDOWN_TIMEOUT_SECONDS))
            .Returns("0.5");

        using var asynchronousMessageBus = new AsynchronousMessageBus(
            [consumer],
            cancellationTokenSource,
            new SystemTask(),
            new NopLoggerFactory(),
            environmentMock.Object);
        await asynchronousMessageBus.InitAsync();
        proxy.SetBuiltMessageBus(asynchronousMessageBus);

        DummyProducer producer = new("GatedProducer", typeof(GatedData));
        await proxy.PublishAsync(producer, new GatedData());
        await consumer.ConsumeStarted.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);

        cancellationTokenSource.Cancel();

        // The consumer never observes the token, so an unbounded wait here would hang the abort. Unlike an
        // interactive Ctrl+C, a '--timeout' or a server-initiated cancellation has no second escape hatch.
        await asynchronousMessageBus.DisableAsync().TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);

        // Having given up on the wait, the bus must report the consumer as still running so the platform
        // leaves it alone instead of disposing it mid-consumption.
        Assert.Contains(consumer, asynchronousMessageBus.ConsumersStillRunning.ToList());

        consumer.AllowConsumeToComplete.SetResult(true);
    }

    [TestMethod]
    public async Task ConsumersStillRunning_WhenTheHandshakeCompleted_ShouldBeEmpty()
    {
        using MessageBusProxy proxy = new();
        GatedConsumer consumer = new(nameof(GatedConsumer));
        consumer.AllowConsumeToComplete.SetResult(true);

        using var asynchronousMessageBus = new AsynchronousMessageBus(
            [consumer],
            new CTRLPlusCCancellationTokenSource(),
            new SystemTask(),
            new NopLoggerFactory(),
            new SystemEnvironment());
        await asynchronousMessageBus.InitAsync();
        proxy.SetBuiltMessageBus(asynchronousMessageBus);

        DummyProducer producer = new("GatedProducer", typeof(GatedData));
        await proxy.PublishAsync(producer, new GatedData());
        await asynchronousMessageBus.DisableAsync();

        Assert.IsEmpty(asynchronousMessageBus.ConsumersStillRunning.ToList());
    }

    [TestMethod]
    public async Task DisableAsync_WhenCalledConcurrently_ShouldAwaitTheSameCompletion()
    {
        using MessageBusProxy proxy = new();
        GatedConsumer consumer = new(nameof(GatedConsumer));
        using var asynchronousMessageBus = new AsynchronousMessageBus(
            [consumer],
            new CTRLPlusCCancellationTokenSource(),
            new SystemTask(),
            new NopLoggerFactory(),
            new SystemEnvironment());
        await asynchronousMessageBus.InitAsync();
        proxy.SetBuiltMessageBus(asynchronousMessageBus);

        DummyProducer producer = new("GatedProducer", typeof(GatedData));
        await proxy.PublishAsync(producer, new GatedData());
        await consumer.ConsumeStarted.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);

        Task first = asynchronousMessageBus.DisableAsync();
        Task second = asynchronousMessageBus.DisableAsync();

        // "Disable started" must not be reported as "disable completed": a second caller that returned here
        // would go on to dispose a consumer whose loop is still running.
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.CancellationToken);
        Assert.IsFalse(second.IsCompleted);

        consumer.AllowConsumeToComplete.SetResult(true);
        await Task.WhenAll(first, second).TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);

        Assert.AreEqual(1, consumer.ConsumedCount);
    }

    [TestMethod]
    public async Task DisableAsync_WhenCancellationArrivesDuringTheWait_ShouldDowngradeToTheBoundedWait()
    {
        using CTRLPlusCCancellationTokenSource cancellationTokenSource = new();
        using MessageBusProxy proxy = new();
        GatedConsumer consumer = new(nameof(GatedConsumer));

        // Keep the shutdown budget short so the test does not have to wait for the 30s default.
        Mock<IEnvironment> environmentMock = new();
        environmentMock
            .Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_MESSAGEBUS_CANCELED_SHUTDOWN_TIMEOUT_SECONDS))
            .Returns("0.5");

        using var asynchronousMessageBus = new AsynchronousMessageBus(
            [consumer],
            cancellationTokenSource,
            new SystemTask(),
            new NopLoggerFactory(),
            environmentMock.Object);
        await asynchronousMessageBus.InitAsync();
        proxy.SetBuiltMessageBus(asynchronousMessageBus);

        DummyProducer producer = new("GatedProducer", typeof(GatedData));
        await proxy.PublishAsync(producer, new GatedData());
        await consumer.ConsumeStarted.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);

        // Nothing is canceled yet, so this is the regular session-end shutdown and the wait is unbounded: a
        // consumer flushing a large report must not be cut off.
        Task disableTask = asynchronousMessageBus.DisableAsync();
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.CancellationToken);
        Assert.IsFalse(disableTask.IsCompleted);

        // The abort arrives while that wait is in flight. It has to downgrade to the bounded wait, otherwise a
        // consumer that ignores the token keeps the abort blocked for as long as it likes.
        cancellationTokenSource.Cancel();

        Task completed = await Task.WhenAny(disableTask, Task.Delay(TimeSpan.FromSeconds(30), TestContext.CancellationToken));
        Assert.AreSame(disableTask, completed, "DisableAsync kept waiting on a consumer that ignores the cancellation token.");
        await disableTask;

        Assert.Contains(consumer, asynchronousMessageBus.ConsumersStillRunning.ToList());

        consumer.AllowConsumeToComplete.SetResult(true);
    }

    [TestMethod]
    [DataRow("1e300", DisplayName = "Out of range")]
    [DataRow("3000000", DisplayName = "Past the SemaphoreSlim limit")]
    [DataRow("1e-300", DisplayName = "Rounds down to zero")]
    [DataRow("0.0001", DisplayName = "Sub-millisecond")]
    [DataRow("NaN", DisplayName = "NaN")]
    [DataRow("0", DisplayName = "Zero")]
    [DataRow("-5", DisplayName = "Negative")]
    [DataRow("abc", DisplayName = "Not a number")]
    [DataRow("", DisplayName = "Empty")]
    public void GetCanceledConsumerCompletion_WhenValueIsInvalidOrOutOfRange_ShouldFallBackToTheDefault(string value)
    {
        Mock<IEnvironment> environmentMock = new();
        environmentMock
            .Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_MESSAGEBUS_CANCELED_SHUTDOWN_TIMEOUT_SECONDS))
            .Returns(value);

        // An optional override must never be able to break the message bus. '1e300' in particular parses fine
        // and would then overflow TimeSpan / the wait itself.
        Assert.AreEqual(
            ShutdownTimeouts.DefaultCanceledConsumerCompletion,
            ShutdownTimeouts.GetCanceledConsumerCompletion(environmentMock.Object));
    }

    [TestMethod]
    public void GetCanceledConsumerCompletion_WhenValueIsValid_ShouldUseIt()
    {
        Mock<IEnvironment> environmentMock = new();
        environmentMock
            .Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_MESSAGEBUS_CANCELED_SHUTDOWN_TIMEOUT_SECONDS))
            .Returns("1.5");

        Assert.AreEqual(TimeSpan.FromSeconds(1.5), ShutdownTimeouts.GetCanceledConsumerCompletion(environmentMock.Object));
    }

    [TestMethod]
    public void GetControllerFinalization_WhenBothOverridesAreSet_ShouldUseDedicatedValue()
    {
        Mock<IEnvironment> environmentMock = new();
        environmentMock
            .Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_MESSAGEBUS_CANCELED_SHUTDOWN_TIMEOUT_SECONDS))
            .Returns("1.5");
        environmentMock
            .Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_FINALIZATION_TIMEOUT_SECONDS))
            .Returns("2.5");

        Assert.AreEqual(TimeSpan.FromSeconds(1.5), ShutdownTimeouts.GetCanceledConsumerCompletion(environmentMock.Object));
        Assert.AreEqual(TimeSpan.FromSeconds(2.5), ShutdownTimeouts.GetControllerFinalization(environmentMock.Object));
    }

    [TestMethod]
    public async Task DisableAsync_WhenConsumerThrowsTimeoutException_ShouldNotSwallowIt()
    {
        using MessageBusProxy proxy = new();
        TimeoutThrowingConsumer consumer = new();
        using var asynchronousMessageBus = new AsynchronousMessageBus(
            [consumer],
            new CTRLPlusCCancellationTokenSource(),
            new SystemTask(),
            new NopLoggerFactory(),
            new SystemEnvironment());
        await asynchronousMessageBus.InitAsync();
        proxy.SetBuiltMessageBus(asynchronousMessageBus);

        DummyProducer producer = new("GatedProducer", typeof(GatedData));
        await proxy.PublishAsync(producer, new GatedData());

        // The shutdown budget is signalled with TimeoutException too, so a consumer that throws that exact type
        // must not be mistaken for our own timeout and silently swallowed.
        TimeoutException ex = await Assert.ThrowsExactlyAsync<TimeoutException>(async () => await asynchronousMessageBus.DisableAsync());
        Assert.AreEqual("Consumer failure", ex.Message);
    }

    [TestMethod]
    public async Task DisableAsync_WhenBlockingConsumerIgnoresTheTokenMidWait_ShouldDowngradeToTheBoundedWait()
    {
        using CTRLPlusCCancellationTokenSource cancellationTokenSource = new();
        using MessageBusProxy proxy = new();
        BlockingConsumer consumer = new("BlockingConsumer");

        // Keep the shutdown budget short so the test does not have to wait for the 30s default.
        Mock<IEnvironment> environmentMock = new();
        environmentMock
            .Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_MESSAGEBUS_CANCELED_SHUTDOWN_TIMEOUT_SECONDS))
            .Returns("0.5");

        using var asynchronousMessageBus = new AsynchronousMessageBus(
            [consumer],
            cancellationTokenSource,
            new SystemTask(),
            new NopLoggerFactory(),
            environmentMock.Object);
        await asynchronousMessageBus.InitAsync();
        proxy.SetBuiltMessageBus(asynchronousMessageBus);

        DummyProducer producer = new("BlockingProducer", typeof(BlockingData));
        Task publishTask = proxy.PublishAsync(producer, new BlockingData());
        await consumer.ConsumeStarted.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);

        // Nothing is canceled yet, so the blocking processor waits for the inline consumption without a bound.
        Task disableTask = asynchronousMessageBus.DisableAsync();
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.CancellationToken);
        Assert.IsFalse(disableTask.IsCompleted);

        // The abort arrives mid-wait. The blocking processor has to downgrade to the bounded wait just like the
        // asynchronous one, otherwise a blocking consumer that ignores its token still hangs the abort.
        cancellationTokenSource.Cancel();

        Task completed = await Task.WhenAny(disableTask, Task.Delay(TimeSpan.FromSeconds(30), TestContext.CancellationToken));
        Assert.AreSame(disableTask, completed, "DisableAsync kept waiting on a blocking consumer that ignores the cancellation token.");
        await disableTask;

        Assert.Contains(consumer, asynchronousMessageBus.ConsumersStillRunning.ToList());

        consumer.AllowConsumeToComplete.SetResult(true);
        await publishTask.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task DisableAsync_WithManyCanceledConsumersIgnoringTheToken_ShouldNotMultiplyTheBudget()
    {
        using CTRLPlusCCancellationTokenSource cancellationTokenSource = new();
        using MessageBusProxy proxy = new();

        // Eight consumers that all park inside ConsumeAsync. The processors are completed sequentially, so a
        // purely per-consumer bound would make the abort take 8 budgets instead of one.
        const int consumerCount = 8;
        const double budgetSeconds = 1;
        GatedConsumer[] consumers = [.. Enumerable.Range(0, consumerCount).Select(i => new GatedConsumer($"GatedConsumer{i}"))];

        Mock<IEnvironment> environmentMock = new();
        environmentMock
            .Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_MESSAGEBUS_CANCELED_SHUTDOWN_TIMEOUT_SECONDS))
            .Returns(budgetSeconds.ToString(CultureInfo.InvariantCulture));

        using var asynchronousMessageBus = new AsynchronousMessageBus(
            [.. consumers],
            cancellationTokenSource,
            new SystemTask(),
            new NopLoggerFactory(),
            environmentMock.Object);
        await asynchronousMessageBus.InitAsync();
        proxy.SetBuiltMessageBus(asynchronousMessageBus);

        DummyProducer producer = new("GatedProducer", typeof(GatedData));
        await proxy.PublishAsync(producer, new GatedData());
        foreach (GatedConsumer consumer in consumers)
        {
            await consumer.ConsumeStarted.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
        }

        cancellationTokenSource.Cancel();

        var stopwatch = Stopwatch.StartNew();
        await asynchronousMessageBus.DisableAsync().TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
        stopwatch.Stop();

        // Correct behaviour takes about one budget; the per-consumer regression takes about
        // consumerCount budgets. Sit halfway between the two so the check both separates them clearly and
        // keeps several times the expected duration as slack for a loaded machine.
        Assert.IsLessThan(TimeSpan.FromSeconds(consumerCount * budgetSeconds / 2), stopwatch.Elapsed);

        // Deterministic half of the same property: we bailed out of the wait with every consumer still going,
        // rather than working through them one budget at a time.
        Assert.HasCount(consumerCount, asynchronousMessageBus.ConsumersStillRunning.ToList());

        foreach (GatedConsumer consumer in consumers)
        {
            consumer.AllowConsumeToComplete.SetResult(true);
        }
    }

    [TestMethod]
    public async Task InitAsync_WhenALaterConsumerFails_ShouldTearDownTheProcessorsItAlreadyStarted()
    {
        GatedConsumer goodConsumer = new(nameof(GatedConsumer));
        DisabledConsumer failingConsumer = new();
        RecordingTask task = new();

        var asynchronousMessageBus = new AsynchronousMessageBus(
            [goodConsumer, failingConsumer],
            new CTRLPlusCCancellationTokenSource(),
            task,
            new NopLoggerFactory(),
            new SystemEnvironment());

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(asynchronousMessageBus.InitAsync);

        // The first consumer's pump was already started before the second consumer failed validation.
        Assert.IsNotEmpty(task.Started);

        // A bus whose InitAsync threw never reaches SetBuiltMessageBus, so nothing else would ever complete
        // that channel. Without the teardown the pump would park forever on a non-cancelable read, rooting the
        // consumer and everything queued for it.
        await Task.WhenAll(task.Started).TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
    }

    private sealed class RecordingTask : ITask
    {
        private readonly SystemTask _inner = new();

        public List<Task> Started { get; } = [];

        public Task Run(Func<Task> function, CancellationToken cancellationToken)
        {
            Task task = _inner.Run(function, cancellationToken);
            lock (Started)
            {
                Started.Add(task);
            }

            return task;
        }

        public Task Run(Action action) => _inner.Run(action);

        public Task<T> Run<T>(Func<Task<T>?> function, CancellationToken cancellationToken) => _inner.Run(function, cancellationToken);

        // Not used by the message bus; forwarding would trip CA1416 since it is unsupported on browser.
        public Task RunLongRunning(Func<Task> action, string name, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task WhenAll(params Task[] tasks) => _inner.WhenAll(tasks);

        public Task Delay(int millisecondDelay) => _inner.Delay(millisecondDelay);

        public Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken) => _inner.Delay(timeSpan, cancellationToken);
    }

    private sealed class DisabledConsumer : IDataConsumer
    {
        public Type[] DataTypesConsumed => [typeof(GatedData)];

        public string Uid => nameof(DisabledConsumer);

        public string Version => "1.0.0";

        public string DisplayName => string.Empty;

        public string Description => string.Empty;

        // Makes AsynchronousMessageBus.InitAsync throw after the previous consumer's processor was built.
        public Task<bool> IsEnabledAsync() => Task.FromResult(false);

        public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    // This test asserts that the idle pumps have finished within a window, which depends on the background
    // consumer tasks (started via ITask.Run) getting scheduled promptly. Under the assembly's method-level
    // parallelism those tasks can be starved on .NET Framework, exactly as documented for
    // DrainDataAsync_Loop_ShouldFail above. Running it non-parallel removes that contention.
    [TestMethod]
    [DoNotParallelize]
    public async Task DisableAsync_WhenOnlyOneCanceledConsumerIgnoresTheToken_ShouldNotReportTheCooperativeOnes()
    {
        using CTRLPlusCCancellationTokenSource cancellationTokenSource = new();
        using MessageBusProxy proxy = new();

        // The stuck consumer is registered first, so it is the one that eats the shared budget. The others are
        // idle and must still be told to stop before that wait begins.
        GatedConsumer stuckConsumer = new("StuckConsumer");
        GatedConsumer[] idleConsumers = [.. Enumerable.Range(0, 3).Select(i => new GatedConsumer($"IdleConsumer{i}"))];
        foreach (GatedConsumer idle in idleConsumers)
        {
            idle.AllowConsumeToComplete.SetResult(true);
        }

        Mock<IEnvironment> environmentMock = new();
        environmentMock
            .Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_MESSAGEBUS_CANCELED_SHUTDOWN_TIMEOUT_SECONDS))
            .Returns("1");

        using var asynchronousMessageBus = new AsynchronousMessageBus(
            [stuckConsumer, .. idleConsumers],
            cancellationTokenSource,
            new SystemTask(),
            new NopLoggerFactory(),
            environmentMock.Object);
        await asynchronousMessageBus.InitAsync();
        proxy.SetBuiltMessageBus(asynchronousMessageBus);

        DummyProducer producer = new("GatedProducer", typeof(GatedData));
        await proxy.PublishAsync(producer, new GatedData());
        await stuckConsumer.ConsumeStarted.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);

        cancellationTokenSource.Cancel();

        // Sample while the shutdown is still inside the shared budget. Correct behaviour signals every
        // processor up front, so the idle pumps have long since finished by now and only the stuck consumer is
        // reported. Signalling lazily inside the wait loop would leave them parked on a non-cancelable read,
        // still unsignalled, because the stuck consumer has not released the loop yet.
        Task disableTask = asynchronousMessageBus.DisableAsync();
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.CancellationToken);

        List<IDataConsumer> stillRunning = [.. asynchronousMessageBus.ConsumersStillRunning];
        Assert.HasCount(1, stillRunning);
        Assert.AreSame(stuckConsumer, stillRunning[0]);

        stuckConsumer.AllowConsumeToComplete.SetResult(true);
        await disableTask.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
    }

    private sealed class NopLoggerFactory : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName) => new NopLogger();
    }

    private sealed class DummyConsumer : IDataConsumer
    {
        private readonly Func<DummyData, Task> _action;

        public DummyConsumer() => _action = _ => Task.CompletedTask;

        public DummyConsumer(Func<DummyData, Task> action) => _action = action;

        public List<DummyData> DummyDataList { get; } = [];

        public Type[] DataTypesConsumed => [typeof(DummyData)];

        public string Uid => nameof(DummyConsumer);

        public string Version => PlatformVersion.Version;

        public string DisplayName => nameof(DummyConsumer);

        public string Description => nameof(DummyConsumer);

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
        {
            if (value is not DummyData dummyData)
            {
                throw new InvalidOperationException("Unexpected invalid data");
            }

            DummyDataList.Add(dummyData);
            await _action(dummyData);
        }

        public sealed class DummyData : IData
        {
            public int Data { get; set; }

            public string DisplayName => "DummyData";

            public string? Description => "DummyData";
        }

        public sealed class DummyProducer : IDataProducer
        {
            public Type[] DataTypesProduced => [typeof(DummyData)];

            public string Uid => nameof(DummyProducer);

            public string Version => PlatformVersion.Version;

            public string DisplayName => nameof(DummyProducer);

            public string Description => nameof(DummyProducer);

            public Task<bool> IsEnabledAsync() => Task.FromResult(true);
        }
    }

    private sealed class LoopDataA : IData
    {
        public string DisplayName => "LoopDataA";

        public string? Description => "LoopDataA";
    }

    private sealed class LoopConsumerA : IDataConsumer, IDataProducer
    {
        private readonly IMessageBus _messageBus;
        private bool _stopConsume;

        public LoopConsumerA(IMessageBus messageBus) => _messageBus = messageBus;

        public Type[] DataTypesConsumed => [typeof(LoopDataB)];

        public string Uid => nameof(LoopConsumerA);

        public string Version => "1.0.0";

        public string DisplayName => string.Empty;

        public string Description => string.Empty;

        public Type[] DataTypesProduced => [typeof(LoopDataA)];

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public void StopConsume() => _stopConsume = true;

        public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
        {
            if (_stopConsume)
            {
                return;
            }

            if (value is LoopDataB)
            {
                await _messageBus.PublishAsync(this, new LoopDataA());
            }
        }
    }

    private sealed class LoopDataB : IData
    {
        public string DisplayName => "LoopDataB";

        public string? Description => "LoopDataB";
    }

    private sealed class Data : IData
    {
        public string DisplayName => "Data";

        public string? Description => "Data";
    }

    private sealed class ConsumerB : IDataConsumer, IDataProducer
    {
        private readonly IMessageBus _messageBus;
        private bool _stopConsume;

        public ConsumerB(IMessageBus messageBus) => _messageBus = messageBus;

        public Type[] DataTypesConsumed => [typeof(LoopDataA)];

        public string Uid => nameof(ConsumerB);

        public string Version => "1.0.0";

        public string DisplayName => string.Empty;

        public string Description => string.Empty;

        public Type[] DataTypesProduced => [typeof(LoopDataB)];

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public void StopConsume() => _stopConsume = true;

        public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
        {
            if (_stopConsume)
            {
                return;
            }

            if (value is LoopDataA)
            {
                await _messageBus.PublishAsync(this, new LoopDataB());
            }
        }
    }

    private sealed class Consumer : IDataConsumer, IDataProducer
    {
        public Consumer(IMessageBus messageBus, string id) => Uid = id;

        public List<IData> ConsumedData { get; } = [];

        public Type[] DataTypesConsumed => [typeof(Data)];

        public string Uid { get; set; }

        public string Version => "1.0.0";

        public string DisplayName => string.Empty;

        public string Description => string.Empty;

        public Type[] DataTypesProduced => [typeof(Data)];

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
        {
            ConsumedData.Add(value);

            return Task.FromResult(true);
        }
    }

    private sealed class InvalidTypePublished : IDataConsumer, IDataProducer
    {
        private readonly IMessageBus _messageBus;

        public InvalidTypePublished(IMessageBus messageBus) => _messageBus = messageBus;

        public ManualResetEvent Published { get; set; } = new(false);

        public Type[] DataTypesConsumed => [typeof(ValidDataToProduce)];

        public string Uid => nameof(InvalidTypePublished);

        public Type[] DataTypesProduced => [typeof(ValidDataToProduce)];

        public string Version => "1.0.0";

        public string DisplayName => string.Empty;

        public string Description => string.Empty;

        public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
        {
            try
            {
                await _messageBus.PublishAsync(this, new InvalidDataToProduce());
            }
            catch
            {
                Published.Set();
                throw;
            }
        }

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public sealed class ValidDataToProduce : IData
        {
            public string DisplayName => nameof(ValidDataToProduce);

            public string? Description => nameof(ValidDataToProduce);
        }

        public sealed class InvalidDataToProduce : IData
        {
            public string DisplayName => nameof(InvalidDataToProduce);

            public string? Description => nameof(InvalidDataToProduce);
        }
    }

    private sealed class BlockingData : IData
    {
        public string DisplayName => nameof(BlockingData);

        public string? Description => nameof(BlockingData);
    }

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates.
    private sealed class BlockingConsumer : IBlockingDataConsumer, IDataProducer
    {
        private int _currentConcurrency;

        public BlockingConsumer(string id) => Uid = id;

        public List<IData> ConsumedData { get; } = [];

        public TaskCompletionSource<bool> ConsumeStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> AllowConsumeToComplete { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // When set, ConsumeAsync delays for this duration while inside the consumption to widen the
        // window in which a missing serialization guarantee would surface as overlapping calls.
        public TimeSpan ConsumeDelay { get; set; } = TimeSpan.Zero;

        public int MaxObservedConcurrency { get; private set; }

        public Type[] DataTypesConsumed => [typeof(BlockingData)];

        public Type[] DataTypesProduced => [typeof(BlockingData)];

        public string Uid { get; }

        public string Version => "1.0.0";

        public string DisplayName => string.Empty;

        public string Description => string.Empty;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
        {
            int concurrency = Interlocked.Increment(ref _currentConcurrency);
            try
            {
                lock (ConsumedData)
                {
                    MaxObservedConcurrency = Math.Max(MaxObservedConcurrency, concurrency);
                }

                ConsumeStarted.TrySetResult(true);
                await AllowConsumeToComplete.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);

                if (ConsumeDelay > TimeSpan.Zero)
                {
                    await Task.Delay(ConsumeDelay, cancellationToken);
                }

                lock (ConsumedData)
                {
                    ConsumedData.Add(value);
                }
            }
            finally
            {
                Interlocked.Decrement(ref _currentConcurrency);
            }
        }
    }

    private sealed class ThrowingBlockingConsumer : IBlockingDataConsumer
    {
        public Type[] DataTypesConsumed => [typeof(BlockingData)];

        public string Uid => nameof(ThrowingBlockingConsumer);

        public string Version => "1.0.0";

        public string DisplayName => string.Empty;

        public string Description => string.Empty;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Blocking consumer failure");
    }
#pragma warning restore TPEXP

    private sealed class GatedData : IData
    {
        public string DisplayName => nameof(GatedData);

        public string? Description => nameof(GatedData);
    }

    /// <summary>
    /// An asynchronous consumer that parks inside <see cref="ConsumeAsync"/> until the test releases it. It
    /// deliberately ignores the cancellation token so that a canceled run still leaves a consumption in flight,
    /// which is exactly the situation where disposal must not overlap consumption.
    /// </summary>
    private sealed class GatedConsumer : IDataConsumer
    {
        private int _consumedCount;

        public GatedConsumer(string id) => Uid = id;

        public TaskCompletionSource<bool> ConsumeStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> AllowConsumeToComplete { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ConsumedCount => Volatile.Read(ref _consumedCount);

        public bool ConsumeCompleted => ConsumedCount > 0;

        public Type[] DataTypesConsumed => [typeof(GatedData)];

        public string Uid { get; }

        public string Version => "1.0.0";

        public string DisplayName => string.Empty;

        public string Description => string.Empty;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
        {
            ConsumeStarted.TrySetResult(true);
            await AllowConsumeToComplete.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
            Interlocked.Increment(ref _consumedCount);
        }
    }

    private sealed class TimeoutThrowingConsumer : IDataConsumer
    {
        public Type[] DataTypesConsumed => [typeof(GatedData)];

        public string Uid => nameof(TimeoutThrowingConsumer);

        public string Version => "1.0.0";

        public string DisplayName => string.Empty;

        public string Description => string.Empty;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
            => throw new TimeoutException("Consumer failure");
    }

    private sealed class ThrowingConsumer : IDataConsumer
    {
        public TaskCompletionSource<bool> ConsumeStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Type[] DataTypesConsumed => [typeof(GatedData)];

        public string Uid => nameof(ThrowingConsumer);

        public string Version => "1.0.0";

        public string DisplayName => string.Empty;

        public string Description => string.Empty;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
        {
            ConsumeStarted.TrySetResult(true);
            throw new InvalidOperationException("Async consumer failure");
        }
    }

    private sealed class DummyProducer : IDataProducer
    {
        public DummyProducer(string producerId, params Type[] dataTypesProduced)
        {
            ProducerId = producerId;
            DataTypesProduced = dataTypesProduced;
        }

        public Type[] DataTypesProduced { get; }

        public string Uid => ProducerId;

        public string Version => "1.0.0";

        public string DisplayName => string.Empty;

        public string Description => string.Empty;

        public string ProducerId { get; }

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }

    private sealed class MultiTypeConsumer : IDataConsumer
    {
        public int ReceivedTypeA { get; private set; }

        public int ReceivedTypeB { get; private set; }

        public Type[] DataTypesConsumed => [typeof(DataTypeA), typeof(DataTypeB)];

        public string Uid => nameof(MultiTypeConsumer);

        public string Version => "1.0.0";

        public string DisplayName => string.Empty;

        public string Description => string.Empty;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
        {
            if (value is DataTypeA)
            {
                ReceivedTypeA++;
            }
            else if (value is DataTypeB)
            {
                ReceivedTypeB++;
            }

            return Task.CompletedTask;
        }

        public sealed class DataTypeA : IData
        {
            public string DisplayName => nameof(DataTypeA);

            public string? Description => nameof(DataTypeA);
        }

        public sealed class DataTypeB : IData
        {
            public string DisplayName => nameof(DataTypeB);

            public string? Description => nameof(DataTypeB);
        }
    }
}
