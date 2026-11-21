// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TelemetryManagerBuildTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void TelemetryManager_DisplayNameAndDescription_AreEmpty()
    {
        TelemetryManager manager = new();

        Assert.AreEqual(string.Empty, manager.DisplayName);
        Assert.AreEqual(string.Empty, manager.Description);
    }

    [TestMethod]
    public void AddOpenTelemetryService_FirstRegistrationWins()
    {
        TelemetryManager manager = new();
        IPlatformOpenTelemetryService first = new Mock<IPlatformOpenTelemetryService>().Object;
        IPlatformOpenTelemetryService second = new Mock<IPlatformOpenTelemetryService>().Object;
        manager.AddOpenTelemetryService(_ => first);
        manager.AddOpenTelemetryService(_ => second);

        Assert.AreSame(first, manager.BuildOTelService(new ServiceProvider()));
    }

    [TestMethod]
    public void BuildOTelProvider_WithoutRegistration_ReturnsNull()
        => Assert.IsNull(new TelemetryManager().BuildOTelProvider(new ServiceProvider()));

    [TestMethod]
    public void BuildOTelProvider_WithRegistration_ReturnsRegisteredProvider()
    {
        TelemetryManager manager = new();
        IOpenTelemetryProvider provider = new Mock<IOpenTelemetryProvider>().Object;
        manager.AddOpenTelemetryProvider(_ => provider);

        Assert.AreSame(provider, manager.BuildOTelProvider(new ServiceProvider()));
    }

    [TestMethod]
    public async Task BuildTelemetryAsync_OptedInWithFactory_ReturnsFactoryCollector()
    {
        (ServiceProvider serviceProvider, RecordingLogger logger, ILoggerFactory loggerFactory) = CreateContext();
        ITelemetryCollector collector = new Mock<ITelemetryCollector>().Object;
        TelemetryManager manager = new();
        manager.AddTelemetryCollectorProvider(_ => collector);

        ITelemetryCollector result = await manager.BuildTelemetryAsync(serviceProvider, loggerFactory, new TestApplicationOptions { EnableTelemetry = true });

        Assert.AreSame(collector, result);
        Assert.Contains(m => m.Contains("Telemetry collector provider:", StringComparison.Ordinal), logger.Messages);
    }

    [TestMethod]
    public async Task BuildTelemetryAsync_OptedInWithoutFactory_ReturnsEnabledNopCollector()
    {
        (ServiceProvider serviceProvider, RecordingLogger logger, ILoggerFactory loggerFactory) = CreateContext();

        ITelemetryCollector result = await new TelemetryManager().BuildTelemetryAsync(serviceProvider, loggerFactory, new TestApplicationOptions { EnableTelemetry = true });

        Assert.IsInstanceOfType<NopTelemetryService>(result);
        await result.LogEventAsync("event", new Dictionary<string, object>(), TestContext.CancellationToken);
        Assert.Contains(m => m.Contains("Telemetry collector provider:", StringComparison.Ordinal), logger.Messages);
    }

    [TestMethod]
    public async Task BuildTelemetryAsync_OptedOutWithFactory_ReturnsDisabledNopCollectorAndDoesNotLogProvider()
    {
        (ServiceProvider serviceProvider, RecordingLogger logger, ILoggerFactory loggerFactory) = CreateContext();
        bool factoryCalled = false;
        TelemetryManager manager = new();
        manager.AddTelemetryCollectorProvider(_ =>
        {
            factoryCalled = true;
            return new Mock<ITelemetryCollector>().Object;
        });

        ITelemetryCollector result = await manager.BuildTelemetryAsync(serviceProvider, loggerFactory, new TestApplicationOptions { EnableTelemetry = false });

        Assert.IsFalse(factoryCalled);
        Assert.IsInstanceOfType<NopTelemetryService>(result);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => result.LogEventAsync("event", new Dictionary<string, object>(), TestContext.CancellationToken));
        Assert.DoesNotContain(m => m.Contains("Telemetry collector provider:", StringComparison.Ordinal), logger.Messages);
    }

    [TestMethod]
    public async Task BuildTelemetryAsync_FirstRunWithoutApplicationPath_UsesPlainSentinelFileName()
    {
        (ServiceProvider serviceProvider, _, ILoggerFactory loggerFactory) = CreateContext(noBanner: false);
        Mock<IFileSystem> fileSystem = new();
        serviceProvider.AddService(fileSystem.Object);
        TelemetryManager manager = new();
        manager.AddTelemetryCollectorProvider(_ => new Mock<ITelemetryCollector>().Object);

        await manager.BuildTelemetryAsync(serviceProvider, loggerFactory, new TestApplicationOptions { EnableTelemetry = true });

        string expected = Path.Combine("sentinelDir", "Microsoft", "TestingPlatform", "testingPlatformFirstTimeUseSentinel");
        fileSystem.Verify(f => f.ExistFile(expected), Times.Once);
        fileSystem.Verify(f => f.NewFileStream(expected, FileMode.Create, FileAccess.Write), Times.Once);
    }

    [TestMethod]
    public async Task BuildTelemetryAsync_SentinelWriteFailsWithIOOrOtherSystemException_LogsErrorAndContinues()
    {
        foreach (Exception failure in new Exception[] { new IOException("io"), new UnauthorizedAccessException("denied") })
        {
            (ServiceProvider serviceProvider, RecordingLogger logger, ILoggerFactory loggerFactory) = CreateContext(noBanner: false);
            Mock<IFileSystem> fileSystem = new();
            fileSystem.Setup(f => f.CreateDirectory(It.IsAny<string>())).Throws(failure);
            serviceProvider.AddService(fileSystem.Object);
            ITelemetryCollector collector = new Mock<ITelemetryCollector>().Object;
            TelemetryManager manager = new();
            manager.AddTelemetryCollectorProvider(_ => collector);

            ITelemetryCollector result = await manager.BuildTelemetryAsync(serviceProvider, loggerFactory, new TestApplicationOptions { EnableTelemetry = true });

            Assert.AreSame(collector, result);
            Assert.Contains(LogLevel.Error, logger.Levels, failure.GetType().Name);
        }
    }

    [TestMethod]
    public async Task BuildTelemetryAsync_SentinelWriteFailsWithNonSystemException_Propagates()
    {
        (ServiceProvider serviceProvider, _, ILoggerFactory loggerFactory) = CreateContext(noBanner: false);
        Mock<IFileSystem> fileSystem = new();
        fileSystem.Setup(f => f.CreateDirectory(It.IsAny<string>())).Throws(new CustomFailure());
        serviceProvider.AddService(fileSystem.Object);
        TelemetryManager manager = new();
        manager.AddTelemetryCollectorProvider(_ => new Mock<ITelemetryCollector>().Object);

        await Assert.ThrowsExactlyAsync<CustomFailure>(() => manager.BuildTelemetryAsync(serviceProvider, loggerFactory, new TestApplicationOptions { EnableTelemetry = true }));
    }

    private static (ServiceProvider ServiceProvider, RecordingLogger Logger, ILoggerFactory LoggerFactory) CreateContext(bool noBanner = true)
    {
        Mock<IEnvironment> environment = new();
        environment.Setup(e => e.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_NOBANNER)).Returns(noBanner ? "1" : null);
        environment.Setup(e => e.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create)).Returns("sentinelDir");
        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(environment.Object);
        serviceProvider.AddService(new Mock<Platform.CommandLine.ICommandLineOptions>().Object);
        serviceProvider.AddService(new Mock<ITestApplicationModuleInfo>().Object);
        serviceProvider.AddService(new Mock<IOutputDevice>().Object);
        serviceProvider.AddService(new Mock<ITestApplicationCancellationTokenSource>().Object);
        RecordingLogger logger = new();
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(logger);
        return (serviceProvider, logger, loggerFactory.Object);
    }

    private sealed class CustomFailure : Exception;

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public List<LogLevel> Levels { get; } = [];

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Levels.Add(logLevel);
            Messages.Add(formatter(state, exception));
        }

        public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Log(logLevel, state, exception, formatter);
            return Task.CompletedTask;
        }
    }
}
