﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Logging;

internal sealed class FileLoggerCategory : ILogger
{
    private readonly FileLogger _fileLogger;
    private readonly string _category;

    public FileLoggerCategory(FileLogger fileLogger, string category)
    {
        _fileLogger = fileLogger;
        _category = category;
    }

    public bool IsEnabled(LogLevel logLevel) => _fileLogger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _fileLogger.Log(logLevel, state, exception, formatter, _category);

    public async Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => await _fileLogger.LogAsync(logLevel, state, exception, formatter, _category);
}
