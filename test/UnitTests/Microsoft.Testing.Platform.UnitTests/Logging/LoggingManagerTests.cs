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
    public void AddProvider_NullFactory_ThrowsArgumentNullException()
    {
        LoggingManager manager = new();
        Assert.ThrowsExactly<ArgumentNullException>(() => manager.AddProvider(null!));
    }

    [TestMethod]
    public async Task BuildAsync_NoProviders_ReturnsNonNullFactory()
    {
        LoggingManager manager = new();
        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);
        Assert.IsNotNull(factory);
        factory.CreateLogger("smoke");
    }

    [TestMethod]
    [DataRow(LogLevel.Trace)]
    [DataRow(LogLevel.Warning)]
    [DataRow(LogLevel.Critical)]
    public async Task BuildAsync_PassesCorrectLogLevelAndServiceProviderToFactory(LogLevel level)
    {
        LogLevel capturedLevel = default;
        IServiceProvider? capturedServiceProvider = null;

        LoggingManager manager = new();
        Mock<ILoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        manager.AddProvider((level, sp) =>
        {
            capturedLevel = level;
            capturedServiceProvider = sp;
            return mockProvider.Object;
        });

        await manager.BuildAsync(_mockServiceProvider.Object, level, _mockMonitor.Object);

        Assert.AreEqual(level, capturedLevel);
        Assert.IsNotNull(capturedServiceProvider, "Factory was not invoked");
        Assert.AreSame(_mockServiceProvider.Object, capturedServiceProvider);
    }

    [TestMethod]
    public async Task BuildAsync_NonExtensionProvider_IsAlwaysIncluded()
    {
        LoggingManager manager = new();
        Mock<ILoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);
        _ = factory.CreateLogger("test");

        mockProvider.Verify(p => p.CreateLogger("test"), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_EnabledExtensionProvider_IsIncluded()
    {
        LoggingManager manager = new();
        Mock<IExtensionLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(true);
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);
        _ = factory.CreateLogger("test");

        mockProvider.Verify(p => p.CreateLogger("test"), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_DisabledExtensionProvider_IsExcluded()
    {
        LoggingManager manager = new();
        Mock<IExtensionLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(false);
        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);
        _ = factory.CreateLogger("test");

        mockProvider.Verify(p => p.CreateLogger(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task BuildAsync_InitializableExtensionProvider_WhenEnabled_CallsInitializeAsync()
    {
        LoggingManager manager = new();
        Mock<IInitializableExtensionLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(true);
        mockProvider.Setup(p => p.InitializeAsync()).Returns(Task.CompletedTask);
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);
        _ = factory.CreateLogger("test");

        mockProvider.Verify(p => p.InitializeAsync(), Times.Once);
        mockProvider.Verify(p => p.CreateLogger("test"), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_InitializableExtensionProvider_WhenDisabled_DoesNotCallInitializeAsync()
    {
        LoggingManager manager = new();
        Mock<IInitializableExtensionLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(false);
        manager.AddProvider((_, _) => mockProvider.Object);

        await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);

        mockProvider.Verify(p => p.InitializeAsync(), Times.Never);
    }

    [TestMethod]
    public async Task BuildAsync_NonExtensionInitializableProvider_CallsInitializeAsync()
    {
        LoggingManager manager = new();
        Mock<IInitializableLoggerProvider> mockProvider = new();
        mockProvider.Setup(p => p.InitializeAsync()).Returns(Task.CompletedTask);
        mockProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
        manager.AddProvider((_, _) => mockProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);
        _ = factory.CreateLogger("test");

        mockProvider.Verify(p => p.InitializeAsync(), Times.Once);
        mockProvider.Verify(p => p.CreateLogger("test"), Times.Once);
    }

    [TestMethod]
    public async Task BuildAsync_MultipleProviders_OnlyIncludesEnabledOnes()
    {
        LoggingManager manager = new();

        Mock<IExtensionLoggerProvider> disabledProvider = new();
        disabledProvider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(false);

        Mock<ILoggerProvider> nonExtensionProvider = new();
        nonExtensionProvider.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        manager.AddProvider((_, _) => disabledProvider.Object);
        manager.AddProvider((_, _) => nonExtensionProvider.Object);

        ILoggerFactory factory = await manager.BuildAsync(_mockServiceProvider.Object, LogLevel.Information, _mockMonitor.Object);
        _ = factory.CreateLogger("test");

        disabledProvider.Verify(p => p.CreateLogger(It.IsAny<string>()), Times.Never);
        nonExtensionProvider.Verify(p => p.CreateLogger("test"), Times.Once);
    }
}

internal interface IExtensionLoggerProvider : ILoggerProvider, IExtension;

internal interface IInitializableLoggerProvider : ILoggerProvider, IAsyncInitializableExtension;

internal interface IInitializableExtensionLoggerProvider : ILoggerProvider, IExtension, IAsyncInitializableExtension;
