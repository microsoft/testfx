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
public class ConsoleLoggerTests : TestBase
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
    private readonly ConsoleLogger _consoleLogger;

    public ConsoleLoggerTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
        _mockConsole.Setup(x => x.WriteLine(It.IsAny<string>())).Callback(() => { });
        _mockClock.Setup(x => x.UtcNow).Returns(new DateTimeOffset(new(2023, 5, 29, 3, 42, 13)));

        _consoleLogger = (new ConsoleLoggerProvider(MinimumLogLevel, _mockConsole.Object, _mockClock.Object).CreateLogger(Category) as ConsoleLogger)!;
    }

    public void ConsoleLogger_CheckEnabled()
    {
        Assert.IsTrue(_consoleLogger.IsEnabled(LogLevel.Information));
        Assert.IsTrue(_consoleLogger.IsEnabled(LogLevel.Error));
        Assert.IsFalse(_consoleLogger.IsEnabled(LogLevel.Trace));
    }

    public void ConsoleLogger_Log_FormattedStringIsCorrect()
    {
        _consoleLogger.Log(LogLevel.Error, Message, _exception, Formatter);
        _consoleLogger.Log(LogLevel.Information, Message, null, Formatter);

        _mockConsole.Verify(
            x => x.WriteLine($"{string.Format(CultureInfo.InvariantCulture, _expectedPrefix, LogLevel.Information)} {Formatter(Message, null)}"),
            Times.Once);
        _mockConsole.Verify(
            x => x.WriteLine($"{string.Format(CultureInfo.InvariantCulture, _expectedPrefix, LogLevel.Error)} {Formatter(Message, _exception)}"),
            Times.Once);
    }

    public async ValueTask ConsoleLogger_LogAsync_FormattedStringIsCorrect()
    {
        await _consoleLogger.LogAsync(LogLevel.Error, Message, _exception, Formatter);
        await _consoleLogger.LogAsync(LogLevel.Information, Message, null, Formatter);

        _mockConsole.Verify(
            x => x.WriteLine($"{string.Format(CultureInfo.InvariantCulture, _expectedPrefix, LogLevel.Information)} {Formatter(Message, null)}"),
            Times.Once);
        _mockConsole.Verify(
            x => x.WriteLine($"{string.Format(CultureInfo.InvariantCulture, _expectedPrefix, LogLevel.Error)} {Formatter(Message, _exception)}"),
            Times.Once);
    }
}
