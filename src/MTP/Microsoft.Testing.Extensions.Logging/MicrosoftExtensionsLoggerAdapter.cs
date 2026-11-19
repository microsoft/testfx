// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MelILogger = Microsoft.Extensions.Logging.ILogger;
using MtpILogger = Microsoft.Testing.Platform.Logging.ILogger;
using MtpLogLevel = Microsoft.Testing.Platform.Logging.LogLevel;

namespace Microsoft.Testing.Extensions.Logging;

/// <summary>
/// Adapter that exposes a <see cref="MelILogger"/> as an <see cref="MtpILogger"/>.
/// </summary>
internal sealed class MicrosoftExtensionsLoggerAdapter : MtpILogger
{
    private readonly MelILogger _inner;

    public MicrosoftExtensionsLoggerAdapter(MelILogger inner)
        => _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public bool IsEnabled(MtpLogLevel logLevel)
        => _inner.IsEnabled(LogLevelMapper.ToMicrosoftExtensions(logLevel));

    public void Log<TState>(MtpLogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _inner.Log(LogLevelMapper.ToMicrosoftExtensions(logLevel), default, state, exception, formatter);

    public Task LogAsync<TState>(MtpLogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // Microsoft.Extensions.Logging has no async API; forward synchronously and complete.
        Log(logLevel, state, exception, formatter);
        return Task.CompletedTask;
    }
}
