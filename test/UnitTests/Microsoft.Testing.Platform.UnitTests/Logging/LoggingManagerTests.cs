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
        => _mockMonitor.Setup(x => x.Lock(It.IsAny<object>())).Returns(new Mock<IDisposable>().Object);

    [TestMethod]
    public void AddProvider_WithNullFactory_ThrowsArgumentNullException()
    {
        LoggingManager manager = new();

        Assert.ThrowsExactly<ArgumentNullException>(() => manager.AddProvider(null!));
    }

    [TestMethod]
    public async Task BuildAsync_WithNoProviders_ReturnsEmptyLoggerFactory()
    {
        LoggingManager manager = new();

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);

        ILogger logger = factory.CreateLogger("test");
        Assert.IsNotNull(logger);
    }

    [TestMethod]
    public async Task BuildAsync_PassesLogLevelAndServiceProviderToFactory()
    {
        LoggingManager manager = new();
        LogLevel capturedLogLevel = default;
        IServiceProvider? capturedServiceProvider = null;
        Mock<ILoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        manager.AddProvider((logLevel, serviceProvider) =>
        {
            capturedLogLevel = logLevel;
            capturedServiceProvider = serviceProvider;
            return mockProvider.Object;
        });

        await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Warning, _mockMonitor.Object);

        Assert.AreEqual(LogLevel.Warning, capturedLogLevel);
        Assert.AreSame(_mockServiceProvider.Object, capturedServiceProvider);
    }

    [TestMethod]
    public async Task BuildAsync_WithNonExtensionProvider_IncludesProvider()
    {
        LoggingManager manager = new();
        Mock<ILogger> mockLogger = new();
        Mock<ILoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.CreateLogger("category")).Returns(mockLogger.Object);

        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);
        _ = factory.CreateLogger("category");

        mockProvider.Verify(p => p.CreateLogger("category"), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_WithEnabledExtensionProvider_IncludesProvider()
    {
        LoggingManager manager = new();
        Mock<ILogger> mockLogger = new();
        Mock<IExtensionLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(true);
        mockProvider.Setup(p => p.CreateLogger("category")).Returns(mockLogger.Object);

        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);
        _ = factory.CreateLogger("category");

        mockProvider.Verify(p => p.CreateLogger("category"), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_WithDisabledExtensionProvider_ExcludesProvider()
    {
        LoggingManager manager = new();
        Mock<IExtensionLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(false);

        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);
        _ = factory.CreateLogger("category");

        mockProvider.Verify(p => p.CreateLogger(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task BuildAsync_WithInitializableProvider_CallsInitializeAsync()
    {
        LoggingManager manager = new();
        Mock<ILogger> mockLogger = new();
        Mock<IInitializableLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.InitializeAsync()).Returns(Task.CompletedTask);
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);

        manager.AddProvider((_, _) => mockProvider.Object);

        await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);

        mockProvider.Verify(p => p.InitializeAsync(), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_WithDisabledInitializableExtensionProvider_DoesNotCallInitializeAsync()
    {
        LoggingManager manager = new();
        Mock<IExtensionInitializableLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(false);

        manager.AddProvider((_, _) => mockProvider.Object);

        await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);

        mockProvider.Verify(p => p.InitializeAsync(), Times.Never);
    }

    [TestMethod]
    public async Task BuildAsync_WithEnabledInitializableExtensionProvider_IncludesAndInitializesProvider()
    {
        LoggingManager manager = new();
        Mock<ILogger> mockLogger = new();
        Mock<IExtensionInitializableLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(true);
        mockProvider.Setup(p => p.InitializeAsync()).Returns(Task.CompletedTask);
        mockProvider.Setup(p => p.CreateLogger("category")).Returns(mockLogger.Object);

        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);
        _ = factory.CreateLogger("category");

        mockProvider.Verify(p => p.InitializeAsync(), Times.Once);
        mockProvider.Verify(p => p.CreateLogger("category"), Times.Once);
    }
}

internal interface IExtensionLoggerProvider : ILoggerProvider, IExtension;

internal interface IInitializableLoggerProvider : ILoggerProvider, IAsyncInitializableExtension;

internal interface IExtensionInitializableLoggerProvider : ILoggerProvider, IExtension, IAsyncInitializableExtension;
