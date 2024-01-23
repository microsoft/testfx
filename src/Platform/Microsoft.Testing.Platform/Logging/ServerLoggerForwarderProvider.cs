// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Logging;

internal sealed class ServerLoggerForwarderProvider(LogLevel logLevel, IServiceProvider serviceProvider)
    : ILoggerProvider
{
    private readonly LogLevel _logLevel = logLevel;
    private readonly ServerLogMessageInMemoryStore _serverLogMessageInMemoryStore = new(logLevel);
    private ServerTestHost? _serverTestHost;

    public ILogger CreateLogger(string categoryName)
    {
        // If we don't have the server test host, we just log to the in-memory store.
        if (_serverTestHost is null)
        {
            return _serverLogMessageInMemoryStore;
        }
        else
        {
            ArgumentGuard.IsNotNull(_serverTestHost);
            return new ServerLoggerForwarder(_logLevel, serviceProvider.GetTask(), _serverTestHost);
        }
    }

    public async Task InitializeAsync(ServerTestHost serverTestHost)
    {
        _serverTestHost = serverTestHost;

        foreach (ServerLogMessage serverLogMessage in _serverLogMessageInMemoryStore.Values)
        {
            await _serverTestHost.PushDataAsync(serverLogMessage);
        }
    }
}

internal abstract class InMemoryStoreLogger<T> : ILogger
    where T : class
{
    private readonly LogLevel _logLevel;

    public InMemoryStoreLogger(LogLevel logLevel)
    {
        _logLevel = logLevel;
    }

    public ConcurrentBag<T> Values { get; private set; } = new();

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _logLevel;

    public abstract void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter);

    public abstract Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter);
}

internal sealed class ServerLogMessageInMemoryStore : InMemoryStoreLogger<ServerLogMessage>
{
    public ServerLogMessageInMemoryStore(LogLevel logLevel)
        : base(logLevel)
    {
    }

    public override void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        string message = formatter(state, exception);
        var logMessage = new ServerLogMessage(logLevel, message);

        Values.Add(logMessage);
    }

    public override Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return Task.CompletedTask;
        }

        string message = formatter(state, exception);
        var logMessage = new ServerLogMessage(logLevel, message);

        Values.Add(logMessage);

        return Task.CompletedTask;
    }
}
