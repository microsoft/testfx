// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Hosts;

namespace Microsoft.Testing.Platform.Logging;

internal sealed class ServerLoggerForwarderProvider(LogLevel logLevel, IServiceProvider serviceProvider)
    : ILoggerProvider
{
    private readonly LogLevel _logLevel = logLevel;
    private readonly ServerLogMessageInMemoryStore _serverLogMessageInMemoryStore = new(logLevel);

    private ServerTestHost? _serverTestHost;

    // If we don't have the server test host, we just log to the in-memory store.
    public ILogger CreateLogger(string categoryName)
        => _serverTestHost is null
        ? _serverLogMessageInMemoryStore
        : new ServerLoggerForwarder(_logLevel, _serverTestHost);

    public async Task InitializeAsync(ServerTestHost serverTestHost)
    {
        _serverTestHost = serverTestHost;
        _serverLogMessageInMemoryStore.Initialize(serverTestHost);

        foreach (ServerLogMessage serverLogMessage in _serverLogMessageInMemoryStore)
        {
            await _serverTestHost.PushDataAsync(serverLogMessage);
        }

        _serverLogMessageInMemoryStore.Clean();
    }
}
