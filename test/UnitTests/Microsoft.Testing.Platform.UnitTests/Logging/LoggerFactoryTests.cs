// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class LoggerFactoryTests
{
    private readonly Mock<ILogger> _mockLogger = new();
    private readonly Mock<IMonitor> _mockMonitor = new();
    private readonly Mock<IDisposableLoggerProvider> _mockLoggerProvider = new();
    private readonly ILoggerProvider[] _loggerProviders;

    public LoggerFactoryTests()
    {
        _mockMonitor.Setup(x => x.Lock(It.IsAny<object>())).Returns(new Mock<IDisposable>().Object);
        _mockLoggerProvider.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_mockLogger.Object);

        _loggerProviders =
        [
            _mockLoggerProvider.Object
        ];
    }

    [TestMethod]
    public void LoggerFactory_LoggerCreatedOnlyOnce()
    {
        using LoggerFactory loggerFactory = new(_loggerProviders, LogLevel.Information, _mockMonitor.Object);

        _ = loggerFactory.CreateLogger("test");
        _ = loggerFactory.CreateLogger("test");
        _ = loggerFactory.CreateLogger("test");
        _mockLoggerProvider.Verify(x => x.CreateLogger("test"), Times.Once);
    }

#if NETCOREAPP
    [TestMethod]
    public async Task DisposeAsync_PrefersAsyncProvidersAndPreservesRegistrationOrder()
    {
        List<string> disposed = [];
        Mock<ILoggerProvider> asynchronous = new();
        asynchronous.As<IAsyncDisposable>().Setup(p => p.DisposeAsync()).Returns(() =>
        {
            disposed.Add("async");
            return ValueTask.CompletedTask;
        });
        Mock<IDisposable> redundantSync = asynchronous.As<IDisposable>();
        Mock<ILoggerProvider> synchronous = new();
        synchronous.As<IDisposable>().Setup(p => p.Dispose()).Callback(() => disposed.Add("sync"));
        Mock<ILoggerProvider> plain = new();
        LoggerFactory factory = new([asynchronous.Object, synchronous.Object, plain.Object], LogLevel.Debug, _mockMonitor.Object);

        await factory.DisposeAsync();

        Assert.AreSequenceEqual(["async", "sync"], disposed);
        redundantSync.Verify(p => p.Dispose(), Times.Never);
        plain.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task DisposeAsync_ProviderFailurePreservesExceptionAndStopsLaterDisposals()
    {
        IOException expected = new("provider flush failed");
        Mock<ILoggerProvider> first = new();
        first.As<IAsyncDisposable>().Setup(p => p.DisposeAsync()).Returns(new ValueTask(Task.FromException(expected)));
        Mock<ILoggerProvider> later = new();
        Mock<IDisposable> laterDisposal = later.As<IDisposable>();
        LoggerFactory factory = new([first.Object, later.Object], LogLevel.Trace, _mockMonitor.Object);

        IOException actual = await Assert.ThrowsExactlyAsync<IOException>(async () => await factory.DisposeAsync());

        Assert.AreSame(expected, actual);
        laterDisposal.Verify(p => p.Dispose(), Times.Never);
    }
#endif
}

internal interface IDisposableLoggerProvider : ILoggerProvider, IDisposable;
