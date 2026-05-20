// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MtpILogger = Microsoft.Testing.Platform.Logging.ILogger;
using MtpILoggerProvider = Microsoft.Testing.Platform.Logging.ILoggerProvider;
using MtpLogLevel = Microsoft.Testing.Platform.Logging.LogLevel;

namespace Microsoft.Testing.Extensions.Logging;

/// <summary>
/// Singleton no-op <see cref="MtpILoggerProvider"/> used when the platform's effective
/// <see cref="MtpLogLevel"/> is <see cref="MtpLogLevel.None"/>, so that no Microsoft.Extensions.Logging
/// pipeline is constructed for runs that will never emit a log.
/// </summary>
internal sealed class NopLoggerProvider : MtpILoggerProvider
{
    public static NopLoggerProvider Instance { get; } = new();

    private NopLoggerProvider()
    {
    }

    public MtpILogger CreateLogger(string categoryName) => NopLogger.Instance;

    private sealed class NopLogger : MtpILogger
    {
        public static NopLogger Instance { get; } = new();

        private NopLogger()
        {
        }

        public bool IsEnabled(MtpLogLevel logLevel) => false;

        public void Log<TState>(MtpLogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }

        public Task LogAsync<TState>(MtpLogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Task.CompletedTask;
    }
}
