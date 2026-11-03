// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.Testing.Platform.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public sealed class MTPTraceLoggerTests : TestContainer
{
    public void EverySeverityPreservesMessagesAndException()
    {
        var logger = new RecordingLogger();
        var bridge = new MTPTraceLogger(logger);
        var invariantValue = new InvariantValue();

        bridge.Verbose("trace");
        bridge.Verbose("trace {0}", invariantValue);
        bridge.Info("info");
        bridge.Info("info {0}", invariantValue);
        bridge.Warning("warning");
        bridge.Warning("warning {0}", invariantValue);
        bridge.Error("error");
        bridge.Error("error {0}", invariantValue);
        bridge.Error(new InvalidOperationException("failure"));

        logger.Messages.Should().Equal(
            (LogLevel.Trace, "trace"),
            (LogLevel.Trace, "trace invariant"),
            (LogLevel.Information, "info"),
            (LogLevel.Information, "info invariant"),
            (LogLevel.Warning, "warning"),
            (LogLevel.Warning, "warning invariant"),
            (LogLevel.Error, "error"),
            (LogLevel.Error, "error invariant"),
            (LogLevel.Error, "System.InvalidOperationException: failure"));
    }

    public void EnablementUsesTheUnderlyingLoggerLevels()
    {
        var logger = new RecordingLogger();
        var bridge = new MTPTraceLogger(logger);

        bridge.IsVerboseEnabled.Should().BeFalse();
        bridge.IsInfoEnabled.Should().BeFalse();
        bridge.IsWarningEnabled.Should().BeFalse();
        bridge.IsErrorEnabled.Should().BeFalse();
        logger.Enabled.UnionWith([LogLevel.Trace, LogLevel.Information, LogLevel.Warning]);
        bridge.IsVerboseEnabled.Should().BeTrue();
        bridge.IsInfoEnabled.Should().BeTrue();
        bridge.IsWarningEnabled.Should().BeTrue();
        bridge.IsErrorEnabled.Should().BeTrue();
    }

    private sealed class InvariantValue : IFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider)
            => ReferenceEquals(formatProvider, CultureInfo.InvariantCulture) ? "invariant" : "wrong provider";
    }

    private sealed class RecordingLogger : ILogger
    {
        internal List<(LogLevel Level, string Text)> Messages { get; } = [];

        internal HashSet<LogLevel> Enabled { get; } = [];

        public bool IsEnabled(LogLevel logLevel) => Enabled.Contains(logLevel);

        public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add((logLevel, formatter(state, exception)));

        public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Log(logLevel, state, exception, formatter);
            return Task.CompletedTask;
        }
    }
}
