// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        _mockMonitor.Setup(x => x.Lock(It.IsAny<object>())).Returns(new Mock<IDisposable>().Object);
        _mockLoggerProvider.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_mockLogger.Object);

        _loggerProviders =
        [
            _mockLoggerProvider.Object
        ];
    }

    public void LoggerFactory_LoggerCreatedOnlyOnce()
    {
        using LoggerFactory loggerFactory = new(_loggerProviders, LogLevel.Information, _mockMonitor.Object);

        _ = loggerFactory.CreateLogger("test");
        _ = loggerFactory.CreateLogger("test");
        _ = loggerFactory.CreateLogger("test");
        _mockLoggerProvider.Verify(x => x.CreateLogger("test"), Times.Once);
    }
}

internal interface IDisposableLoggerProvider : ILoggerProvider, IDisposable;
