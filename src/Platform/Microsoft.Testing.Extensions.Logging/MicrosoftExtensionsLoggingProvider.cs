// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MelILoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;
using MtpILogger = Microsoft.Testing.Platform.Logging.ILogger;
using MtpILoggerProvider = Microsoft.Testing.Platform.Logging.ILoggerProvider;

namespace Microsoft.Testing.Extensions.Logging;

/// <summary>
/// Microsoft.Testing.Platform logger provider that forwards every log message produced by the
/// platform and its extensions to a <see cref="MelILoggerFactory"/>.
/// </summary>
internal sealed class MicrosoftExtensionsLoggingProvider : MtpILoggerProvider, IDisposable
#if NETCOREAPP
#pragma warning disable SA1001 // Commas should be spaced correctly
    , IAsyncDisposable
#pragma warning restore SA1001 // Commas should be spaced correctly
#endif
{
    private readonly MelILoggerFactory _loggerFactory;
    private readonly bool _ownsFactory;
    private int _disposed;

    public MicrosoftExtensionsLoggingProvider(MelILoggerFactory loggerFactory, bool ownsFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _ownsFactory = ownsFactory;
    }

    public MtpILogger CreateLogger(string categoryName)
        => Volatile.Read(ref _disposed) != 0
            ? throw new ObjectDisposedException(nameof(MicrosoftExtensionsLoggingProvider))
            : new MicrosoftExtensionsLoggerAdapter(_loggerFactory.CreateLogger(categoryName));

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_ownsFactory && _loggerFactory is IDisposable disposable)
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

        if (!_ownsFactory)
        {
            return;
        }

        // Prefer the async path so that buffered/network-backed MEL providers (Serilog, OpenTelemetry,
        // Application Insights) can flush gracefully without blocking the synchronous Dispose() call.
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
