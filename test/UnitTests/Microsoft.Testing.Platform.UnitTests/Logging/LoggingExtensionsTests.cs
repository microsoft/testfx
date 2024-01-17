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

    public void LoggerExtensions_LogTrace_CallsLogWithLogLevelTrace()
    {
        _mockLogger.Setup(x => x.Log(LogLevel.Trace, Message, null, LoggingExtensions.Formatter));
        _mockLogger.Object.LogTrace(Message);
        _mockLogger.Verify(x => x.Log(LogLevel.Trace, Message, null, LoggingExtensions.Formatter), Times.Once);
    }

    public void LoggerExtensions_LogDebug_CallsLogWithLogLevelDebug()
    {
        _mockLogger.Setup(x => x.Log(LogLevel.Debug, Message, null, LoggingExtensions.Formatter));
        _mockLogger.Object.LogDebug(Message);
        _mockLogger.Verify(x => x.Log(LogLevel.Debug, Message, null, LoggingExtensions.Formatter), Times.Once);
    }

    public void LoggerExtensions_LogInformation_CallsLogWithLogLevelInformation()
    {
        _mockLogger.Setup(x => x.Log(LogLevel.Information, Message, null, LoggingExtensions.Formatter));
        _mockLogger.Object.LogInformation(Message);
        _mockLogger.Verify(x => x.Log(LogLevel.Information, Message, null, LoggingExtensions.Formatter), Times.Once);
    }

    public void LoggerExtensions_LogWarning_CallsLogWithLogLevelWarning()
    {
        _mockLogger.Setup(x => x.Log(LogLevel.Warning, Message, null, LoggingExtensions.Formatter));
        _mockLogger.Object.LogWarning(Message);
        _mockLogger.Verify(x => x.Log(LogLevel.Warning, Message, null, LoggingExtensions.Formatter), Times.Once);
    }

    public void LoggerExtensions_LogError_CallsLogWithLogLevelError()
    {
        _mockLogger.Setup(x => x.Log(LogLevel.Error, Message, null, LoggingExtensions.Formatter));
        _mockLogger.Object.LogError(Message);
        _mockLogger.Verify(x => x.Log(LogLevel.Error, Message, null, LoggingExtensions.Formatter), Times.Once);
    }

    public void LoggerExtensions_LogError_CallsLogWithLogLevelErrorAndMessageAndException()
    {
        _mockLogger.Setup(x => x.Log(LogLevel.Error, Message, _exception, LoggingExtensions.Formatter));
        _mockLogger.Object.LogError(Message, _exception);
        _mockLogger.Verify(x => x.Log(LogLevel.Error, Message, _exception, LoggingExtensions.Formatter), Times.Once);
    }

    public void LoggerExtensions_LogError_CallsLogWithLogLevelErrorAndException()
    {
        _mockLogger.Setup(x => x.Log(LogLevel.Error, _exception.ToString(), null, LoggingExtensions.Formatter));
        _mockLogger.Object.LogError(_exception);
        _mockLogger.Verify(x => x.Log(LogLevel.Error, _exception.ToString(), null, LoggingExtensions.Formatter), Times.Once);
    }

    public void LoggerExtensions_LogCritical_CallsLogWithLogLevelCritical()
    {
        _mockLogger.Setup(x => x.Log(LogLevel.Critical, Message, null, LoggingExtensions.Formatter));
        _mockLogger.Object.LogCritical(Message);
        _mockLogger.Verify(x => x.Log(LogLevel.Critical, Message, null, LoggingExtensions.Formatter), Times.Once);
    }

    public async ValueTask LoggerExtensions_LogTraceAsync_CallsLogAsyncWithLogLevelTrace()
    {
        _mockLogger.Setup(x => x.LogAsync(LogLevel.Trace, Message, null, LoggingExtensions.Formatter));
        await _mockLogger.Object.LogTraceAsync(Message);
        _mockLogger.Verify(x => x.LogAsync(LogLevel.Trace, Message, null, LoggingExtensions.Formatter), Times.Once);
    }

    public async ValueTask LoggerExtensions_LogDebugAsync_CallsLogAsyncWithLogLevelDebug()
    {
        _mockLogger.Setup(x => x.LogAsync(LogLevel.Debug, Message, null, LoggingExtensions.Formatter));
        await _mockLogger.Object.LogDebugAsync(Message);
        _mockLogger.Verify(x => x.LogAsync(LogLevel.Debug, Message, null, LoggingExtensions.Formatter), Times.Once);
    }

    public async ValueTask LoggerExtensions_LogInformationAsync_CallsLogAsyncWithLogLevelInformation()
    {
        _mockLogger.Setup(x => x.LogAsync(LogLevel.Information, Message, null, LoggingExtensions.Formatter));
        await _mockLogger.Object.LogInformationAsync(Message);
        _mockLogger.Verify(x => x.LogAsync(LogLevel.Information, Message, null, LoggingExtensions.Formatter), Times.Once);
    }

    public async ValueTask LoggerExtensions_LogWarningAsync_CallsLogAsyncWithLogLevelWarning()
    {
        _mockLogger.Setup(x => x.LogAsync(LogLevel.Warning, Message, null, LoggingExtensions.Formatter));
        await _mockLogger.Object.LogWarningAsync(Message);
        _mockLogger.Verify(x => x.LogAsync(LogLevel.Warning, Message, null, LoggingExtensions.Formatter), Times.Once);
    }

    public async ValueTask LoggerExtensions_LogErrorAsync_CallsLogAsyncWithLogLevelError()
    {
        _mockLogger.Setup(x => x.LogAsync(LogLevel.Error, Message, null, LoggingExtensions.Formatter));
        await _mockLogger.Object.LogErrorAsync(Message);
        _mockLogger.Verify(x => x.LogAsync(LogLevel.Error, Message, null, LoggingExtensions.Formatter), Times.Once);
    }

    public async ValueTask LoggerExtensions_LogErrorAsync_CallsLogAsyncWithLogLevelErrorAndMessageAndException()
    {
        _mockLogger.Setup(x => x.LogAsync(LogLevel.Error, Message, _exception, LoggingExtensions.Formatter));
        await _mockLogger.Object.LogErrorAsync(Message, _exception);
        _mockLogger.Verify(x => x.LogAsync(LogLevel.Error, Message, _exception, LoggingExtensions.Formatter), Times.Once);
    }

    public async ValueTask LoggerExtensions_LogErrorAsync_CallsLogAsyncWithLogLevelErrorAndException()
    {
        _mockLogger.Setup(x => x.LogAsync(LogLevel.Error, _exception.ToString(), null, LoggingExtensions.Formatter));
        await _mockLogger.Object.LogErrorAsync(_exception);
        _mockLogger.Verify(x => x.LogAsync(LogLevel.Error, _exception.ToString(), null, LoggingExtensions.Formatter), Times.Once);
    }

    public async ValueTask LoggerExtensions_LogCriticalAsync_CallsLogAsyncWithLogLevelCritical()
    {
        _mockLogger.Setup(x => x.LogAsync(LogLevel.Critical, Message, null, LoggingExtensions.Formatter));
        await _mockLogger.Object.LogCriticalAsync(Message);
        _mockLogger.Verify(x => x.LogAsync(LogLevel.Critical, Message, null, LoggingExtensions.Formatter), Times.Once);
    }
}
