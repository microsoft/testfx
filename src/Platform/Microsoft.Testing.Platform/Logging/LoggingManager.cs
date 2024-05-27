// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Logging;

internal sealed class LoggingManager : ILoggingManager
{
    private readonly List<Func<LogLevel, IServiceProvider, ILoggerProvider>> _loggerProviderFullFactories = [];

    public void AddProvider(Func<LogLevel, IServiceProvider, ILoggerProvider> loggerProviderFactory)
    {
        ArgumentGuard.IsNotNull(loggerProviderFactory);
        _loggerProviderFullFactories.Add(loggerProviderFactory);
    }

    internal async Task<ILoggerFactory> BuildAsync(IServiceProvider serviceProvider, LogLevel logLevel, IMonitor monitor)
    {
        List<ILoggerProvider> loggerProviders = [];

        foreach (Func<LogLevel, IServiceProvider, ILoggerProvider> factory in _loggerProviderFullFactories)
        {
            ILoggerProvider serviceInstance = factory(logLevel, serviceProvider);
            if (serviceInstance is IExtension extension && !await extension.IsEnabledAsync())
            {
                continue;
            }

            await serviceInstance.TryInitialize();

            loggerProviders.Add(serviceInstance);
        }

        return new LoggerFactory(loggerProviders.ToArray(), logLevel, monitor);
    }
}
