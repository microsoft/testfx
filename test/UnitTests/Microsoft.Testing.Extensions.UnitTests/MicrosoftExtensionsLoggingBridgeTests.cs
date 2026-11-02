// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Logging;

using Moq;

using MelEventId = Microsoft.Extensions.Logging.EventId;
using MelILogger = Microsoft.Extensions.Logging.ILogger;
using MelILoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;
using MelLogLevel = Microsoft.Extensions.Logging.LogLevel;
using MtpILogger = Microsoft.Testing.Platform.Logging.ILogger;
using MtpLogLevel = Microsoft.Testing.Platform.Logging.LogLevel;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class MicrosoftExtensionsLoggingBridgeTests
{
    [TestMethod]
    public void NopLoggerProvider_Instance_ReturnsSameProvider()
    {
        NopLoggerProvider first = NopLoggerProvider.Instance;
        NopLoggerProvider second = NopLoggerProvider.Instance;

        Assert.AreSame(first, second);
    }

    [DataRow("first-category")]
    [DataRow("second-category")]
    [DataRow(null)]
    [TestMethod]
    public void NopLoggerProvider_CreateLogger_ReturnsSameLoggerForEveryCategory(string? categoryName)
    {
        MtpILogger baseline = NopLoggerProvider.Instance.CreateLogger("baseline");

        MtpILogger logger = NopLoggerProvider.Instance.CreateLogger(categoryName!);

        Assert.IsNotNull(logger);
        Assert.AreSame(baseline, logger);
    }

    [DataRow(MtpLogLevel.Trace)]
    [DataRow(MtpLogLevel.Debug)]
    [DataRow(MtpLogLevel.Information)]
    [DataRow(MtpLogLevel.Warning)]
    [DataRow(MtpLogLevel.Error)]
    [DataRow(MtpLogLevel.Critical)]
    [DataRow(MtpLogLevel.None)]
    [DataRow((MtpLogLevel)int.MaxValue)]
    [TestMethod]
    public void NopLogger_IsEnabled_ReturnsFalseForEveryLogLevel(MtpLogLevel logLevel)
    {
        MtpILogger logger = NopLoggerProvider.Instance.CreateLogger("category");

        Assert.IsFalse(logger.IsEnabled(logLevel));
    }

    [TestMethod]
    public void NopLogger_Log_DoesNotInvokeFormatter()
    {
        MtpILogger logger = NopLoggerProvider.Instance.CreateLogger("category");
        var exception = new InvalidOperationException("failure");
        int formatterCallCount = 0;
        Func<string, Exception?, string> formatter = (_, _) =>
        {
            formatterCallCount++;
            throw new InvalidOperationException("The no-op logger must not invoke the formatter.");
        };

        logger.Log(MtpLogLevel.Error, "state", exception, formatter);

        Assert.AreEqual(0, formatterCallCount);
    }

    [TestMethod]
    public async Task NopLogger_LogAsync_ReturnsCompletedTaskWithoutInvokingFormatter()
    {
        MtpILogger logger = NopLoggerProvider.Instance.CreateLogger("category");
        var exception = new InvalidOperationException("failure");
        int formatterCallCount = 0;
        Func<string, Exception?, string> formatter = (_, _) =>
        {
            formatterCallCount++;
            throw new InvalidOperationException("The no-op logger must not invoke the formatter.");
        };

        Task task = logger.LogAsync(MtpLogLevel.Error, "state", exception, formatter);

        Assert.AreSame(Task.CompletedTask, task);
        Assert.AreEqual(TaskStatus.RanToCompletion, task.Status);
        Assert.AreEqual(0, formatterCallCount);

        await task;

        Assert.AreEqual(0, formatterCallCount);
    }

    [TestMethod]
    public void MicrosoftExtensionsLoggerAdapter_Constructor_NullInnerThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => new MicrosoftExtensionsLoggerAdapter(null!));

        Assert.AreEqual("inner", exception.ParamName);
    }

    [DataRow(MtpLogLevel.Trace, MelLogLevel.Trace, true)]
    [DataRow(MtpLogLevel.Debug, MelLogLevel.Debug, false)]
    [DataRow(MtpLogLevel.Information, MelLogLevel.Information, true)]
    [DataRow(MtpLogLevel.Warning, MelLogLevel.Warning, false)]
    [DataRow(MtpLogLevel.Error, MelLogLevel.Error, true)]
    [DataRow(MtpLogLevel.Critical, MelLogLevel.Critical, false)]
    [DataRow(MtpLogLevel.None, MelLogLevel.None, true)]
    [DataRow((MtpLogLevel)int.MaxValue, MelLogLevel.None, false)]
    [TestMethod]
    public void MicrosoftExtensionsLoggerAdapter_IsEnabled_ForwardsMappedLevelAndInnerResult(
        MtpLogLevel mtpLogLevel,
        MelLogLevel expectedMelLogLevel,
        bool innerResult)
    {
        Mock<MelILogger> loggerMock = new();
        loggerMock.Setup(logger => logger.IsEnabled(expectedMelLogLevel)).Returns(innerResult);
        var adapter = new MicrosoftExtensionsLoggerAdapter(loggerMock.Object);

        bool result = adapter.IsEnabled(mtpLogLevel);

        Assert.AreEqual(innerResult, result);
        loggerMock.Verify(logger => logger.IsEnabled(expectedMelLogLevel), Times.Once);
    }

    [TestMethod]
    public void MicrosoftExtensionsLoggerAdapter_Log_ForwardsExactArgumentsAndFormatter()
    {
        Mock<MelILogger> loggerMock = new();
        object state = new();
        var exception = new InvalidOperationException("failure");
        Func<object, Exception?, string> formatter = (_, error) => $"formatted:{error?.Message}";
        MelEventId capturedEventId = default;
        object? capturedState = null;
        Exception? capturedException = null;
        Func<object, Exception?, string>? capturedFormatter = null;
        string? formattedMessage = null;
        loggerMock
            .Setup(logger => logger.Log(
                MelLogLevel.Warning,
                It.IsAny<MelEventId>(),
                It.IsAny<object>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<object, Exception?, string>>()))
            .Callback<MelLogLevel, MelEventId, object, Exception?, Func<object, Exception?, string>>(
                (_, eventId, candidateState, candidateException, candidateFormatter) =>
                {
                    capturedEventId = eventId;
                    capturedState = candidateState;
                    capturedException = candidateException;
                    capturedFormatter = candidateFormatter;
                    formattedMessage = candidateFormatter(candidateState, candidateException);
                });
        var adapter = new MicrosoftExtensionsLoggerAdapter(loggerMock.Object);

        adapter.Log(MtpLogLevel.Warning, state, exception, formatter);

        Assert.AreEqual(0, capturedEventId.Id);
        Assert.IsNull(capturedEventId.Name);
        Assert.AreSame(state, capturedState);
        Assert.AreSame(exception, capturedException);
        Assert.AreSame(formatter, capturedFormatter);
        Assert.AreEqual("formatted:failure", formattedMessage);
        loggerMock.Verify(
            logger => logger.Log(
                MelLogLevel.Warning,
                It.Is<MelEventId>(eventId => eventId.Id == 0 && eventId.Name == null),
                It.Is<object>(candidateState => ReferenceEquals(candidateState, state)),
                It.Is<Exception?>(candidateException => ReferenceEquals(candidateException, exception)),
                It.Is<Func<object, Exception?, string>>(candidateFormatter => ReferenceEquals(candidateFormatter, formatter))),
            Times.Once);
    }

    [TestMethod]
    public void MicrosoftExtensionsLoggerAdapter_Log_WhenInnerIsDisabledStillForwardsWithoutCheckingIsEnabled()
    {
        Mock<MelILogger> loggerMock = new();
        loggerMock.Setup(logger => logger.IsEnabled(MelLogLevel.Error)).Returns(false);
        var adapter = new MicrosoftExtensionsLoggerAdapter(loggerMock.Object);

        adapter.Log(MtpLogLevel.Error, "state", null, static (state, _) => state);

        loggerMock.Verify(
            logger => logger.Log(
                MelLogLevel.Error,
                It.Is<MelEventId>(eventId => eventId.Id == 0 && eventId.Name == null),
                "state",
                null,
                It.IsAny<Func<string, Exception?, string>>()),
            Times.Once);
        loggerMock.Verify(logger => logger.IsEnabled(It.IsAny<MelLogLevel>()), Times.Never);
    }

    [TestMethod]
    public async Task MicrosoftExtensionsLoggerAdapter_LogAsync_ForwardsSynchronouslyAndReturnsCompletedTask()
    {
        Mock<MelILogger> loggerMock = new();
        object state = new();
        var exception = new InvalidOperationException("failure");
        Func<object, Exception?, string> formatter = static (_, error) => error?.Message ?? string.Empty;
        bool forwarded = false;
        loggerMock
            .Setup(logger => logger.Log(
                MelLogLevel.Critical,
                It.Is<MelEventId>(eventId => eventId.Id == 0 && eventId.Name == null),
                It.Is<object>(candidateState => ReferenceEquals(candidateState, state)),
                It.Is<Exception?>(candidateException => ReferenceEquals(candidateException, exception)),
                It.Is<Func<object, Exception?, string>>(candidateFormatter => ReferenceEquals(candidateFormatter, formatter))))
            .Callback(() => forwarded = true);
        var adapter = new MicrosoftExtensionsLoggerAdapter(loggerMock.Object);

        Task task = adapter.LogAsync(MtpLogLevel.Critical, state, exception, formatter);

        Assert.IsTrue(forwarded);
        Assert.AreSame(Task.CompletedTask, task);
        Assert.AreEqual(TaskStatus.RanToCompletion, task.Status);
        loggerMock.Verify(
            logger => logger.Log(
                MelLogLevel.Critical,
                It.Is<MelEventId>(eventId => eventId.Id == 0 && eventId.Name == null),
                It.Is<object>(candidateState => ReferenceEquals(candidateState, state)),
                It.Is<Exception?>(candidateException => ReferenceEquals(candidateException, exception)),
                It.Is<Func<object, Exception?, string>>(candidateFormatter => ReferenceEquals(candidateFormatter, formatter))),
            Times.Once);
        loggerMock.Verify(logger => logger.IsEnabled(It.IsAny<MelLogLevel>()), Times.Never);

        await task;
    }

    [TestMethod]
    public void MicrosoftExtensionsLoggingProvider_Constructor_NullFactoryThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => new MicrosoftExtensionsLoggingProvider(null!, ownsFactory: false));

        Assert.AreEqual("loggerFactory", exception.ParamName);
    }

    [DataRow("normal-category")]
    [DataRow("")]
    [DataRow(null)]
    [TestMethod]
    public void MicrosoftExtensionsLoggingProvider_CreateLogger_ForwardsCategoryAndWrapsExactFactoryLogger(string? categoryName)
    {
        Mock<MelILogger> loggerMock = new();
        loggerMock.Setup(logger => logger.IsEnabled(MelLogLevel.Warning)).Returns(true);
        Mock<MelILoggerFactory> factoryMock = new();
        factoryMock.Setup(factory => factory.CreateLogger(categoryName!)).Returns(loggerMock.Object);
        using var provider = new MicrosoftExtensionsLoggingProvider(factoryMock.Object, ownsFactory: false);

        MtpILogger logger = provider.CreateLogger(categoryName!);
        bool isEnabled = logger.IsEnabled(MtpLogLevel.Warning);

        Assert.IsInstanceOfType<MicrosoftExtensionsLoggerAdapter>(logger);
        Assert.IsTrue(isEnabled);
        factoryMock.Verify(factory => factory.CreateLogger(categoryName!), Times.Once);
        loggerMock.Verify(inner => inner.IsEnabled(MelLogLevel.Warning), Times.Once);
    }

    [TestMethod]
    public void MicrosoftExtensionsLoggingProvider_CreateLogger_ReturnsNewAdapterForEachCall()
    {
        Mock<MelILogger> loggerMock = new();
        Mock<MelILoggerFactory> factoryMock = new();
        factoryMock.Setup(factory => factory.CreateLogger(It.IsAny<string>())).Returns(loggerMock.Object);
        using var provider = new MicrosoftExtensionsLoggingProvider(factoryMock.Object, ownsFactory: false);

        MtpILogger first = provider.CreateLogger("first");
        MtpILogger second = provider.CreateLogger("second");

        Assert.AreNotSame(first, second);
        factoryMock.Verify(factory => factory.CreateLogger("first"), Times.Once);
        factoryMock.Verify(factory => factory.CreateLogger("second"), Times.Once);
    }

    [DataRow(true, 1)]
    [DataRow(false, 0)]
    [TestMethod]
    public void MicrosoftExtensionsLoggingProvider_Dispose_OwnershipControlsFactoryDisposalAndDisposesProvider(
        bool ownsFactory,
        int expectedDisposeCount)
    {
        Mock<MelILoggerFactory> factoryMock = new();
        Mock<IDisposable> disposableFactoryMock = factoryMock.As<IDisposable>();
        var provider = new MicrosoftExtensionsLoggingProvider(factoryMock.Object, ownsFactory);

        provider.Dispose();
        provider.Dispose();

        disposableFactoryMock.Verify(disposable => disposable.Dispose(), Times.Exactly(expectedDisposeCount));
        AssertProviderDisposed(provider);
    }

    [TestMethod]
    public void MicrosoftExtensionsLoggingProvider_Dispose_WhenFactoryThrowsRemainsDisposedAndDoesNotRetry()
    {
        var expectedException = new InvalidOperationException("dispose failure");
        Mock<MelILoggerFactory> factoryMock = new();
        Mock<IDisposable> disposableFactoryMock = factoryMock.As<IDisposable>();
        disposableFactoryMock.Setup(disposable => disposable.Dispose()).Throws(expectedException);
        var provider = new MicrosoftExtensionsLoggingProvider(factoryMock.Object, ownsFactory: true);

        InvalidOperationException actualException = Assert.ThrowsExactly<InvalidOperationException>(provider.Dispose);
        provider.Dispose();

        Assert.AreSame(expectedException, actualException);
        disposableFactoryMock.Verify(disposable => disposable.Dispose(), Times.Once);
        AssertProviderDisposed(provider);
    }

#if NETCOREAPP
    [TestMethod]
    public async Task MicrosoftExtensionsLoggingProvider_DisposeAsync_OwnedAsyncFactoryPrefersAsyncAndIsIdempotent()
    {
        Mock<MelILoggerFactory> factoryMock = new();
        Mock<IDisposable> disposableFactoryMock = factoryMock.As<IDisposable>();
        Mock<IAsyncDisposable> asyncDisposableFactoryMock = factoryMock.As<IAsyncDisposable>();
        asyncDisposableFactoryMock.Setup(disposable => disposable.DisposeAsync()).Returns(ValueTask.CompletedTask);
        var provider = new MicrosoftExtensionsLoggingProvider(factoryMock.Object, ownsFactory: true);

        await provider.DisposeAsync();
        await provider.DisposeAsync();

        asyncDisposableFactoryMock.Verify(disposable => disposable.DisposeAsync(), Times.Once);
        disposableFactoryMock.Verify(disposable => disposable.Dispose(), Times.Never);
        AssertProviderDisposed(provider);
    }

    [TestMethod]
    public async Task MicrosoftExtensionsLoggingProvider_DisposeAsync_OwnedSyncFactoryFallsBackToSyncAndIsIdempotent()
    {
        Mock<MelILoggerFactory> factoryMock = new();
        Mock<IDisposable> disposableFactoryMock = factoryMock.As<IDisposable>();
        var provider = new MicrosoftExtensionsLoggingProvider(factoryMock.Object, ownsFactory: true);

        await provider.DisposeAsync();
        await provider.DisposeAsync();

        disposableFactoryMock.Verify(disposable => disposable.Dispose(), Times.Once);
        AssertProviderDisposed(provider);
    }

    [TestMethod]
    public async Task MicrosoftExtensionsLoggingProvider_DisposeAsync_BorrowedFactoryDoesNotDisposeAndDisposesProvider()
    {
        Mock<MelILoggerFactory> factoryMock = new();
        Mock<IDisposable> disposableFactoryMock = factoryMock.As<IDisposable>();
        Mock<IAsyncDisposable> asyncDisposableFactoryMock = factoryMock.As<IAsyncDisposable>();
        var provider = new MicrosoftExtensionsLoggingProvider(factoryMock.Object, ownsFactory: false);

        await provider.DisposeAsync();
        await provider.DisposeAsync();

        asyncDisposableFactoryMock.Verify(disposable => disposable.DisposeAsync(), Times.Never);
        disposableFactoryMock.Verify(disposable => disposable.Dispose(), Times.Never);
        AssertProviderDisposed(provider);
    }

    [TestMethod]
    public async Task MicrosoftExtensionsLoggingProvider_DisposeThenDisposeAsync_UsesOnlyFirstSyncDisposal()
    {
        Mock<MelILoggerFactory> factoryMock = new();
        Mock<IDisposable> disposableFactoryMock = factoryMock.As<IDisposable>();
        Mock<IAsyncDisposable> asyncDisposableFactoryMock = factoryMock.As<IAsyncDisposable>();
        var provider = new MicrosoftExtensionsLoggingProvider(factoryMock.Object, ownsFactory: true);

        provider.Dispose();
        await provider.DisposeAsync();

        disposableFactoryMock.Verify(disposable => disposable.Dispose(), Times.Once);
        asyncDisposableFactoryMock.Verify(disposable => disposable.DisposeAsync(), Times.Never);
        AssertProviderDisposed(provider);
    }

    [TestMethod]
    public async Task MicrosoftExtensionsLoggingProvider_DisposeAsyncThenDispose_UsesOnlyFirstAsyncDisposal()
    {
        Mock<MelILoggerFactory> factoryMock = new();
        Mock<IDisposable> disposableFactoryMock = factoryMock.As<IDisposable>();
        Mock<IAsyncDisposable> asyncDisposableFactoryMock = factoryMock.As<IAsyncDisposable>();
        asyncDisposableFactoryMock.Setup(disposable => disposable.DisposeAsync()).Returns(ValueTask.CompletedTask);
        var provider = new MicrosoftExtensionsLoggingProvider(factoryMock.Object, ownsFactory: true);

        await provider.DisposeAsync();
        provider.Dispose();

        asyncDisposableFactoryMock.Verify(disposable => disposable.DisposeAsync(), Times.Once);
        disposableFactoryMock.Verify(disposable => disposable.Dispose(), Times.Never);
        AssertProviderDisposed(provider);
    }

    [TestMethod]
    public async Task MicrosoftExtensionsLoggingProvider_DisposeAsync_WhenFactoryThrowsRemainsDisposedAndDoesNotRetry()
    {
        var expectedException = new InvalidOperationException("async dispose failure");
        Mock<MelILoggerFactory> factoryMock = new();
        Mock<IDisposable> disposableFactoryMock = factoryMock.As<IDisposable>();
        Mock<IAsyncDisposable> asyncDisposableFactoryMock = factoryMock.As<IAsyncDisposable>();
        asyncDisposableFactoryMock
            .Setup(disposable => disposable.DisposeAsync())
            .Returns(ValueTask.FromException(expectedException));
        var provider = new MicrosoftExtensionsLoggingProvider(factoryMock.Object, ownsFactory: true);

        InvalidOperationException actualException = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await provider.DisposeAsync());
        await provider.DisposeAsync();
        provider.Dispose();

        Assert.AreSame(expectedException, actualException);
        asyncDisposableFactoryMock.Verify(disposable => disposable.DisposeAsync(), Times.Once);
        disposableFactoryMock.Verify(disposable => disposable.Dispose(), Times.Never);
        AssertProviderDisposed(provider);
    }
#endif

    private static void AssertProviderDisposed(MicrosoftExtensionsLoggingProvider provider)
    {
        ObjectDisposedException exception = Assert.ThrowsExactly<ObjectDisposedException>(
            () => provider.CreateLogger("category"));

        Assert.AreEqual(nameof(MicrosoftExtensionsLoggingProvider), exception.ObjectName);
    }
}
