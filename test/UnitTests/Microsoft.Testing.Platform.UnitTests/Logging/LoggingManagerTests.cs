// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class LoggingManagerTests
{
    private readonly Mock<IMonitor> _mockMonitor = new();
    private readonly Mock<IServiceProvider> _mockServiceProvider = new();

    public LoggingManagerTests()
        => _mockMonitor.Setup(m => m.Lock(It.IsAny<object>())).Returns(new Mock<IDisposable>().Object);

    [TestMethod]
    public void AddProvider_WithNull_ThrowsArgumentNullException()
    {
        LoggingManager manager = new();

        Assert.ThrowsExactly<ArgumentNullException>(() => manager.AddProvider(null!));
    }

    [TestMethod]
    public async Task BuildAsync_WithNonExtensionProvider_IncludesProvider()
    {
        Mock<ILoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Debug, _mockMonitor.Object);
        factory.CreateLogger("test");

        mockProvider.Verify(p => p.CreateLogger("test"), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_WithEnabledExtensionProvider_IncludesProvider()
    {
        Mock<IExtensionLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(true);
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Debug, _mockMonitor.Object);
        factory.CreateLogger("test");

        mockProvider.Verify(p => p.CreateLogger("test"), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_WithDisabledExtensionProvider_ExcludesProvider()
    {
        Mock<IExtensionLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(false);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Debug, _mockMonitor.Object);
        factory.CreateLogger("test");

        mockProvider.Verify(p => p.CreateLogger(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task BuildAsync_WithNonExtensionInitializableProvider_CallsInitializeAsync()
    {
        Mock<IInitializableLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
        mockProvider.Setup(p => p.InitializeAsync()).Returns(Task.CompletedTask);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Debug, _mockMonitor.Object);
        factory.CreateLogger("test");

        mockProvider.Verify(p => p.InitializeAsync(), Times.Once);
        mockProvider.Verify(p => p.CreateLogger("test"), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_WithDisabledExtensionProvider_DoesNotCallInitializeAsync()
    {
        Mock<IExtensionAndInitializableLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(false);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Debug, _mockMonitor.Object);

        mockProvider.Verify(p => p.InitializeAsync(), Times.Never);
    }

    [TestMethod]
    public async Task BuildAsync_WithEnabledExtensionAndInitializableProvider_CallsInitializeAsync()
    {
        Mock<IExtensionAndInitializableLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(true);
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
        mockProvider.Setup(p => p.InitializeAsync()).Returns(Task.CompletedTask);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Debug, _mockMonitor.Object);
        factory.CreateLogger("test");

        mockProvider.Verify(p => p.InitializeAsync(), Times.Once);
        mockProvider.Verify(p => p.CreateLogger("test"), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_PassesLogLevelAndServiceProviderToFactory()
    {
        IServiceProvider? capturedServiceProvider = null;
        LogLevel capturedLogLevel = default;
        Mock<ILoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        LoggingManager manager = new();
        manager.AddProvider((level, sp) =>
        {
            capturedLogLevel = level;
            capturedServiceProvider = sp;
            return mockProvider.Object;
        });

        await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Warning, _mockMonitor.Object);

        Assert.AreEqual(LogLevel.Warning, capturedLogLevel);
        Assert.AreSame(_mockServiceProvider.Object, capturedServiceProvider);
    }
}

internal interface IExtensionLoggerProvider : ILoggerProvider, IExtension;

internal interface IInitializableLoggerProvider : ILoggerProvider, IAsyncInitializableExtension;

internal interface IExtensionAndInitializableLoggerProvider : ILoggerProvider, IExtension, IAsyncInitializableExtension;
