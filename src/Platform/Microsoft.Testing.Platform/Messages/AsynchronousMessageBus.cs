// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Text;

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Messages;

internal sealed class AsynchronousMessageBus : BaseMessageBus, IMessageBus, IDisposable
{
    // This is an arbitrary number of attempts to drain the message bus.
    // The number of attempts is configurable via the environment variable TESTINGPLATFORM_MESSAGEBUS_DRAINDATA_ATTEMPTS.
    private const int DefaultDrainAttempt = 5;
    private readonly ITask _task;
    private readonly IEnvironment _environment;
    private readonly ILogger<AsynchronousMessageBus> _logger;
    private readonly bool _isTraceLoggingEnabled;
    private readonly Dictionary<IDataConsumer, AsyncConsumerDataProcessor> _consumerProcessor = [];
    private readonly Dictionary<Type, List<AsyncConsumerDataProcessor>> _dataTypeConsumers = [];
    private readonly IDataConsumer[] _dataConsumers;
    private readonly ITestApplicationCancellationTokenSource _testApplicationCancellationTokenSource;
    private bool _disabled;

    public AsynchronousMessageBus(
        IDataConsumer[] dataConsumers,
        ITestApplicationCancellationTokenSource testApplicationCancellationTokenSource,
        ITask task,
        ILoggerFactory loggerFactory,
        IEnvironment environment)
    {
        _dataConsumers = dataConsumers;
        _testApplicationCancellationTokenSource = testApplicationCancellationTokenSource;
        _task = task;
        _environment = environment;
        _logger = loggerFactory.CreateLogger<AsynchronousMessageBus>();
        _isTraceLoggingEnabled = _logger.IsEnabled(LogLevel.Trace);
    }

    public override IDataConsumer[] DataConsumerServices
        => _dataConsumers;

    public override async Task InitAsync() => await BuildConsumerProducersAsync();

    private async Task BuildConsumerProducersAsync()
    {
        foreach (IDataConsumer consumer in _dataConsumers)
        {
            if (!await consumer.IsEnabledAsync())
            {
                throw new InvalidOperationException($"Unexpected disabled IDataConsumer '{consumer}'");
            }

            foreach (Type dataType in consumer.DataTypesConsumed)
            {
                if (!_dataTypeConsumers.TryGetValue(dataType, out List<AsyncConsumerDataProcessor>? asyncMultiProducerMultiConsumerDataProcessors))
                {
                    asyncMultiProducerMultiConsumerDataProcessors = [];
                    _dataTypeConsumers.Add(dataType, asyncMultiProducerMultiConsumerDataProcessors);
                }

                if (asyncMultiProducerMultiConsumerDataProcessors.Any(c => c.DataConsumer == consumer))
                {
                    throw new InvalidOperationException($"Consumer registered two time for data type '{dataType}', consumer '{consumer}'");
                }

                if (!_consumerProcessor.TryGetValue(consumer, out AsyncConsumerDataProcessor? asyncMultiProducerMultiConsumerDataProcessor))
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
            await LogDataAsync(dataProducer, data);
        }

        Type dataType = data.GetType();
        if (Array.IndexOf(dataProducer.DataTypesProduced, dataType) < 0)
        {
            throw new InvalidOperationException($"Unexpected data type '{dataType}' produced by '{dataProducer.Uid}'");
        }

        if (!_dataTypeConsumers.TryGetValue(dataType, out List<AsyncConsumerDataProcessor>? values))
        {
            return;
        }

        for (int i = 0; i < values.Count; i++)
        {
            await values[i].PublishAsync(dataProducer, data);
        }
    }

    private async Task LogDataAsync(IDataProducer dataProducer, IData data)
    {
        StringBuilder messageBuilder = new();
        messageBuilder.AppendLine(
            CultureInfo.InvariantCulture,
            $"The producer '{dataProducer.DisplayName}' (ID: {dataProducer.Uid}) pushed data:");
        messageBuilder.AppendLine(data.ToString());

        await _logger.LogTraceAsync(messageBuilder.ToString());
    }

    public override async Task DrainDataAsync()
    {
        Dictionary<AsyncConsumerDataProcessor, long> consumerToDrain = [];
        bool anotherRound = true;
        string? customAttempts = _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_MESSAGEBUS_DRAINDATA_ATTEMPTS);
        if (!int.TryParse(customAttempts, out int totalNumberOfDrainAttempt))
        {
            totalNumberOfDrainAttempt = DefaultDrainAttempt;
        }

        var stopwatch = Stopwatch.StartNew();
        CancellationToken cancellationToken = _testApplicationCancellationTokenSource.CancellationToken;
        while (anotherRound)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (totalNumberOfDrainAttempt == 0)
            {
                StringBuilder builder = new();
                builder.Append(CultureInfo.InvariantCulture, $"Publisher/Consumer loop detected during the drain after {stopwatch.Elapsed}.\n{builder}");

                foreach (KeyValuePair<AsyncConsumerDataProcessor, long> keyValuePair in consumerToDrain)
                {
                    builder.AppendLine(CultureInfo.InvariantCulture, $"Consumer '{keyValuePair.Key.DataConsumer}' payload received {keyValuePair.Value}.");
                }

                throw new InvalidOperationException(builder.ToString());
            }

            totalNumberOfDrainAttempt--;
            anotherRound = false;
            foreach (KeyValuePair<Type, List<AsyncConsumerDataProcessor>> dataTypeConsumer in _dataTypeConsumers)
            {
                foreach (AsyncConsumerDataProcessor asyncMultiProducerMultiConsumerDataProcessor in dataTypeConsumer.Value)
                {
                    if (!consumerToDrain.TryGetValue(asyncMultiProducerMultiConsumerDataProcessor, out long _))
                    {
                        consumerToDrain.Add(asyncMultiProducerMultiConsumerDataProcessor, 0);
                    }

                    long totalPayloadReceived = await asyncMultiProducerMultiConsumerDataProcessor.DrainDataAsync();
                    if (consumerToDrain[asyncMultiProducerMultiConsumerDataProcessor] != totalPayloadReceived)
                    {
                        consumerToDrain[asyncMultiProducerMultiConsumerDataProcessor] = totalPayloadReceived;
                        anotherRound = true;
                    }
                }
            }
        }
    }

    public override async Task DisableAsync()
    {
        if (_disabled)
        {
            throw new InvalidOperationException("AsynchronousMessageBus already disabled");
        }

        _disabled = true;

        foreach (KeyValuePair<Type, List<AsyncConsumerDataProcessor>> dataTypeConsumer in _dataTypeConsumers)
        {
            foreach (AsyncConsumerDataProcessor asyncMultiProducerMultiConsumerDataProcessor in dataTypeConsumer.Value)
            {
                await asyncMultiProducerMultiConsumerDataProcessor.CompleteAddingAsync();
            }
        }
    }

    public override void Dispose()
    {
        foreach (KeyValuePair<Type, List<AsyncConsumerDataProcessor>> dataTypeConsumer in _dataTypeConsumers)
        {
            foreach (AsyncConsumerDataProcessor asyncMultiProducerMultiConsumerDataProcessor in dataTypeConsumer.Value)
            {
                asyncMultiProducerMultiConsumerDataProcessor.Dispose();
            }
        }

        _consumerProcessor.Clear();
        _dataTypeConsumers.Clear();
    }
}
