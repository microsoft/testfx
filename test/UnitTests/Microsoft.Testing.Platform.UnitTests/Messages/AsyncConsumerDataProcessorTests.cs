// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETCOREAPP
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class AsyncConsumerDataProcessorTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task DrainDataAsync_WhenConsumerThrowsException_ShouldRethrowException()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Test exception");
        var consumer = new ThrowingConsumer(expectedException);
        var producer = new DummyProducer();
        using var cts = new CancellationTokenSource();
        var processor = new AsyncConsumerDataProcessor(consumer, new SystemTask(), cts.Token);

        // Act - Publish data that will cause the consumer to throw
        await processor.PublishAsync(producer, new DummyData());

        // Wait a bit to ensure the consume task has started processing
        await Task.Delay(50);

        // Assert - DrainDataAsync should rethrow the exception from the consumer
        var thrownException = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await processor.DrainDataAsync());

        Assert.AreEqual(expectedException.Message, thrownException.Message);
    }

    [TestMethod]
    public async Task DrainDataAsync_WhenExceptionOccursBeforeIncrementingProcessedCount_ShouldStillRethrowException()
    {
        // This test verifies the race condition scenario described in the code comments:
        // If an exception occurs in ConsumeAsync, and DrainDataAsync checks the payload counts
        // before _totalPayloadProcessed is incremented (in the finally block), the exception
        // should still be properly detected and rethrown via _consumerState.

        // Arrange
        var expectedException = new InvalidOperationException("Race condition test exception");
        var slowConsumer = new SlowThrowingConsumer(expectedException, delayBeforeThrow: 10);
        var producer = new DummyProducer();
        using var cts = new CancellationTokenSource();
        var processor = new AsyncConsumerDataProcessor(slowConsumer, new SystemTask(), cts.Token);

        // Act - Publish data that will cause the consumer to throw after a small delay
        await processor.PublishAsync(producer, new DummyData());

        // Call DrainDataAsync immediately, which might check payload counts
        // before the exception is thrown and _totalPayloadProcessed is incremented
        var thrownException = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await processor.DrainDataAsync());

        // Assert
        Assert.AreEqual(expectedException.Message, thrownException.Message);
    }

    [TestMethod]
    public async Task DrainDataAsync_WhenMultipleItemsPublishedAndOneThrows_ShouldRethrowException()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Test exception on second item");
        var consumer = new ConditionalThrowingConsumer(expectedException, throwOnItemNumber: 2);
        var producer = new DummyProducer();
        using var cts = new CancellationTokenSource();
        var processor = new AsyncConsumerDataProcessor(consumer, new SystemTask(), cts.Token);

        // Act - Publish multiple items, second one will throw
        await processor.PublishAsync(producer, new DummyData());
        await processor.PublishAsync(producer, new DummyData());
        await processor.PublishAsync(producer, new DummyData());

        // Wait a bit to ensure processing has started
        await Task.Delay(100);

        // Assert - DrainDataAsync should rethrow the exception
        var thrownException = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await processor.DrainDataAsync());

        Assert.AreEqual(expectedException.Message, thrownException.Message);
    }

    [TestMethod]
    public async Task DrainDataAsync_WhenNoException_ShouldReturnTotalPayloadReceived()
    {
        // Arrange
        var consumer = new SimpleConsumer();
        var producer = new DummyProducer();
        using var cts = new CancellationTokenSource();
        var processor = new AsyncConsumerDataProcessor(consumer, new SystemTask(), cts.Token);

        // Act - Publish multiple items
        await processor.PublishAsync(producer, new DummyData());
        await processor.PublishAsync(producer, new DummyData());
        await processor.PublishAsync(producer, new DummyData());

        long totalProcessed = await processor.DrainDataAsync();

        // Assert
        Assert.AreEqual(3L, totalProcessed);
        Assert.AreEqual(3, consumer.ConsumedCount);
    }

    private sealed class DummyData : IData
    {
        public string DisplayName => "DummyData";
        public string? Description => "DummyData";
    }

    private sealed class DummyProducer : IDataProducer
    {
        public Type[] DataTypesProduced => [typeof(DummyData)];
        public string Uid => nameof(DummyProducer);
        public string Version => AppVersion.DefaultSemVer;
        public string DisplayName => nameof(DummyProducer);
        public string Description => nameof(DummyProducer);
        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }

    private sealed class ThrowingConsumer : IDataConsumer
    {
        private readonly Exception _exceptionToThrow;

        public ThrowingConsumer(Exception exceptionToThrow)
        {
            _exceptionToThrow = exceptionToThrow;
        }

        public Type[] DataTypesConsumed => [typeof(DummyData)];
        public string Uid => nameof(ThrowingConsumer);
        public string Version => AppVersion.DefaultSemVer;
        public string DisplayName => nameof(ThrowingConsumer);
        public string Description => nameof(ThrowingConsumer);
        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
        {
            throw _exceptionToThrow;
        }
    }

    private sealed class SlowThrowingConsumer : IDataConsumer
    {
        private readonly Exception _exceptionToThrow;
        private readonly int _delayBeforeThrow;

        public SlowThrowingConsumer(Exception exceptionToThrow, int delayBeforeThrow)
        {
            _exceptionToThrow = exceptionToThrow;
            _delayBeforeThrow = delayBeforeThrow;
        }

        public Type[] DataTypesConsumed => [typeof(DummyData)];
        public string Uid => nameof(SlowThrowingConsumer);
        public string Version => AppVersion.DefaultSemVer;
        public string DisplayName => nameof(SlowThrowingConsumer);
        public string Description => nameof(SlowThrowingConsumer);
        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
        {
            await Task.Delay(_delayBeforeThrow);
            throw _exceptionToThrow;
        }
    }

    private sealed class ConditionalThrowingConsumer : IDataConsumer
    {
        private readonly Exception _exceptionToThrow;
        private readonly int _throwOnItemNumber;
        private int _itemCount;

        public ConditionalThrowingConsumer(Exception exceptionToThrow, int throwOnItemNumber)
        {
            _exceptionToThrow = exceptionToThrow;
            _throwOnItemNumber = throwOnItemNumber;
        }

        public Type[] DataTypesConsumed => [typeof(DummyData)];
        public string Uid => nameof(ConditionalThrowingConsumer);
        public string Version => AppVersion.DefaultSemVer;
        public string DisplayName => nameof(ConditionalThrowingConsumer);
        public string Description => nameof(ConditionalThrowingConsumer);
        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
        {
            _itemCount++;
            if (_itemCount == _throwOnItemNumber)
            {
                throw _exceptionToThrow;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class SimpleConsumer : IDataConsumer
    {
        public int ConsumedCount { get; private set; }

        public Type[] DataTypesConsumed => [typeof(DummyData)];
        public string Uid => nameof(SimpleConsumer);
        public string Version => AppVersion.DefaultSemVer;
        public string DisplayName => nameof(SimpleConsumer);
        public string Description => nameof(SimpleConsumer);
        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
        {
            ConsumedCount++;
            return Task.CompletedTask;
        }
    }
}
#endif
