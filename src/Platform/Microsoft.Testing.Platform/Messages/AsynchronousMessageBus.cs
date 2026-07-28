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
    // Default maximum number of drain rounds before we consider that a publisher/consumer cycle
    // exists and we throw to surface the bug rather than spin forever. Can be overridden via the
    // TESTINGPLATFORM_MESSAGEBUS_DRAINDATA_ATTEMPTS environment variable.
    private const int DefaultMaxDrainAttempts = 5;

    private readonly ITask _task;
    private readonly IEnvironment _environment;
    private readonly ILogger<AsynchronousMessageBus> _logger;
    private readonly bool _isTraceLoggingEnabled;
    private readonly Dictionary<IDataConsumer, IAsyncConsumerDataProcessor> _consumerProcessor = [];
    private readonly Dictionary<Type, List<IAsyncConsumerDataProcessor>> _dataTypeConsumers = [];
    private readonly IDataConsumer[] _dataConsumers;
    private readonly ITestApplicationCancellationTokenSource _testApplicationCancellationTokenSource;
    private readonly IShutdownProgressReporter? _shutdownProgressReporter;
    private IAsyncConsumerDataProcessor[] _distinctProcessors = [];
    private long[] _drainLastReceived = [];
    private bool _disabled;

    public AsynchronousMessageBus(
        IDataConsumer[] dataConsumers,
        ITestApplicationCancellationTokenSource testApplicationCancellationTokenSource,
        ITask task,
        ILoggerFactory loggerFactory,
        IEnvironment environment)
        : this(dataConsumers, testApplicationCancellationTokenSource, task, loggerFactory, environment, shutdownProgressReporter: null)
    {
    }

    public AsynchronousMessageBus(
        IDataConsumer[] dataConsumers,
        ITestApplicationCancellationTokenSource testApplicationCancellationTokenSource,
        ITask task,
        ILoggerFactory loggerFactory,
        IEnvironment environment,
        IShutdownProgressReporter? shutdownProgressReporter)
    {
        _dataConsumers = dataConsumers;
        _testApplicationCancellationTokenSource = testApplicationCancellationTokenSource;
        _task = task;
        _environment = environment;
        _shutdownProgressReporter = shutdownProgressReporter;
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
                    // A consumer that implements the IBlockingDataConsumer marker interface must consume the data
                    // inline (the publisher blocks until consumption completes) instead of going through the
                    // asynchronous background processing loop.
                    //
                    // On single-threaded wasm runtimes (browser-wasm / wasi-wasm) there is no thread pool, so the
                    // AsyncConsumerDataProcessor background loop (started via Task.Run) would never run and the
                    // drain would hang. Force inline consumption for every consumer in that case.
                    asyncMultiProducerMultiConsumerDataProcessor = consumer is IBlockingDataConsumer || !RuntimeFeatureHelper.IsMultiThreaded
                        ? new BlockingConsumerDataProcessor(consumer, _testApplicationCancellationTokenSource.CancellationToken)
                        : new AsyncConsumerDataProcessor(consumer, _task, _testApplicationCancellationTokenSource.CancellationToken);
                    _consumerProcessor.Add(consumer, asyncMultiProducerMultiConsumerDataProcessor);
                }

                asyncMultiProducerMultiConsumerDataProcessors.Add(asyncMultiProducerMultiConsumerDataProcessor);
            }
        }

        _distinctProcessors = [.. _consumerProcessor.Values];
        _drainLastReceived = new long[_distinctProcessors.Length];
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
        // We keep draining until no processor has received new items between rounds. If we still
        // keep receiving new payloads after `maxAttempts`, we consider that a publisher/consumer
        // cycle exists and surface it as an error. The limit can be overridden via the
        // TESTINGPLATFORM_MESSAGEBUS_DRAINDATA_ATTEMPTS environment variable.
        string? customAttempts = _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_MESSAGEBUS_DRAINDATA_ATTEMPTS);
        if (!int.TryParse(customAttempts, NumberStyles.Integer, CultureInfo.InvariantCulture, out int maxAttempts) || maxAttempts <= 0)
        {
            maxAttempts = DefaultMaxDrainAttempts;
        }

        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < _distinctProcessors.Length; i++)
        {
            _drainLastReceived[i] = _distinctProcessors[i].ReceivedCount;
        }

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (_testApplicationCancellationTokenSource.CancellationToken.IsCancellationRequested)
            {
                return;
            }

            for (int i = 0; i < _distinctProcessors.Length; i++)
            {
                IAsyncConsumerDataProcessor processor = _distinctProcessors[i];
                using (_shutdownProgressReporter?.Track(processor.DataConsumer.Uid, processor.DataConsumer.DisplayName, nameof(IAsyncConsumerDataProcessor.DrainDataAsync)))
                {
                    await processor.DrainDataAsync().ConfigureAwait(false);
                }
            }

            bool anyNewlyReceived = false;
            for (int i = 0; i < _distinctProcessors.Length; i++)
            {
                long currentReceived = _distinctProcessors[i].ReceivedCount;
                if (currentReceived != _drainLastReceived[i])
                {
                    _drainLastReceived[i] = currentReceived;
                    anyNewlyReceived = true;
                }
            }

            if (!anyNewlyReceived)
            {
                return;
            }
        }

        StringBuilder builder = new();
        builder.Append(CultureInfo.InvariantCulture, $"Publisher/Consumer loop detected during the drain after {stopwatch.Elapsed}.");
        foreach (IAsyncConsumerDataProcessor processor in _distinctProcessors)
        {
            builder.AppendLine();
            builder.Append(CultureInfo.InvariantCulture, $"Consumer '{processor.DataConsumer}' payload received {processor.ReceivedCount}.");
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

        foreach (IAsyncConsumerDataProcessor processor in _distinctProcessors)
        {
            await processor.CompleteAddingAsync().ConfigureAwait(false);
        }
    }

    public override void Dispose()
    {
        foreach (IAsyncConsumerDataProcessor processor in _distinctProcessors)
        {
            processor.Dispose();
        }

        _distinctProcessors = [];
        _drainLastReceived = [];
        _consumerProcessor.Clear();
        _dataTypeConsumers.Clear();
    }
}
