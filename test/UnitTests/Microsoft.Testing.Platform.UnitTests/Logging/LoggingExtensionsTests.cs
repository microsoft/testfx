// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Framework;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.TestInfrastructure;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public class LoggingExtensionsTests : TestBase
{
    private const string Message = "Test";
    private readonly Exception _exception = new("TestException");

    private readonly Mock<ILogger> _mockLogger = new();

    public LoggingExtensionsTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
        _mockLogger.Setup(x => x.Log(LogLevel.Trace, Message, null, LoggingExtensions.Formatter));
        _mockLogger.Setup(x => x.Log(LogLevel.Debug, Message, null, LoggingExtensions.Formatter));
        _mockLogger.Setup(x => x.Log(LogLevel.Information, Message, null, LoggingExtensions.Formatter));
        _mockLogger.Setup(x => x.Log(LogLevel.Warning, Message, null, LoggingExtensions.Formatter));
        _mockLogger.Setup(x => x.Log(LogLevel.Error, Message, null, LoggingExtensions.Formatter));
        _mockLogger.Setup(x => x.Log(LogLevel.Error, Message, _exception, LoggingExtensions.Formatter));
        _mockLogger.Setup(x => x.Log(LogLevel.Error, _exception.ToString(), null, LoggingExtensions.Formatter));
        _mockLogger.Setup(x => x.Log(LogLevel.Critical, Message, null, LoggingExtensions.Formatter));

        _mockLogger.Setup(x => x.LogAsync(LogLevel.Trace, Message, null, LoggingExtensions.Formatter));
        _mockLogger.Setup(x => x.LogAsync(LogLevel.Debug, Message, null, LoggingExtensions.Formatter));
        _mockLogger.Setup(x => x.LogAsync(LogLevel.Information, Message, null, LoggingExtensions.Formatter));
        _mockLogger.Setup(x => x.LogAsync(LogLevel.Warning, Message, null, LoggingExtensions.Formatter));
        _mockLogger.Setup(x => x.LogAsync(LogLevel.Error, Message, null, LoggingExtensions.Formatter));
        _mockLogger.Setup(x => x.LogAsync(LogLevel.Error, Message, _exception, LoggingExtensions.Formatter));
        _mockLogger.Setup(x => x.LogAsync(LogLevel.Error, _exception.ToString(), null, LoggingExtensions.Formatter));
        _mockLogger.Setup(x => x.LogAsync(LogLevel.Critical, Message, null, LoggingExtensions.Formatter));
    }

    public void LoggerExtensions_LogTest()
    {
        _mockLogger.Object.LogTrace(Message);
        _mockLogger.Object.LogDebug(Message);
        _mockLogger.Object.LogInformation(Message);
        _mockLogger.Object.LogWarning(Message);
        _mockLogger.Object.LogError(Message);
        _mockLogger.Object.LogError(Message, _exception);
        _mockLogger.Object.LogError(_exception);
        _mockLogger.Object.LogCritical(Message);

        _mockLogger.Verify(x => x.Log(LogLevel.Trace, Message, null, LoggingExtensions.Formatter), Times.Once);
        _mockLogger.Verify(x => x.Log(LogLevel.Debug, Message, null, LoggingExtensions.Formatter), Times.Once);
        _mockLogger.Verify(x => x.Log(LogLevel.Information, Message, null, LoggingExtensions.Formatter), Times.Once);
        _mockLogger.Verify(x => x.Log(LogLevel.Warning, Message, null, LoggingExtensions.Formatter), Times.Once);
        _mockLogger.Verify(x => x.Log(LogLevel.Error, Message, null, LoggingExtensions.Formatter), Times.Once);
        _mockLogger.Verify(x => x.Log(LogLevel.Error, _exception.ToString(), null, LoggingExtensions.Formatter), Times.Once);
        _mockLogger.Verify(x => x.Log(LogLevel.Error, Message, _exception, LoggingExtensions.Formatter), Times.Once);
        _mockLogger.Verify(x => x.Log(LogLevel.Critical, Message, null, LoggingExtensions.Formatter), Times.Once);
    }

    public async ValueTask LoggerExtensions_LogAsyncTest()
    {
        await _mockLogger.Object.LogTraceAsync(Message);
        await _mockLogger.Object.LogDebugAsync(Message);
        await _mockLogger.Object.LogInformationAsync(Message);
        await _mockLogger.Object.LogWarningAsync(Message);
        await _mockLogger.Object.LogErrorAsync(Message);
        await _mockLogger.Object.LogErrorAsync(Message, _exception);
        await _mockLogger.Object.LogErrorAsync(_exception);
        await _mockLogger.Object.LogCriticalAsync(Message);

        _mockLogger.Verify(x => x.LogAsync(LogLevel.Trace, Message, null, LoggingExtensions.Formatter), Times.Once);
        _mockLogger.Verify(x => x.LogAsync(LogLevel.Debug, Message, null, LoggingExtensions.Formatter), Times.Once);
        _mockLogger.Verify(x => x.LogAsync(LogLevel.Information, Message, null, LoggingExtensions.Formatter), Times.Once);
        _mockLogger.Verify(x => x.LogAsync(LogLevel.Warning, Message, null, LoggingExtensions.Formatter), Times.Once);
        _mockLogger.Verify(x => x.LogAsync(LogLevel.Error, Message, null, LoggingExtensions.Formatter), Times.Once);
        _mockLogger.Verify(x => x.LogAsync(LogLevel.Error, _exception.ToString(), null, LoggingExtensions.Formatter), Times.Once);
        _mockLogger.Verify(x => x.LogAsync(LogLevel.Error, Message, _exception, LoggingExtensions.Formatter), Times.Once);
        _mockLogger.Verify(x => x.LogAsync(LogLevel.Critical, Message, null, LoggingExtensions.Formatter), Times.Once);
    }
}
