// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Logging;

internal sealed class ServerLogMessageInMemoryStore(LogLevel logLevel) : InMemoryStoreLogger<ServerLogMessage>(logLevel)
{
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
