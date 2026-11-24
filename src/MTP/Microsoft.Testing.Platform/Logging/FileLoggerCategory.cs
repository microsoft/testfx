// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Logging;

internal sealed class FileLoggerCategory : ILogger
{
    private FileLogger? FileLogger { get; }

    private FileLoggerProvider? FileLoggerProvider { get; }

    private string Category { get; }

    public FileLoggerCategory(FileLogger fileLogger, string category)
    {
        FileLogger = fileLogger;
        Category = category;
    }

    public FileLoggerCategory(FileLoggerProvider fileLoggerProvider, string category)
    {
        FileLoggerProvider = fileLoggerProvider;
        Category = category;
    }

    public bool IsEnabled(LogLevel logLevel)
        => FileLoggerProvider?.IsEnabled(logLevel) ?? FileLogger!.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (FileLoggerProvider is not null)
        {
            FileLoggerProvider.Log(logLevel, state, exception, formatter, Category);
        }
        else
        {
            FileLogger!.Log(logLevel, state, exception, formatter, Category);
        }
    }

    public async Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (FileLoggerProvider is not null)
        {
            await FileLoggerProvider.LogAsync(logLevel, state, exception, formatter, Category).ConfigureAwait(false);
        }
        else
        {
            await FileLogger!.LogAsync(logLevel, state, exception, formatter, Category).ConfigureAwait(false);
        }
    }
}
