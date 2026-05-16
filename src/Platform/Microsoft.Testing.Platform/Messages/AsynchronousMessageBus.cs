// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Messages;

internal sealed class AsynchronousMessageBus : BaseMessageBus, IMessageBus, IDisposable
{
    // Maximum number of drain rounds before we consider that a publisher/consumer cycle exists
    // and we throw to surface the bug rather than spin forever.
    private const int MaxDrainAttempts = 5;

    private readonly ITask _task;
    private readonly ILogger<AsynchronousMessageBus> _logger;
    private readonly bool _isTraceLoggingEnabled;
    private readonly Dictionary<IDataConsumer, IAsyncConsumerDataProcessor> _consumerProcessor = [];
    private readonly Dictionary<Type, List<IAsyncConsumerDataProcessor>> _dataTypeConsumers = [];
    private readonly IDataConsumer[] _dataConsumers;
    private readonly ITestApplicationCancellationTokenSource _testApplicationCancellationTokenSource;
    private bool _disabled;

    public AsynchronousMessageBus(
        IDataConsumer[] dataConsumers,
        ITestApplicationCancellationTokenSource testApplicationCancellationTokenSource,
        ITask task,
        ILoggerFactory loggerFactory)
    {
        _dataConsumers = dataConsumers;
        _testApplicationCancellationTokenSource = testApplicationCancellationTokenSource;
        _task = task;
        _logger = loggerFactory.CreateLogger<AsynchronousMessageBus>();
        _isTraceLoggingEnabled = _logger.IsEnabled(LogLevel.Trace);
    }

    public override IDataConsumer[] DataConsumerServices
        => _dataConsumers;

    public override async Task InitAsync()
    {
        foreach (IDataConsumer consumer in _dataConsumers)
        {
            if (!await consumer.IsEnabledAsync().ConfigureAwait(false))
            {
                throw new InvalidOperationException($"Unexpected disabled IDataConsumer '{consumer}'");
            }

            foreach (Type dataType in consumer.DataTypesConsumed)
            {
                if (!_dataTypeConsumers.TryGetValue(dataType, out List<IAsyncConsumerDataProcessor>? asyncMultiProducerMultiConsumerDataProcessors))
                {
                    asyncMultiProducerMultiConsumerDataProcessors = [];
                    _dataTypeConsumers.Add(dataType, asyncMultiProducerMultiConsumerDataProcessors);
                }

                if (asyncMultiProducerMultiConsumerDataProcessors.Any(c => c.DataConsumer == consumer))
                {
                    throw new InvalidOperationException($"Consumer registered two time for data type '{dataType}', consumer '{consumer}'");
                }

                if (!_consumerProcessor.TryGetValue(consumer, out IAsyncConsumerDataProcessor? asyncMultiProducerMultiConsumerDataProcessor))
                {
                    asyncMultiProducerMultiConsumerDataProcessor = new AsyncConsumerDataProcessor(consumer, _task, _testApplicationCancellationTokenSource.CancellationToken);
                    _consumerProcessor.Add(consumer, asyncMultiProducerMultiConsumerDataProcessor);
                }

                asyncMultiProducerMultiConsumerDataProcessors.Add(asyncMultiProducerMultiConsumerDataProcessor);
            }
        }
    }

    public override async Task PublishAsync(IDataProducer dataProducer, IData data)
    {
        if (_disabled)
        {
            throw new InvalidOperationException("The message bus has been drained and is no longer usable.");
        }

        if (_dataTypeConsumers is null)
        {
            throw new InvalidOperationException("The message bus has not been built yet.");
        }

        if (_testApplicationCancellationTokenSource.CancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (_isTraceLoggingEnabled)
        {
            await LogDataAsync(dataProducer, data).ConfigureAwait(false);
        }

        Type dataType = data.GetType();
        if (Array.IndexOf(dataProducer.DataTypesProduced, dataType) < 0)
        {
            throw new InvalidOperationException($"Unexpected data type '{dataType}' produced by '{dataProducer.Uid}'");
        }

        if (!_dataTypeConsumers.TryGetValue(dataType, out List<IAsyncConsumerDataProcessor>? values))
        {
            return;
        }

        for (int i = 0; i < values.Count; i++)
        {
            await values[i].PublishAsync(dataProducer, data).ConfigureAwait(false);
        }
    }

    private async Task LogDataAsync(IDataProducer dataProducer, IData data)
    {
        StringBuilder messageBuilder = new();
        messageBuilder.AppendLine(
            CultureInfo.InvariantCulture,
            $"The producer '{dataProducer.DisplayName}' (ID: {dataProducer.Uid}) pushed data:");
        messageBuilder.AppendLine(data.ToString());

        await _logger.LogTraceAsync(messageBuilder.ToString()).ConfigureAwait(false);
    }

    public override async Task DrainDataAsync()
    {
        // Iterate the distinct processors (a consumer that subscribes to multiple data types
        // shares a single processor and we don't want to drain it more than once per round).
        // We keep draining until every processor reports that no data was processed during
        // the current round. If we still keep processing data after `MaxDrainAttempts`, we
        // consider that a publisher/consumer cycle exists and surface it as an error.
        var stopwatch = Stopwatch.StartNew();
        for (int attempt = 0; attempt < MaxDrainAttempts; attempt++)
        {
            if (_testApplicationCancellationTokenSource.CancellationToken.IsCancellationRequested)
            {
                return;
            }

            bool anyProcessed = false;
            foreach (IAsyncConsumerDataProcessor asyncMultiProducerMultiConsumerDataProcessor in _consumerProcessor.Values)
            {
                if (await asyncMultiProducerMultiConsumerDataProcessor.DrainDataAsync().ConfigureAwait(false))
                {
                    anyProcessed = true;
                }
            }

            if (!anyProcessed)
            {
                return;
            }
        }

        StringBuilder builder = new();
        builder.Append(CultureInfo.InvariantCulture, $"Publisher/Consumer loop detected during the drain after {stopwatch.Elapsed}.");
        foreach (IAsyncConsumerDataProcessor processor in _consumerProcessor.Values)
        {
            builder.AppendLine();
            builder.Append(CultureInfo.InvariantCulture, $"Consumer '{processor.DataConsumer}'.");
        }

        throw new InvalidOperationException(builder.ToString());
    }

    public override async Task DisableAsync()
    {
        if (_disabled)
        {
            throw new InvalidOperationException("AsynchronousMessageBus already disabled");
        }

        _disabled = true;

        foreach (List<IAsyncConsumerDataProcessor> dataProcessors in _dataTypeConsumers.Values)
        {
            foreach (IAsyncConsumerDataProcessor asyncMultiProducerMultiConsumerDataProcessor in dataProcessors)
            {
                await asyncMultiProducerMultiConsumerDataProcessor.CompleteAddingAsync().ConfigureAwait(false);
            }
        }
    }

    public override void Dispose()
    {
        foreach (List<IAsyncConsumerDataProcessor> dataProcessors in _dataTypeConsumers.Values)
        {
            foreach (IAsyncConsumerDataProcessor asyncMultiProducerMultiConsumerDataProcessor in dataProcessors)
            {
                asyncMultiProducerMultiConsumerDataProcessor.Dispose();
            }
        }

        _consumerProcessor.Clear();
        _dataTypeConsumers.Clear();
    }
}
