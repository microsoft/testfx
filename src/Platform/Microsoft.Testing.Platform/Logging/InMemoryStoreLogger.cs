// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace Microsoft.Testing.Platform.Logging;

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
