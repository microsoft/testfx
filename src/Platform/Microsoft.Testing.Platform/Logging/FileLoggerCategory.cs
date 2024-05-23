// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Logging;

internal sealed class FileLoggerCategory(FileLogger fileLogger, string category) : ILogger
{
    public bool IsEnabled(LogLevel logLevel) => fileLogger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => fileLogger.Log(logLevel, state, exception, formatter, category);

    public async Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => await fileLogger.LogAsync(logLevel, state, exception, formatter, category);
}
