// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Logging;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class LoggerTests
{
    private static readonly Func<string, Exception?, string> Formatter =
        (state, exception) =>
            string.Format(CultureInfo.InvariantCulture, "{0}{1}", state, exception is not null ? $" -- {exception}" : string.Empty);

    private const string Message = "Test";
    private readonly Exception _exception = new("TestException");
    private readonly Mock<ILogger> _mockLogger = new();

    public LoggerTests()
    {
        _mockLogger.Setup(x => x.Log(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception>(), Formatter));
        _mockLogger.Setup(x => x.LogAsync(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception>(), Formatter));
    }

    private Logger<string> CreateLogger(LogLevel logLevel)
    {
        _mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns<LogLevel>(currentLogLevel => currentLogLevel >= logLevel);

        Logger logger = new([_mockLogger.Object], logLevel);

        Mock<ILoggerFactory> mockLoggerFactory = new();
        mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(logger);
        return new Logger<string>(mockLoggerFactory.Object);
    }

    [DynamicData(nameof(LogTestHelpers.GetLogLevelCombinations), typeof(LogTestHelpers))]
    [TestMethod]
    public void Logger_CheckEnabled(LogLevel defaultLogLevel, LogLevel currentLogLevel)
    {
        Logger<string> logger = CreateLogger(defaultLogLevel);
        Assert.AreEqual(logger.IsEnabled(currentLogLevel), LogTestHelpers.IsLogEnabled(defaultLogLevel, currentLogLevel));
    }

    [DynamicData(nameof(LogTestHelpers.GetLogLevelCombinations), typeof(LogTestHelpers))]
    [TestMethod]
    public void Logger_Log_FormattedStringIsCorrect(LogLevel defaultLogLevel, LogLevel currentLogLevel)
    {
        Logger<string> logger = CreateLogger(defaultLogLevel);

        logger.Log(currentLogLevel, Message, _exception, Formatter);
        _mockLogger.Verify(
            x => x.Log(currentLogLevel, Message, _exception, Formatter),
            LogTestHelpers.GetExpectedLogCallTimes(defaultLogLevel, currentLogLevel));
    }

    [DynamicData(nameof(LogTestHelpers.GetLogLevelCombinations), typeof(LogTestHelpers))]
    [TestMethod]
    public async ValueTask Logger_LogAsync_FormattedStringIsCorrect(LogLevel defaultLogLevel, LogLevel currentLogLevel)
    {
        Logger<string> logger = CreateLogger(defaultLogLevel);

        await logger.LogAsync(currentLogLevel, Message, _exception, Formatter);
        _mockLogger.Verify(
            x => x.LogAsync(currentLogLevel, Message, _exception, Formatter),
            LogTestHelpers.GetExpectedLogCallTimes(defaultLogLevel, currentLogLevel));
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Synchronous provider dispatch")]
    [DataRow(true, DisplayName = "Asynchronous provider dispatch")]
    public async Task Log_MixedProviders_PreservesArgumentsAndEnabledProviderOrder(bool asynchronous)
    {
        List<string> events = [];
        Mock<ILogger> first = RecordingProvider("first", enabled: true);
        Mock<ILogger> disabled = RecordingProvider("disabled", enabled: false);
        Mock<ILogger> last = RecordingProvider("last", enabled: true);
        Logger logger = new([first.Object, disabled.Object, last.Object], LogLevel.Trace);
        InvalidOperationException expectedException = new("payload failure");

        if (asynchronous)
        {
            await logger.LogAsync(LogLevel.Information, "payload", expectedException, Formatter);
        }
        else
        {
            logger.Log(LogLevel.Information, "payload", expectedException, Formatter);
        }

        Assert.AreSequenceEqual(new[] { "first:payload", "last:payload" }, events);
        foreach (Mock<ILogger> provider in new[] { first, last })
        {
            provider.Verify(x => x.Log(LogLevel.Information, "payload", expectedException, Formatter), asynchronous ? Times.Never : Times.Once);
            provider.Verify(x => x.LogAsync(LogLevel.Information, "payload", expectedException, Formatter), asynchronous ? Times.Once : Times.Never);
        }

        disabled.Verify(x => x.Log(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<Func<string, Exception?, string>>()), Times.Never);
        disabled.Verify(x => x.LogAsync(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<Func<string, Exception?, string>>()), Times.Never);

        Mock<ILogger> RecordingProvider(string name, bool enabled)
        {
            Mock<ILogger> provider = new();
            provider.Setup(x => x.IsEnabled(LogLevel.Information)).Returns(enabled);
            provider.Setup(x => x.Log(LogLevel.Information, It.IsAny<string>(), It.IsAny<Exception?>(), Formatter))
                .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>((_, state, _, _) => events.Add($"{name}:{state}"));
            provider.Setup(x => x.LogAsync(LogLevel.Information, It.IsAny<string>(), It.IsAny<Exception?>(), Formatter))
                .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>((_, state, _, _) => events.Add($"{name}:{state}"))
                .Returns(Task.CompletedTask);
            return provider;
        }
    }

    [TestMethod]
    public async Task LogAsync_ProviderThrows_PreservesFailureAndDoesNotDispatchToLaterProviders()
    {
        InvalidOperationException expectedFailure = new("log provider failed");
        Mock<ILogger> first = new();
        first.Setup(x => x.IsEnabled(LogLevel.Error)).Returns(true);
        first.Setup(x => x.LogAsync(LogLevel.Error, "payload", null, Formatter)).ThrowsAsync(expectedFailure);
        Mock<ILogger> later = new(MockBehavior.Strict);
        Logger logger = new([first.Object, later.Object], LogLevel.Trace);

        InvalidOperationException actualFailure = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => logger.LogAsync(LogLevel.Error, "payload", null, Formatter));

        Assert.AreSame(expectedFailure, actualFailure);
        later.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void ServerLogMessage_ToStringPreservesLevelAndMultilinePayload()
    {
        ServerLogMessage message = new(LogLevel.Warning, "first line\n  second line");

        Assert.AreEqual(
            """
            Server log message:
            Level: Warning
            Message: first line
              second line
            """.Replace("\r\n", "\n") + "\n",
            message.ToString().Replace("\r\n", "\n"));
        Assert.AreEqual(LogLevel.Warning, message.Level);
        Assert.AreEqual("first line\n  second line", message.Message);
    }
}
