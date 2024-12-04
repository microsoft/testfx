// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Logging;

internal sealed class LoggerFactoryProxy : ILoggerFactory
{
    private ILoggerFactory? _loggerFactory;

    public ILogger CreateLogger(string categoryName) => _loggerFactory is null
            ? throw new InvalidOperationException(Resources.PlatformResources.LoggerFactoryNotReady)
            : _loggerFactory.CreateLogger(categoryName);

    public void SetLoggerFactory(ILoggerFactory loggerFactory)
    {
        Guard.NotNull(loggerFactory);
        _loggerFactory = loggerFactory;
    }
}
