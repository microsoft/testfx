// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Concurrent;

namespace Microsoft.Testing.Platform.Logging;

internal sealed class ServerLogMessageInMemoryStore(LogLevel logLevel) : ILogger, IEnumerable<ServerLogMessage>
{
    private readonly LogLevel _logLevel = logLevel;
    private
#if NETCOREAPP
    readonly
#endif
    ConcurrentBag<ServerLogMessage> _values = new();

    public IEnumerator<ServerLogMessage> GetEnumerator() => _values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _logLevel;

    public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        string message = formatter(state, exception);
        var logMessage = new ServerLogMessage(logLevel, message);

        _values.Add(logMessage);
    }

    public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return Task.CompletedTask;
        }

        string message = formatter(state, exception);
        var logMessage = new ServerLogMessage(logLevel, message);

        _values.Add(logMessage);

        return Task.CompletedTask;
    }

    public void Clean()
    {
#if NETCOREAPP
        _values.Clear();
#else
        _values = new();
#endif
    }
}
