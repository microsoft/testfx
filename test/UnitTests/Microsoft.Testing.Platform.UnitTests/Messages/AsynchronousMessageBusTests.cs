// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.TestInfrastructure;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public sealed class AsynchronousMessageBusTests : TestBase
{
    public AsynchronousMessageBusTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

    public async Task UnexpectedTypePublished_ShouldFail()
    {
        MessageBusProxy proxy = new();
        InvalidTypePublished consumer = new(proxy);
        AsynchronousMessageBus asynchronousMessageBus = new(
            [consumer],
            new CTRLPlusCCancellationTokenSource(),
            new SystemTask(),
            new NopLoggerFactory(),
            new SystemEnvironment());
        await asynchronousMessageBus.InitAsync();
        proxy.SetBuiltMessageBus(asynchronousMessageBus);

        // Fire consume with a good message
        await proxy.PublishAsync(new DummyProducer("DummyProducer", typeof(InvalidTypePublished.ValidDataToProduce)), new InvalidTypePublished.ValidDataToProduce());
        consumer.Published.WaitOne(TimeoutHelper.DefaultHangTimeoutMilliseconds);
        await Assert.ThrowsAsync<InvalidOperationException>(() => proxy.DrainDataAsync());
    }

    public async Task DrainDataAsync_Loop_ShouldFail()
    {
        MessageBusProxy proxy = new();
        LoopConsumerA consumerA = new(proxy);
        ConsumerB consumerB = new(proxy);
        AsynchronousMessageBus asynchronousMessageBus = new(
            [consumerA, consumerB],
            new CTRLPlusCCancellationTokenSource(),
            new SystemTask(),
            new NopLoggerFactory(),
            new SystemEnvironment());
        await asynchronousMessageBus.InitAsync();
        proxy.SetBuiltMessageBus(asynchronousMessageBus);

        await proxy.PublishAsync(consumerA, new LoopDataA());

        try
        {
            await asynchronousMessageBus.DrainDataAsync();
        }
        catch (InvalidOperationException ex)
        {
            Assert.That(ex.Message.Contains("Publisher/Consumer loop detected during the drain after"));
        }

        // Prevent loop to continue
        consumerA.StopConsume();
        consumerB.StopConsume();
    }

    public async Task MessageBus_WhenConsumerProducesAndConsumesTheSameType_ShouldNotConsumeWhatProducedByItself()
    {
        MessageBusProxy proxy = new();
        Consumer consumerA = new(proxy, "consumerA");
        Consumer consumerB = new(proxy, "consumerB");
        AsynchronousMessageBus asynchronousMessageBus = new(
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
        Assert.AreEqual(consumerA.ConsumedData.Count, 1);
        Assert.AreEqual(consumerA.ConsumedData[0], consumerBData);

        Assert.AreEqual(consumerB.ConsumedData.Count, 1);
        Assert.AreEqual(consumerB.ConsumedData[0], consumerAData);
    }

    public async Task Consumers_ConsumeData_ShouldNotMissAnyPayload()
    {
        int totalConsumers = Environment.ProcessorCount;
        int totalPayloads = Environment.ProcessorCount * 3;
        using MessageBusProxy proxy = new();
        List<DummyConsumer> dummyConsumers = [];
        Random random = new();
        for (int i = 0; i < totalConsumers; i++)
        {
            DummyConsumer dummyConsumer = new(async _ => await Task.Delay(random.Next(40, 80)));
            dummyConsumers.Add(dummyConsumer);
        }

        using AsynchronousMessageBus asynchronousMessageBus = new(
            dummyConsumers.ToArray(),
            new CTRLPlusCCancellationTokenSource(),
            new SystemTask(),
            new NopLoggerFactory(),
            new SystemEnvironment());
        await asynchronousMessageBus.InitAsync();

        proxy.SetBuiltMessageBus(asynchronousMessageBus);

        DummyConsumer.DummyProducer producer = new();
        await Task.WhenAll(Enumerable.Range(1, totalPayloads)
            .Select(i => Task.Run(async () => await proxy.PublishAsync(producer, new DummyConsumer.DummyData() { Data = i }))).ToArray());

        await proxy.DrainDataAsync();

        Assert.AreEqual(totalConsumers, dummyConsumers.Count);
        foreach (DummyConsumer consumer in dummyConsumers)
        {
            Assert.AreEqual(totalPayloads, consumer.DummyDataList.Count);

            int i = 1;
            foreach (DummyConsumer.DummyData payload in consumer.DummyDataList.OrderBy(x => x.Data))
            {
                Assert.AreEqual(i, payload.Data);
                i++;
            }
        }
    }

    private sealed class NopLoggerFactory : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName) => new NopLogger();
    }

    private sealed class DummyConsumer : IDataConsumer
    {
        private readonly Func<DummyData, Task> _action;

        public DummyConsumer()
        {
            _action = _ => Task.CompletedTask;
        }

        public DummyConsumer(Func<DummyData, Task> action)
        {
            _action = action;
        }

        public List<DummyData> DummyDataList { get; } = [];

        public Type[] DataTypesConsumed => new[] { typeof(DummyData) };

        public string Uid => nameof(DummyConsumer);

        public string Version => AppVersion.DefaultSemVer;

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
            public Type[] DataTypesProduced => new[] { typeof(DummyData) };

            public string Uid => nameof(DummyProducer);

            public string Version => AppVersion.DefaultSemVer;

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

        public LoopConsumerA(IMessageBus messageBus)
        {
            _messageBus = messageBus;
        }

        public Type[] DataTypesConsumed => new[] { typeof(LoopDataB) };

        public string Uid => nameof(LoopConsumerA);

        public string Version => "1.0.0";

        public string DisplayName => string.Empty;

        public string Description => string.Empty;

        public Type[] DataTypesProduced => new[] { typeof(LoopDataA) };

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

        public ConsumerB(IMessageBus messageBus)
        {
            _messageBus = messageBus;
        }

        public Type[] DataTypesConsumed => new[] { typeof(LoopDataA) };

        public string Uid => nameof(LoopConsumerA);

        public string Version => "1.0.0";

        public string DisplayName => string.Empty;

        public string Description => string.Empty;

        public Type[] DataTypesProduced => new[] { typeof(LoopDataB) };

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
        public Consumer(IMessageBus messageBus, string id)
        {
            Uid = id;
        }

        public List<IData> ConsumedData { get; } = [];

        public Type[] DataTypesConsumed => new[] { typeof(Data) };

        public string Uid { get; set; }

        public string Version => "1.0.0";

        public string DisplayName => string.Empty;

        public string Description => string.Empty;

        public Type[] DataTypesProduced => new[] { typeof(Data) };

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

        public InvalidTypePublished(IMessageBus messageBus)
        {
            _messageBus = messageBus;
        }

        public ManualResetEvent Published { get; set; } = new(false);

        public Type[] DataTypesConsumed => new[] { typeof(ValidDataToProduce) };

        public string Uid => nameof(InvalidTypePublished);

        public Type[] DataTypesProduced => new[] { typeof(ValidDataToProduce) };

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
}
