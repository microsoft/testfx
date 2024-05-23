// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Concurrent;

using Microsoft.Testing.Platform.Hosts;

namespace Microsoft.Testing.Platform.Logging;

internal sealed class ServerLogMessageInMemoryStore(LogLevel level) : ILogger, IEnumerable<ServerLogMessage>
{
    private
#if NETCOREAPP
    readonly
#endif
    ConcurrentBag<ServerLogMessage> _values = new();

    private ServerTestHost? _serverTestHost;

    public void Initialize(ServerTestHost serverTestHost) => _serverTestHost = serverTestHost;

    public IEnumerator<ServerLogMessage> GetEnumerator() => _values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();

    public bool IsEnabled(LogLevel logLevel) => logLevel >= level;

    public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        string message = formatter(state, exception);
        ServerLogMessage logMessage = new(logLevel, message);

        if (_serverTestHost is not null)
        {
            // Server channel is async only.
            _serverTestHost.PushDataAsync(logMessage).GetAwaiter().GetResult();
        }
        else
        {
            _values.Add(logMessage);
        }
    }

    public async Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        string message = formatter(state, exception);
        ServerLogMessage logMessage = new(logLevel, message);

        if (_serverTestHost is not null)
        {
            await _serverTestHost.PushDataAsync(logMessage);
        }
        else
        {
            _values.Add(logMessage);
        }
    }

    public void Clean() =>
#if NETCOREAPP
        _values.Clear();
#else
        _values = new();
#endif

}
