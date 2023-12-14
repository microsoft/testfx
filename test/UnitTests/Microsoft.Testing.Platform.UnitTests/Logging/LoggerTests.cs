// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Framework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.TestInfrastructure;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public class LoggerTests : TestBase
{
    private static readonly Func<string, Exception?, string> Formatter =
        (state, exception) =>
            string.Format(CultureInfo.InvariantCulture, "{0}{1}", state, exception is not null ? $" -- {exception}" : string.Empty);

    private const string Message = "Test";
    private const string Category = "ConsoleLoggerTests";
    private const LogLevel MinimumLogLevel = LogLevel.Information;

    private readonly string _expectedPrefix = "[03:42:13 ConsoleLoggerTests - {0}]";
    private readonly Exception _exception = new("TestException");

    private readonly Mock<IConsole> _mockConsole = new();
    private readonly Mock<IClock> _mockClock = new();
    private readonly Mock<ILoggerFactory> _mockLoggerFactory = new();
    private readonly Logger _logger;
    private readonly Logger<string> _genericLogger;

    public LoggerTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
        _mockConsole.Setup(x => x.WriteLine(It.IsAny<string>())).Callback(() => { });
        _mockClock.Setup(x => x.UtcNow).Returns(new DateTimeOffset(new(2023, 5, 29, 3, 42, 13)));

        _logger = new Logger(
            new[]
            {
                new ConsoleLoggerProvider(MinimumLogLevel, _mockConsole.Object, _mockClock.Object).CreateLogger(Category),
                new NopLogger(),
            },
            MinimumLogLevel);

        _mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_logger);
        _genericLogger = new Logger<string>(_mockLoggerFactory.Object);
    }

    public void Logger_CheckEnabled()
    {
        Assert.IsTrue(_genericLogger.IsEnabled(LogLevel.Information));
        Assert.IsTrue(_genericLogger.IsEnabled(LogLevel.Error));
        Assert.IsFalse(_genericLogger.IsEnabled(LogLevel.Trace));
    }

    public void Logger_Log_FormattedStringIsCorrect()
    {
        _genericLogger.Log(LogLevel.Error, Message, _exception, Formatter);
        _genericLogger.Log(LogLevel.Information, Message, null, Formatter);

        _mockConsole.Verify(
            x => x.WriteLine($"{string.Format(CultureInfo.InvariantCulture, _expectedPrefix, LogLevel.Information)} {Formatter(Message, null)}"),
            Times.Once);
        _mockConsole.Verify(
            x => x.WriteLine($"{string.Format(CultureInfo.InvariantCulture, _expectedPrefix, LogLevel.Error)} {Formatter(Message, _exception)}"),
            Times.Once);
    }

    public async ValueTask Logger_LogAsync_FormattedStringIsCorrect()
    {
        await _genericLogger.LogAsync(LogLevel.Error, Message, _exception, Formatter);
        await _genericLogger.LogAsync(LogLevel.Information, Message, null, Formatter);

        _mockConsole.Verify(
            x => x.WriteLine($"{string.Format(CultureInfo.InvariantCulture, _expectedPrefix, LogLevel.Information)} {Formatter(Message, null)}"),
            Times.Once);
        _mockConsole.Verify(
            x => x.WriteLine($"{string.Format(CultureInfo.InvariantCulture, _expectedPrefix, LogLevel.Error)} {Formatter(Message, _exception)}"),
            Times.Once);
    }
}
