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
    private readonly Mock<IServiceProvider> _mockServiceProvider = new();
    private readonly Mock<IMonitor> _mockMonitor = new();

    public LoggingManagerTests()
        => _mockMonitor.Setup(x => x.Lock(It.IsAny<object>())).Returns(new Mock<IDisposable>().Object);

    [TestMethod]
    public async Task BuildAsync_WithNoProviders_ReturnsFactoryThatCreatesLogger()
    {
        LoggingManager manager = new();

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);

        Assert.IsNotNull(factory.CreateLogger("category"));
    }

    [TestMethod]
    public async Task BuildAsync_WithNonExtensionProvider_IncludesProvider()
    {
        Mock<ILoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);
        _ = factory.CreateLogger("category");

        mockProvider.Verify(p => p.CreateLogger("category"), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_WithEnabledExtensionProvider_IncludesProvider()
    {
        Mock<IExtensionLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(true);
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);
        _ = factory.CreateLogger("category");

        mockProvider.Verify(p => p.CreateLogger("category"), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_WithDisabledExtensionProvider_ExcludesProvider()
    {
        Mock<IExtensionLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(false);
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);
        _ = factory.CreateLogger("category");

        mockProvider.Verify(p => p.CreateLogger(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task BuildAsync_WithDisabledExtensionProvider_DoesNotCallInitialize()
    {
        Mock<IExtensionInitializableLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(false);
        mockProvider.Setup(p => p.InitializeAsync()).Returns(Task.CompletedTask);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        _ = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);

        mockProvider.Verify(p => p.IsEnabledAsync(), Times.Once);
        mockProvider.Verify(p => p.InitializeAsync(), Times.Never);
    }

    [TestMethod]
    public async Task BuildAsync_WithEnabledInitializableExtensionProvider_CallsInitialize()
    {
        Mock<IExtensionInitializableLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(true);
        mockProvider.Setup(p => p.InitializeAsync()).Returns(Task.CompletedTask);
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        _ = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);

        mockProvider.Verify(p => p.InitializeAsync(), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_WithNonExtensionInitializableProvider_CallsInitialize()
    {
        Mock<IInitializableLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.InitializeAsync()).Returns(Task.CompletedTask);
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => mockProvider.Object);

        _ = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);

        mockProvider.Verify(p => p.InitializeAsync(), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_PassesLogLevelAndServiceProviderToFactory()
    {
        LogLevel capturedLogLevel = LogLevel.None;
        IServiceProvider? capturedServiceProvider = null;

        Mock<ILoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        LoggingManager manager = new();
        manager.AddProvider((level, sp) =>
        {
            capturedLogLevel = level;
            capturedServiceProvider = sp;
            return mockProvider.Object;
        });

        _ = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Warning, _mockMonitor.Object);

        Assert.AreEqual(LogLevel.Warning, capturedLogLevel);
        Assert.AreSame(_mockServiceProvider.Object, capturedServiceProvider);
    }

    [TestMethod]
    public async Task BuildAsync_WithMixedProviders_IncludesOnlyEnabledOnes()
    {
        Mock<ILoggerProvider> nonExtProvider = new();
        Mock<IExtensionLoggerProvider> enabledExtProvider = new();
        Mock<IExtensionLoggerProvider> disabledExtProvider = new();

        Mock<ILogger> mockLogger = new();
        nonExtProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);
        enabledExtProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(true);
        enabledExtProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);
        disabledExtProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(false);

        LoggingManager manager = new();
        manager.AddProvider((_, _) => nonExtProvider.Object);
        manager.AddProvider((_, _) => enabledExtProvider.Object);
        manager.AddProvider((_, _) => disabledExtProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);
        _ = factory.CreateLogger("category");

        nonExtProvider.Verify(p => p.CreateLogger("category"), Times.Once);
        enabledExtProvider.Verify(p => p.CreateLogger("category"), Times.Once);
        disabledExtProvider.Verify(p => p.CreateLogger(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public void AddProvider_WithNullFactory_ThrowsArgumentNullException()
    {
        LoggingManager manager = new();

        Assert.ThrowsExactly<ArgumentNullException>(() => manager.AddProvider(null!));
    }

    internal interface IExtensionLoggerProvider : ILoggerProvider, IExtension;

    internal interface IExtensionInitializableLoggerProvider : ILoggerProvider, IExtension, IAsyncInitializableExtension;

    internal interface IInitializableLoggerProvider : ILoggerProvider, IAsyncInitializableExtension;
}
