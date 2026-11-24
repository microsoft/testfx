// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Logging;

internal sealed class LoggerFactoryProxy : ILoggerFactory, IDisposable
#if NETCOREAPP
#pragma warning disable SA1001 // Commas should be spaced correctly
    , IAsyncDisposable
#pragma warning restore SA1001 // Commas should be spaced correctly
#endif
{
    private ILoggerFactory? _loggerFactory;
    private int _disposed;

    public ILogger CreateLogger(string categoryName)
        => Volatile.Read(ref _disposed) != 0
            ? throw new ObjectDisposedException(nameof(LoggerFactoryProxy))
            : _loggerFactory is null
                ? throw new InvalidOperationException(Resources.PlatformResources.LoggerFactoryNotReady)
                : _loggerFactory.CreateLogger(categoryName);

    public void SetLoggerFactory(ILoggerFactory loggerFactory)
        => _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_loggerFactory is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

#if NETCOREAPP
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_loggerFactory is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else if (_loggerFactory is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
#endif
}
