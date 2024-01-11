// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Framework;
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
    private readonly Exception _exception = new("TestException");
    private readonly Mock<ILogger> _mockLogger = new();

    public LoggerTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
        _mockLogger.Setup(x => x.Log(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception>(), Formatter));
        _mockLogger.Setup(x => x.LogAsync(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception>(), Formatter));
    }

    private Logger<string> CreateLogger(LogLevel logLevel)
    {
        _mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns<LogLevel>(currentLogLevel => currentLogLevel >= logLevel);

        Logger logger = new(new[] { _mockLogger.Object }, logLevel);

        Mock<ILoggerFactory> mockLoggerFactory = new();
        mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(logger);
        return new Logger<string>(mockLoggerFactory.Object);
    }

    [Arguments(LogLevel.Trace, LogLevel.Trace, true)]
    [Arguments(LogLevel.Trace, LogLevel.Debug, true)]
    [Arguments(LogLevel.Trace, LogLevel.Information, true)]
    [Arguments(LogLevel.Trace, LogLevel.Warning, true)]
    [Arguments(LogLevel.Trace, LogLevel.Error, true)]
    [Arguments(LogLevel.Trace, LogLevel.Critical, true)]
    [Arguments(LogLevel.Debug, LogLevel.Trace, false)]
    [Arguments(LogLevel.Debug, LogLevel.Debug, true)]
    [Arguments(LogLevel.Debug, LogLevel.Information, true)]
    [Arguments(LogLevel.Debug, LogLevel.Warning, true)]
    [Arguments(LogLevel.Debug, LogLevel.Error, true)]
    [Arguments(LogLevel.Debug, LogLevel.Critical, true)]
    [Arguments(LogLevel.Information, LogLevel.Trace, false)]
    [Arguments(LogLevel.Information, LogLevel.Debug, false)]
    [Arguments(LogLevel.Information, LogLevel.Information, true)]
    [Arguments(LogLevel.Information, LogLevel.Warning, true)]
    [Arguments(LogLevel.Information, LogLevel.Error, true)]
    [Arguments(LogLevel.Information, LogLevel.Critical, true)]
    [Arguments(LogLevel.Warning, LogLevel.Trace, false)]
    [Arguments(LogLevel.Warning, LogLevel.Debug, false)]
    [Arguments(LogLevel.Warning, LogLevel.Information, false)]
    [Arguments(LogLevel.Warning, LogLevel.Warning, true)]
    [Arguments(LogLevel.Warning, LogLevel.Error, true)]
    [Arguments(LogLevel.Warning, LogLevel.Critical, true)]
    [Arguments(LogLevel.Error, LogLevel.Trace, false)]
    [Arguments(LogLevel.Error, LogLevel.Debug, false)]
    [Arguments(LogLevel.Error, LogLevel.Information, false)]
    [Arguments(LogLevel.Error, LogLevel.Warning, false)]
    [Arguments(LogLevel.Error, LogLevel.Error, true)]
    [Arguments(LogLevel.Error, LogLevel.Critical, true)]
    [Arguments(LogLevel.Critical, LogLevel.Trace, false)]
    [Arguments(LogLevel.Critical, LogLevel.Debug, false)]
    [Arguments(LogLevel.Critical, LogLevel.Information, false)]
    [Arguments(LogLevel.Critical, LogLevel.Warning, false)]
    [Arguments(LogLevel.Critical, LogLevel.Error, false)]
    [Arguments(LogLevel.Critical, LogLevel.Critical, true)]
    [Arguments(LogLevel.None, LogLevel.Trace, false)]
    [Arguments(LogLevel.None, LogLevel.Debug, false)]
    [Arguments(LogLevel.None, LogLevel.Information, false)]
    [Arguments(LogLevel.None, LogLevel.Warning, false)]
    [Arguments(LogLevel.None, LogLevel.Error, false)]
    [Arguments(LogLevel.None, LogLevel.Critical, false)]
    public void Logger_CheckEnabled(LogLevel defaultLogLevel, LogLevel currentLogLevel, bool shouldBeEnabled)
    {
        Logger<string> logger = CreateLogger(defaultLogLevel);
        Assert.AreEqual(logger.IsEnabled(currentLogLevel), shouldBeEnabled);
    }

    [Arguments(LogLevel.Trace, LogLevel.Trace, true)]
    [Arguments(LogLevel.Trace, LogLevel.Debug, true)]
    [Arguments(LogLevel.Trace, LogLevel.Information, true)]
    [Arguments(LogLevel.Trace, LogLevel.Warning, true)]
    [Arguments(LogLevel.Trace, LogLevel.Error, true)]
    [Arguments(LogLevel.Trace, LogLevel.Critical, true)]
    [Arguments(LogLevel.Debug, LogLevel.Trace, false)]
    [Arguments(LogLevel.Debug, LogLevel.Debug, true)]
    [Arguments(LogLevel.Debug, LogLevel.Information, true)]
    [Arguments(LogLevel.Debug, LogLevel.Warning, true)]
    [Arguments(LogLevel.Debug, LogLevel.Error, true)]
    [Arguments(LogLevel.Debug, LogLevel.Critical, true)]
    [Arguments(LogLevel.Information, LogLevel.Trace, false)]
    [Arguments(LogLevel.Information, LogLevel.Debug, false)]
    [Arguments(LogLevel.Information, LogLevel.Information, true)]
    [Arguments(LogLevel.Information, LogLevel.Warning, true)]
    [Arguments(LogLevel.Information, LogLevel.Error, true)]
    [Arguments(LogLevel.Information, LogLevel.Critical, true)]
    [Arguments(LogLevel.Warning, LogLevel.Trace, false)]
    [Arguments(LogLevel.Warning, LogLevel.Debug, false)]
    [Arguments(LogLevel.Warning, LogLevel.Information, false)]
    [Arguments(LogLevel.Warning, LogLevel.Warning, true)]
    [Arguments(LogLevel.Warning, LogLevel.Error, true)]
    [Arguments(LogLevel.Warning, LogLevel.Critical, true)]
    [Arguments(LogLevel.Error, LogLevel.Trace, false)]
    [Arguments(LogLevel.Error, LogLevel.Debug, false)]
    [Arguments(LogLevel.Error, LogLevel.Information, false)]
    [Arguments(LogLevel.Error, LogLevel.Warning, false)]
    [Arguments(LogLevel.Error, LogLevel.Error, true)]
    [Arguments(LogLevel.Error, LogLevel.Critical, true)]
    [Arguments(LogLevel.Critical, LogLevel.Trace, false)]
    [Arguments(LogLevel.Critical, LogLevel.Debug, false)]
    [Arguments(LogLevel.Critical, LogLevel.Information, false)]
    [Arguments(LogLevel.Critical, LogLevel.Warning, false)]
    [Arguments(LogLevel.Critical, LogLevel.Error, false)]
    [Arguments(LogLevel.Critical, LogLevel.Critical, true)]
    [Arguments(LogLevel.None, LogLevel.Trace, false)]
    [Arguments(LogLevel.None, LogLevel.Debug, false)]
    [Arguments(LogLevel.None, LogLevel.Information, false)]
    [Arguments(LogLevel.None, LogLevel.Warning, false)]
    [Arguments(LogLevel.None, LogLevel.Error, false)]
    [Arguments(LogLevel.None, LogLevel.Critical, false)]
    public void Logger_Log_FormattedStringIsCorrect(LogLevel defaultLogLevel, LogLevel currentLogLevel, bool shouldBeEnabled)
    {
        Logger<string> logger = CreateLogger(defaultLogLevel);

        logger.Log(currentLogLevel, Message, _exception, Formatter);
        _mockLogger.Verify(
            x => x.Log(currentLogLevel, Message, _exception, Formatter),
            shouldBeEnabled ? Times.Once : Times.Never);
    }

    [Arguments(LogLevel.Trace, LogLevel.Trace, true)]
    [Arguments(LogLevel.Trace, LogLevel.Debug, true)]
    [Arguments(LogLevel.Trace, LogLevel.Information, true)]
    [Arguments(LogLevel.Trace, LogLevel.Warning, true)]
    [Arguments(LogLevel.Trace, LogLevel.Error, true)]
    [Arguments(LogLevel.Trace, LogLevel.Critical, true)]
    [Arguments(LogLevel.Debug, LogLevel.Trace, false)]
    [Arguments(LogLevel.Debug, LogLevel.Debug, true)]
    [Arguments(LogLevel.Debug, LogLevel.Information, true)]
    [Arguments(LogLevel.Debug, LogLevel.Warning, true)]
    [Arguments(LogLevel.Debug, LogLevel.Error, true)]
    [Arguments(LogLevel.Debug, LogLevel.Critical, true)]
    [Arguments(LogLevel.Information, LogLevel.Trace, false)]
    [Arguments(LogLevel.Information, LogLevel.Debug, false)]
    [Arguments(LogLevel.Information, LogLevel.Information, true)]
    [Arguments(LogLevel.Information, LogLevel.Warning, true)]
    [Arguments(LogLevel.Information, LogLevel.Error, true)]
    [Arguments(LogLevel.Information, LogLevel.Critical, true)]
    [Arguments(LogLevel.Warning, LogLevel.Trace, false)]
    [Arguments(LogLevel.Warning, LogLevel.Debug, false)]
    [Arguments(LogLevel.Warning, LogLevel.Information, false)]
    [Arguments(LogLevel.Warning, LogLevel.Warning, true)]
    [Arguments(LogLevel.Warning, LogLevel.Error, true)]
    [Arguments(LogLevel.Warning, LogLevel.Critical, true)]
    [Arguments(LogLevel.Error, LogLevel.Trace, false)]
    [Arguments(LogLevel.Error, LogLevel.Debug, false)]
    [Arguments(LogLevel.Error, LogLevel.Information, false)]
    [Arguments(LogLevel.Error, LogLevel.Warning, false)]
    [Arguments(LogLevel.Error, LogLevel.Error, true)]
    [Arguments(LogLevel.Error, LogLevel.Critical, true)]
    [Arguments(LogLevel.Critical, LogLevel.Trace, false)]
    [Arguments(LogLevel.Critical, LogLevel.Debug, false)]
    [Arguments(LogLevel.Critical, LogLevel.Information, false)]
    [Arguments(LogLevel.Critical, LogLevel.Warning, false)]
    [Arguments(LogLevel.Critical, LogLevel.Error, false)]
    [Arguments(LogLevel.Critical, LogLevel.Critical, true)]
    [Arguments(LogLevel.None, LogLevel.Trace, false)]
    [Arguments(LogLevel.None, LogLevel.Debug, false)]
    [Arguments(LogLevel.None, LogLevel.Information, false)]
    [Arguments(LogLevel.None, LogLevel.Warning, false)]
    [Arguments(LogLevel.None, LogLevel.Error, false)]
    [Arguments(LogLevel.None, LogLevel.Critical, false)]
    public async ValueTask Logger_LogAsync_FormattedStringIsCorrect(LogLevel defaultLogLevel, LogLevel currentLogLevel, bool shouldBeEnabled)
    {
        Logger<string> logger = CreateLogger(defaultLogLevel);

        await logger.LogAsync(currentLogLevel, Message, _exception, Formatter);
        _mockLogger.Verify(
            x => x.LogAsync(currentLogLevel, Message, _exception, Formatter),
            shouldBeEnabled ? Times.Once : Times.Never);
    }
}
