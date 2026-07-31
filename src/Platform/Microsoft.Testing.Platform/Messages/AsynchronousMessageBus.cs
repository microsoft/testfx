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
    private TimeSpan _canceledShutdownTimeout = ShutdownTimeouts.DefaultCanceledConsumerCompletion;
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
        _canceledShutdownTimeout = canceledShutdownTimeout;
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
        CancellationToken cancellationToken = _testApplicationCancellationTokenSource.CancellationToken;
        Task completeAll = CompleteAllProcessorsAsync();

        if (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Normal shutdown: every consumer gets as long as it needs to flush its final results. The
                // token is observed so that a cancellation arriving mid-wait downgrades to the bounded wait.
                await completeAll.WaitAsync(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException oc) when (oc.CancellationToken == cancellationToken)
            {
                // The run was canceled while we were waiting. Fall through to the bounded wait, which returns
                // immediately if the work happened to finish in the meantime.
            }
        }

        try
        {
            // The budget bounds the shutdown as a whole rather than each consumer. The processors are completed
            // sequentially, so a purely per-consumer bound would let N uncooperative consumers stretch an abort
            // to N times the budget - which is the hang this bound exists to prevent.
            await completeAll.WaitAsync(_canceledShutdownTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // WaitAsync also surfaces the task's own failure with this exception type, and the per-processor
            // budget expires at the same moment as ours, so we must not decide from a racy IsCompleted check in
            // an exception filter. Re-await instead: that rethrows a genuine consumer failure and returns
            // quietly when the work simply finished as we gave up.
            if (completeAll.IsCompleted)
            {
                await completeAll.ConfigureAwait(false);
                return;
            }

            // We stop waiting so the abort can complete. Whatever did not finish keeps reporting
            // IsConsumerRunning, so the platform leaves those consumers undisposed instead of racing them, and
            // we observe the abandoned task so its failure cannot resurface as an unobserved task exception.
            _ = completeAll.ContinueWith(
                static task => _ = task.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private async Task CompleteAllProcessorsAsync()
    {
        // Start every completion up front. CompleteAddingAsync both signals the processor (completing its
        // channel) and waits for the loop, and the signalling half runs synchronously before its first await,
        // so invoking them all here tells every consumer to stop before we block on any of them. Awaiting them
        // one at a time instead would let the first uncooperative consumer burn the whole shared budget while
        // the later pumps had not even been told to finish: they would still be parked on a non-cancelable
        // read, and would then be reported as still running and skipped by disposal despite being perfectly
        // cooperative.
        var completions = new Task[_distinctProcessors.Length];
        for (int i = 0; i < _distinctProcessors.Length; i++)
        {
            completions[i] = CompleteProcessorAsync(_distinctProcessors[i]);
        }

        // Await every one even if some fault, otherwise a single misbehaving consumer would leave the
        // remaining consumer loops running while they are being disposed.
        List<Exception>? exceptions = null;
        foreach (Task completion in completions)
        {
            try
            {
                await completion.ConfigureAwait(false);
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

    private async Task CompleteProcessorAsync(IAsyncConsumerDataProcessor processor)
    {
        using (_shutdownProgressReporter?.Track(processor.DataConsumer.Uid, processor.DataConsumer.DisplayName, nameof(IAsyncConsumerDataProcessor.CompleteAddingAsync)))
        {
            await processor.CompleteAddingAsync().ConfigureAwait(false);
        }
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
