// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Framework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public class LoggerFactoryTests : TestBase
{
    private readonly Mock<ILogger> _mockLogger = new();
    private readonly Mock<IMonitor> _mockMonitor = new();
    private readonly Mock<IDisposableLoggerProvider> _mockLoggerProvider = new();
    private readonly ILoggerProvider[] _loggerProviders;

    public LoggerFactoryTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
        _mockLogger.Setup(x => x.Log(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<Func<string, Exception?, string>>()))
            .Callback(() => { });
        _mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        _mockMonitor.Setup(x => x.Lock(It.IsAny<object>())).Returns(new Mock<IDisposable>().Object);
        _mockLoggerProvider.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_mockLogger.Object);

        _loggerProviders = new[]
        {
            _mockLoggerProvider.Object,
        };
    }

    public void LoggerFactory_WriteLogFromFactory()
    {
        const string message = "hello";
        Func<string, Exception?, string> formatter = (state, ex) => state;

        using LoggerFactory loggerFactory = new(_loggerProviders, LogLevel.Information, _mockMonitor.Object);
        ILogger logger = loggerFactory.CreateLogger("test");
        logger.Log(LogLevel.Information, message, null, formatter);

        _mockLogger.Verify(x => x.Log(LogLevel.Information, message, null, formatter), Times.Once);
    }
}

internal interface IDisposableLoggerProvider : ILoggerProvider, IDisposable
{
}
