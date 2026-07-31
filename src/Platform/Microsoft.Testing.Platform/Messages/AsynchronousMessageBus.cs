// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.ExceptionServices;

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
#pragma warning disable IDE0330 // Use 'System.Threading.Lock' - not available on all target frameworks of this project.
    private readonly object _disableLock = new();
#pragma warning restore IDE0330
    private IAsyncConsumerDataProcessor[] _distinctProcessors = [];
    private long[] _drainLastReceived = [];
    private IDataConsumer[]? _consumersRunningAtDispose;
    private Task? _disableTask;
    private volatile bool _disabled;

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

    public override IEnumerable<IDataConsumer> ConsumersStillRunning
        // Once disposed the processors are gone, so we replay the set latched at that moment. The platform can
        // walk a service provider more than once (each pass starts with its own "already disposed" list), and
        // every pass has to reach the same conclusion, otherwise a later one would dispose a consumer that an
        // earlier one deliberately spared.
        => _consumersRunningAtDispose ?? EnumerateRunningConsumers();

    private IEnumerable<IDataConsumer> EnumerateRunningConsumers()
    {
        foreach (IAsyncConsumerDataProcessor processor in _distinctProcessors)
        {
            if (processor.IsConsumerRunning)
            {
                yield return processor.DataConsumer;
            }
        }
    }

    public override async Task InitAsync()
    {
        try
        {
            await InitCoreAsync().ConfigureAwait(false);
        }
        catch
        {
            // The pumps of the processors we built before the failure are already running, and a bus whose
            // InitAsync threw never reaches SetBuiltMessageBus, so nothing would ever complete their channels
            // and they would park forever, rooting their consumers and everything queued for them.
            foreach (IAsyncConsumerDataProcessor processor in _consumerProcessor.Values)
            {
                processor.Dispose();
            }

            _consumerProcessor.Clear();
            _dataTypeConsumers.Clear();
            throw;
        }
    }

    private async Task InitCoreAsync()
    {
        TimeSpan canceledShutdownTimeout = ShutdownTimeouts.GetCanceledConsumerCompletion(_environment);
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
                        ? new BlockingConsumerDataProcessor(consumer, _testApplicationCancellationTokenSource.CancellationToken, canceledShutdownTimeout)
                        : new AsyncConsumerDataProcessor(consumer, _task, _testApplicationCancellationTokenSource.CancellationToken, canceledShutdownTimeout);
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
        // The cancellation check comes first on purpose. Once the run is being aborted the platform closes the
        // bus as part of the shutdown handshake, and teardown code that publishes at that point (an extension
        // flushing state from its Dispose, for example) must observe a silent no-op rather than a hard failure
        // that would mask the original cancellation.
        if (_testApplicationCancellationTokenSource.CancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (_disabled)
        {
            throw new InvalidOperationException("The message bus has been drained and is no longer usable.");
        }

        if (_dataTypeConsumers is null)
        {
            throw new InvalidOperationException("The message bus has not been built yet.");
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
        // Disabling is idempotent and every caller awaits the same completion. The regular session-end path
        // disables the bus, and the host disables it again as a safety net before disposing the services
        // (which is what guarantees the handshake also happens when the session was canceled or failed). A
        // later caller must observe the *completion* of the first call and not merely the fact that it
        // started, otherwise it would go on to dispose consumers whose loops are still running.
        Task disableTask;
        lock (_disableLock)
        {
            // Set synchronously, under the lock, so publishers stop as soon as anyone asks for the shutdown.
            _disabled = true;
            disableTask = _disableTask ??= DisableCoreAsync();
        }

        await disableTask.ConfigureAwait(false);
    }

    private async Task DisableCoreAsync()
    {
        // Complete every processor even if one of them faults, otherwise a single misbehaving consumer would
        // leave the remaining consumer loops running while they are being disposed.
        List<Exception>? exceptions = null;
        foreach (IAsyncConsumerDataProcessor processor in _distinctProcessors)
        {
            try
            {
                using (_shutdownProgressReporter?.Track(processor.DataConsumer.Uid, processor.DataConsumer.DisplayName, nameof(IAsyncConsumerDataProcessor.CompleteAddingAsync)))
                {
                    await processor.CompleteAddingAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                (exceptions ??= []).Add(ex);
            }
        }

        if (exceptions is null)
        {
            return;
        }

        if (exceptions.Count == 1)
        {
            // Rethrow the original exception with its stack intact, matching the previous fail-fast behavior.
            ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
        }

        throw new AggregateException(exceptions);
    }

    public override void Dispose()
    {
        // Latch the verdict before the processors go away, so a later disposal pass over the same service
        // provider still knows which consumers must not be touched. We deliberately never clear it: once we
        // have decided a consumer is unsafe to dispose we keep sparing it rather than re-checking a processor
        // we no longer own.
        _consumersRunningAtDispose ??= [.. EnumerateRunningConsumers()];

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
