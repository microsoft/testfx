// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Microsoft.Testing.Extensions.Logging;

using Moq;

using MelILogger = Microsoft.Extensions.Logging.ILogger;
using MelLogLevel = Microsoft.Extensions.Logging.LogLevel;
using MtpLogLevel = Microsoft.Testing.Platform.Logging.LogLevel;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class MicrosoftExtensionsLoggingBridgeTests
{
    [TestMethod]
    public void NopLoggerProvider_Instance_IsSingleton()
    {
        NopLoggerProvider first = NopLoggerProvider.Instance;
        NopLoggerProvider second = NopLoggerProvider.Instance;
        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void NopLoggerProvider_CreateLogger_ReturnsNonNullLoggerRegardlessOfCategory()
    {
        Platform.Logging.ILogger logger1 = NopLoggerProvider.Instance.CreateLogger("SomeCategory");
        Platform.Logging.ILogger logger2 = NopLoggerProvider.Instance.CreateLogger("AnotherCategory");

        Assert.IsNotNull(logger1);
        Assert.AreSame(logger1, logger2);
    }

    [TestMethod]
    public void NopLoggerProvider_Logger_IsEnabled_AlwaysFalse()
    {
        Platform.Logging.ILogger logger = NopLoggerProvider.Instance.CreateLogger("Category");

        foreach (MtpLogLevel level in (MtpLogLevel[])Enum.GetValues(typeof(MtpLogLevel)))
        {
            Assert.IsFalse(logger.IsEnabled(level), $"NopLoggerProvider's logger should never be enabled for {level}.");
        }
    }

    [TestMethod]
    public void NopLoggerProvider_Logger_Log_DoesNotThrow()
    {
        Platform.Logging.ILogger logger = NopLoggerProvider.Instance.CreateLogger("Category");

        // Should be a true no-op: no exception, no callback invocation required.
        logger.Log(MtpLogLevel.Error, "state", new InvalidOperationException("boom"), (s, e) => throw new InvalidOperationException("Formatter should not be invoked by NopLogger."));
    }

    [TestMethod]
    public async Task NopLoggerProvider_Logger_LogAsync_CompletesSynchronously()
    {
        Platform.Logging.ILogger logger = NopLoggerProvider.Instance.CreateLogger("Category");

        Task task = logger.LogAsync(MtpLogLevel.Warning, "state", exception: null, (s, e) => throw new InvalidOperationException("Formatter should not be invoked by NopLogger."));

        Assert.IsTrue(task.IsCompletedSuccessfully);
        await task;
    }

    [TestMethod]
    public void MicrosoftExtensionsLoggerAdapter_Constructor_ThrowsForNullInnerLogger()
        => Assert.ThrowsExactly<ArgumentNullException>(() => _ = new MicrosoftExtensionsLoggerAdapter(null!));

    [TestMethod]
    [DataRow(MtpLogLevel.Trace, MelLogLevel.Trace)]
    [DataRow(MtpLogLevel.Debug, MelLogLevel.Debug)]
    [DataRow(MtpLogLevel.Information, MelLogLevel.Information)]
    [DataRow(MtpLogLevel.Warning, MelLogLevel.Warning)]
    [DataRow(MtpLogLevel.Error, MelLogLevel.Error)]
    [DataRow(MtpLogLevel.Critical, MelLogLevel.Critical)]
    [DataRow(MtpLogLevel.None, MelLogLevel.None)]
    public void MicrosoftExtensionsLoggerAdapter_IsEnabled_ForwardsMappedLevelToInnerLogger(MtpLogLevel mtpLevel, MelLogLevel expectedMelLevel)
    {
        var innerLoggerMock = new Mock<MelILogger>();
        innerLoggerMock.Setup(l => l.IsEnabled(expectedMelLevel)).Returns(true);
        var adapter = new MicrosoftExtensionsLoggerAdapter(innerLoggerMock.Object);

        bool result = adapter.IsEnabled(mtpLevel);

        Assert.IsTrue(result);
        innerLoggerMock.Verify(l => l.IsEnabled(expectedMelLevel), Times.Once);
    }

    [TestMethod]
    public void MicrosoftExtensionsLoggerAdapter_Log_ForwardsStateExceptionAndFormatterToInnerLogger()
    {
        var innerLoggerMock = new Mock<MelILogger>();
        var adapter = new MicrosoftExtensionsLoggerAdapter(innerLoggerMock.Object);
        var exception = new InvalidOperationException("boom");
        const string state = "my-state";

        adapter.Log(MtpLogLevel.Error, state, exception, (s, e) => $"{s}-{e?.Message}");

        innerLoggerMock.Verify(
            l => l.Log(
                MelLogLevel.Error,
                It.IsAny<EventId>(),
                state,
                exception,
                It.IsAny<Func<string, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task MicrosoftExtensionsLoggerAdapter_LogAsync_ForwardsSynchronouslyAndCompletes()
    {
        var innerLoggerMock = new Mock<MelILogger>();
        var adapter = new MicrosoftExtensionsLoggerAdapter(innerLoggerMock.Object);

        Task task = adapter.LogAsync(MtpLogLevel.Information, "state", exception: null, (s, e) => s.ToString() ?? string.Empty);

        Assert.IsTrue(task.IsCompletedSuccessfully);
        await task;
        innerLoggerMock.Verify(
            l => l.Log(
                MelLogLevel.Information,
                It.IsAny<EventId>(),
                "state",
                null,
                It.IsAny<Func<string, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public void MicrosoftExtensionsLoggingProvider_Constructor_ThrowsForNullLoggerFactory()
        => Assert.ThrowsExactly<ArgumentNullException>(() => _ = new MicrosoftExtensionsLoggingProvider(null!, ownsFactory: true));

    [TestMethod]
    public void MicrosoftExtensionsLoggingProvider_CreateLogger_ReturnsAdapterWrappingFactoryLogger()
    {
        var innerLoggerMock = new Mock<MelILogger>();
        var factoryMock = new Mock<ILoggerFactory>();
        factoryMock.Setup(f => f.CreateLogger("MyCategory")).Returns(innerLoggerMock.Object);
        var provider = new MicrosoftExtensionsLoggingProvider(factoryMock.Object, ownsFactory: false);

        Platform.Logging.ILogger logger = provider.CreateLogger("MyCategory");

        Assert.IsInstanceOfType<MicrosoftExtensionsLoggerAdapter>(logger);
        factoryMock.Verify(f => f.CreateLogger("MyCategory"), Times.Once);
    }

    [TestMethod]
    public void MicrosoftExtensionsLoggingProvider_CreateLogger_ThrowsObjectDisposedExceptionAfterDispose()
    {
        var factoryMock = new Mock<ILoggerFactory>();
        var provider = new MicrosoftExtensionsLoggingProvider(factoryMock.Object, ownsFactory: false);

        provider.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = provider.CreateLogger("Category"));
    }

    [TestMethod]
    public void MicrosoftExtensionsLoggingProvider_Dispose_DisposesFactoryWhenOwnsFactoryIsTrue()
    {
        var factoryMock = new Mock<ILoggerFactory>();
        factoryMock.As<IDisposable>().Setup(d => d.Dispose());
        var provider = new MicrosoftExtensionsLoggingProvider(factoryMock.Object, ownsFactory: true);

        provider.Dispose();

        factoryMock.As<IDisposable>().Verify(d => d.Dispose(), Times.Once);
    }

    [TestMethod]
    public void MicrosoftExtensionsLoggingProvider_Dispose_DoesNotDisposeFactoryWhenOwnsFactoryIsFalse()
    {
        var factoryMock = new Mock<ILoggerFactory>();
        factoryMock.As<IDisposable>().Setup(d => d.Dispose());
        var provider = new MicrosoftExtensionsLoggingProvider(factoryMock.Object, ownsFactory: false);

        provider.Dispose();

        factoryMock.As<IDisposable>().Verify(d => d.Dispose(), Times.Never);
    }

    [TestMethod]
    public void MicrosoftExtensionsLoggingProvider_Dispose_IsIdempotent()
    {
        var factoryMock = new Mock<ILoggerFactory>();
        factoryMock.As<IDisposable>().Setup(d => d.Dispose());
        var provider = new MicrosoftExtensionsLoggingProvider(factoryMock.Object, ownsFactory: true);

        provider.Dispose();
        provider.Dispose();

        factoryMock.As<IDisposable>().Verify(d => d.Dispose(), Times.Once);
    }

#if NETCOREAPP
    [TestMethod]
    public async Task MicrosoftExtensionsLoggingProvider_DisposeAsync_PrefersAsyncDisposableWhenOwningFactory()
    {
        var factoryMock = new Mock<ILoggerFactory>();
        Mock<IAsyncDisposable> asyncDisposableMock = factoryMock.As<IAsyncDisposable>();
        factoryMock.As<IDisposable>();
        asyncDisposableMock.Setup(d => d.DisposeAsync()).Returns(ValueTask.CompletedTask);
        var provider = new MicrosoftExtensionsLoggingProvider(factoryMock.Object, ownsFactory: true);

        await provider.DisposeAsync();

        asyncDisposableMock.Verify(d => d.DisposeAsync(), Times.Once);
        factoryMock.As<IDisposable>().Verify(d => d.Dispose(), Times.Never);
    }

    [TestMethod]
    public async Task MicrosoftExtensionsLoggingProvider_DisposeAsync_DoesNothingWhenNotOwningFactory()
    {
        var factoryMock = new Mock<ILoggerFactory>();
        Mock<IAsyncDisposable> asyncDisposableMock = factoryMock.As<IAsyncDisposable>();
        factoryMock.As<IDisposable>();
        var provider = new MicrosoftExtensionsLoggingProvider(factoryMock.Object, ownsFactory: false);

        await provider.DisposeAsync();

        asyncDisposableMock.Verify(d => d.DisposeAsync(), Times.Never);
        factoryMock.As<IDisposable>().Verify(d => d.Dispose(), Times.Never);
    }

    [TestMethod]
    public async Task MicrosoftExtensionsLoggingProvider_DisposeAsync_IsIdempotent()
    {
        var factoryMock = new Mock<ILoggerFactory>();
        Mock<IAsyncDisposable> asyncDisposableMock = factoryMock.As<IAsyncDisposable>();
        factoryMock.As<IDisposable>();
        asyncDisposableMock.Setup(d => d.DisposeAsync()).Returns(ValueTask.CompletedTask);
        var provider = new MicrosoftExtensionsLoggingProvider(factoryMock.Object, ownsFactory: true);

        await provider.DisposeAsync();
        await provider.DisposeAsync();

        asyncDisposableMock.Verify(d => d.DisposeAsync(), Times.Once);
    }
#endif
}
