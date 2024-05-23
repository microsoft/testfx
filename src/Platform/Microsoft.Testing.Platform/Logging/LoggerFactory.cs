// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Logging;

internal sealed class LoggerFactory(ILoggerProvider[] loggerProviders, LogLevel logLevel, IMonitor monitor) : ILoggerFactory, IDisposable
#if NETCOREAPP
#pragma warning disable SA1001 // Commas should be spaced correctly
    , IAsyncDisposable
#pragma warning restore SA1001 // Commas should be spaced correctly
#endif
{
    private readonly object _sync = new();
    private readonly Dictionary<string, Logger> _loggers = new(StringComparer.Ordinal);

    public ILogger CreateLogger(string categoryName)
    {
        using (monitor.Lock(_sync))
        {
            if (!_loggers.TryGetValue(categoryName, out Logger? logger))
            {
                logger = new Logger(CreateLoggers(categoryName), logLevel);

                _loggers[categoryName] = logger;
            }

            return logger;
        }
    }

    private ILogger[] CreateLoggers(string categoryName)
    {
        List<ILogger> loggers = [];
        foreach (ILoggerProvider loggerProvider in loggerProviders)
        {
            loggers.Add(loggerProvider.CreateLogger(categoryName));
        }

        return loggers.ToArray();
    }

    public void Dispose()
    {
        foreach (IDisposable disposable in loggerProviders.OfType<IDisposable>())
        {
            // FileLoggerProvider is special and needs to be disposed manually.
            if (disposable is FileLoggerProvider)
            {
                continue;
            }

            disposable.Dispose();
        }
    }

#if NETCOREAPP
    public async ValueTask DisposeAsync()
    {
        foreach (IAsyncDisposable asyncDisposable in loggerProviders.OfType<IAsyncDisposable>())
        {
            // FileLoggerProvider is special and needs to be disposed manually.
            if (asyncDisposable is FileLoggerProvider)
            {
                continue;
            }

            await asyncDisposable.DisposeAsync();
        }
    }
#endif
}
