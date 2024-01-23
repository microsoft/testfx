// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Logging;

internal sealed class ServerLoggerForwarderProvider(LogLevel logLevel, IServiceProvider serviceProvider)
    : ILoggerProvider
{
    private readonly LogLevel _logLevel = logLevel;
    private
#if NETCOREAPP
    readonly
#endif
    ServerLogMessageInMemoryStore _serverLogMessageInMemoryStore = new(logLevel);

    private ServerTestHost? _serverTestHost;

    // If we don't have the server test host, we just log to the in-memory store.
    public ILogger CreateLogger(string categoryName)
        => _serverTestHost is null
        ? _serverLogMessageInMemoryStore
        : new ServerLoggerForwarder(_logLevel, serviceProvider.GetTask(), _serverTestHost);

    public async Task InitializeAsync(ServerTestHost serverTestHost)
    {
        _serverTestHost = serverTestHost;

        foreach (ServerLogMessage serverLogMessage in _serverLogMessageInMemoryStore.Values)
        {
            await _serverTestHost.PushDataAsync(serverLogMessage);
        }

#if NETCOREAPP
        _serverLogMessageInMemoryStore.Values.Clear();
#else
        _serverLogMessageInMemoryStore = new(_logLevel);
#endif
    }
}
