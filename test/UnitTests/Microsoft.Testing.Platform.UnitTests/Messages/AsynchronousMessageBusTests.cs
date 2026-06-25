// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Services;

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

        Assert.HasCount(0, consumer.ConsumedData);

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
