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

    internal static IEnumerable<(LogLevel DefaultLevel, LogLevel CurrentLevel, bool ShouldLog)> GetLogLevelCombinationsWithShouldLog()
    {
        yield return (LogLevel.Trace, LogLevel.Trace, true);
        yield return (LogLevel.Trace, LogLevel.Debug, true);
        yield return (LogLevel.Trace, LogLevel.Information, true);
        yield return (LogLevel.Trace, LogLevel.Warning, true);
        yield return (LogLevel.Trace, LogLevel.Error, true);
        yield return (LogLevel.Trace, LogLevel.Critical, true);
        yield return (LogLevel.Debug, LogLevel.Trace, false);
        yield return (LogLevel.Debug, LogLevel.Debug, true);
        yield return (LogLevel.Debug, LogLevel.Information, true);
        yield return (LogLevel.Debug, LogLevel.Warning, true);
        yield return (LogLevel.Debug, LogLevel.Error, true);
        yield return (LogLevel.Debug, LogLevel.Critical, true);
        yield return (LogLevel.Information, LogLevel.Trace, false);
        yield return (LogLevel.Information, LogLevel.Debug, false);
        yield return (LogLevel.Information, LogLevel.Information, true);
        yield return (LogLevel.Information, LogLevel.Warning, true);
        yield return (LogLevel.Information, LogLevel.Error, true);
        yield return (LogLevel.Information, LogLevel.Critical, true);
        yield return (LogLevel.Warning, LogLevel.Trace, false);
        yield return (LogLevel.Warning, LogLevel.Debug, false);
        yield return (LogLevel.Warning, LogLevel.Information, false);
        yield return (LogLevel.Warning, LogLevel.Warning, true);
        yield return (LogLevel.Warning, LogLevel.Error, true);
        yield return (LogLevel.Warning, LogLevel.Critical, true);
        yield return (LogLevel.Error, LogLevel.Trace, false);
        yield return (LogLevel.Error, LogLevel.Debug, false);
        yield return (LogLevel.Error, LogLevel.Information, false);
        yield return (LogLevel.Error, LogLevel.Warning, false);
        yield return (LogLevel.Error, LogLevel.Error, true);
        yield return (LogLevel.Error, LogLevel.Critical, true);
        yield return (LogLevel.Critical, LogLevel.Trace, false);
        yield return (LogLevel.Critical, LogLevel.Debug, false);
        yield return (LogLevel.Critical, LogLevel.Information, false);
        yield return (LogLevel.Critical, LogLevel.Warning, false);
        yield return (LogLevel.Critical, LogLevel.Error, false);
        yield return (LogLevel.Critical, LogLevel.Critical, true);
        yield return (LogLevel.None, LogLevel.Trace, false);
        yield return (LogLevel.None, LogLevel.Debug, false);
        yield return (LogLevel.None, LogLevel.Information, false);
        yield return (LogLevel.None, LogLevel.Warning, false);
        yield return (LogLevel.None, LogLevel.Error, false);
        yield return (LogLevel.None, LogLevel.Critical, false);
    }

    [ArgumentsProvider(nameof(GetLogLevelCombinationsWithShouldLog))]
    public void Logger_CheckEnabled(LogLevel defaultLogLevel, LogLevel currentLogLevel, bool shouldLog)
    {
        Logger<string> logger = CreateLogger(defaultLogLevel);
        Assert.AreEqual(logger.IsEnabled(currentLogLevel), shouldLog);
    }

    [ArgumentsProvider(nameof(GetLogLevelCombinationsWithShouldLog))]
    public void Logger_Log_FormattedStringIsCorrect(LogLevel defaultLogLevel, LogLevel currentLogLevel, bool shouldLog)
    {
        Logger<string> logger = CreateLogger(defaultLogLevel);

        logger.Log(currentLogLevel, Message, _exception, Formatter);
        _mockLogger.Verify(
            x => x.Log(currentLogLevel, Message, _exception, Formatter),
            shouldLog ? Times.Once : Times.Never);
    }

    [ArgumentsProvider(nameof(GetLogLevelCombinationsWithShouldLog))]
    public async ValueTask Logger_LogAsync_FormattedStringIsCorrect(LogLevel defaultLogLevel, LogLevel currentLogLevel, bool shouldLog)
    {
        Logger<string> logger = CreateLogger(defaultLogLevel);

        await logger.LogAsync(currentLogLevel, Message, _exception, Formatter);
        _mockLogger.Verify(
            x => x.LogAsync(currentLogLevel, Message, _exception, Formatter),
            shouldLog ? Times.Once : Times.Never);
    }
}
