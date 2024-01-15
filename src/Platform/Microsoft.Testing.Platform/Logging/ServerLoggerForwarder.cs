// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
#else
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
#endif
using System.Globalization;

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform.Logging;

/// <summary>
/// This logger forwards the messages back to the server host so that they're
/// logged over RPC back to the client.
/// </summary>
internal sealed class ServerLoggerForwarder : ILogger, IDisposable
#if NETCOREAPP
#pragma warning disable SA1001 // Commas should be spaced correctly
    , IAsyncDisposable
#pragma warning restore SA1001 // Commas should be spaced correctly
#endif
{
    private readonly LogLevel _logLevel;
    private readonly Task _logLoop;
    private readonly IServerTestHost? _serverTestHost;
#if NETCOREAPP
    private readonly Channel<ServerLogMessage>? _channel;
#else
    private readonly BlockingCollection<ServerLogMessage>? _asyncLogs;
#endif

    private bool _isDisposed;

    public ServerLoggerForwarder(
        LogLevel logLevel,
        ITask task,
        IServerTestHost? serverTestHost)
    {
        _logLevel = logLevel;
        _serverTestHost = serverTestHost;
#if NETCOREAPP
        _channel = Channel.CreateUnbounded<ServerLogMessage>(new UnboundedChannelOptions()
        {
            // We process only 1 data at a time
            SingleReader = true,

            // We don't know how many threads will call the Log method
            SingleWriter = false,

            // We want to unlink the caller from the consumer
            AllowSynchronousContinuations = false,
        });
#else
        _asyncLogs = [];
#endif
        _logLoop = task.Run(WriteLogMessageAsync, CancellationToken.None);
    }

    private async Task WriteLogMessageAsync()
    {
#if NETCOREAPP
        // We do this check out of the try because we want to crash the process if the _channel is null.
        ApplicationStateGuard.Ensure(_channel is not null);

        // We don't need cancellation token because the task will be stopped when the Channel is completed thanks to the call to Complete() inside the Dispose method.
        while (await _channel.Reader.WaitToReadAsync())
        {
            await PushServerLogMessageToTheMessageBusAsync(await _channel.Reader.ReadAsync());
        }
#else
        ApplicationStateGuard.Ensure(_asyncLogs is not null);

        // We don't need cancellation token because the task will be stopped when the BlockingCollection is completed thanks to the call to CompleteAdding()
        // inside the Dispose method.
        foreach (ServerLogMessage message in _asyncLogs.GetConsumingEnumerable())
        {
            await PushServerLogMessageToTheMessageBusAsync(message);
        }
#endif
    }

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _logLevel;

    public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        string message = formatter(state, exception);
        var logMessage = new ServerLogMessage(logLevel, message);
        EnsureAsyncLogObjectsAreNotNull();
#if NETCOREAPP
        if (!_channel.Writer.TryWrite(logMessage))
        {
            throw new InvalidOperationException("Failed to write the log to the channel");
        }
#else
        _asyncLogs.Add(logMessage);
#endif
    }

    public async Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        string message = formatter(state, exception);
        var logMessage = new ServerLogMessage(logLevel, message);
        await PushServerLogMessageToTheMessageBusAsync(logMessage);
    }

    private async Task PushServerLogMessageToTheMessageBusAsync(ServerLogMessage logMessage)
    {
        if (_serverTestHost?.IsInitialized == true)
        {
            await _serverTestHost.PushDataAsync(logMessage);
        }
    }

#if NETCOREAPP
    [MemberNotNull(nameof(_channel), nameof(_logLoop))]
    private void EnsureAsyncLogObjectsAreNotNull()
    {
        ApplicationStateGuard.Ensure(_channel is not null);
        ApplicationStateGuard.Ensure(_logLoop is not null);
    }
#else
    [MemberNotNull(nameof(_asyncLogs), nameof(_logLoop))]
    private void EnsureAsyncLogObjectsAreNotNull()
    {
        ApplicationStateGuard.Ensure(_asyncLogs is not null);
        ApplicationStateGuard.Ensure(_logLoop is not null);
    }
#endif

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        EnsureAsyncLogObjectsAreNotNull();
#if NETCOREAPP
        // Wait for all logs to be written
        var result = _channel.Writer.TryComplete();
        if (!_logLoop.Wait(TimeoutHelper.DefaultHangTimeSpanTimeout))
        {
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.TimeoutFlushingLogsErrorMessage, TimeoutHelper.DefaultHangTimeoutSeconds));
        }
#else
        // Wait for all logs to be written
        _asyncLogs.CompleteAdding();
        if (!_logLoop.Wait(TimeoutHelper.DefaultHangTimeSpanTimeout))
        {
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.TimeoutFlushingLogsErrorMessage, TimeoutHelper.DefaultHangTimeoutSeconds));
        }
#endif
        _isDisposed = true;
    }

#if NETCOREAPP
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        EnsureAsyncLogObjectsAreNotNull();

        // Wait for all logs to be written
        _channel.Writer.TryComplete();
        await _logLoop.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
        _isDisposed = true;
    }
#endif
}
