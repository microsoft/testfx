﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Logging;

internal sealed class Logger(ILogger[] loggers, LogLevel logLevel) : ILogger
{
    private readonly ILogger[] _loggers = loggers;
    private readonly LogLevel _logLevel = logLevel;

    public bool IsEnabled(LogLevel logLevel)
        => logLevel >= _logLevel;

    public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        foreach (ILogger logger in _loggers)
        {
            if (logger.IsEnabled(_logLevel))
            {
                logger.Log(logLevel, state, exception, formatter);
            }
        }
    }

    public async Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        foreach (ILogger logger in _loggers)
        {
            if (logger.IsEnabled(_logLevel))
            {
                await logger.LogAsync(logLevel, state, exception, formatter);
            }
        }
    }
}

internal sealed class Logger<TCategoryName> : ILogger<TCategoryName>
{
    private readonly ILogger _logger;

    public Logger(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(GetCategoryName());
    }

    public bool IsEnabled(LogLevel logLevel)
        => _logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _logger.Log(logLevel, state, exception, formatter);

    public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _logger.LogAsync(logLevel, state, exception, formatter);

    private static string GetCategoryName()
        => TypeNameHelper.GetTypeDisplayName(typeof(TCategoryName), includeGenericParameters: false, nestedTypeDelimiter: '.');
}
