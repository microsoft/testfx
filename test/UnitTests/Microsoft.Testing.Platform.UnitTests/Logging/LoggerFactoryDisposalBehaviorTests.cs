// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class LoggerFactoryDisposalBehaviorTests
{
    private readonly Mock<IMonitor> _monitor = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Mock<IConsole> _console = new();
    private readonly Mock<IFileSystem> _fileSystem = new();
    private readonly Mock<IFileStream> _fileStream = new();
    private readonly Mock<IFileStreamFactory> _fileStreamFactory = new();

    public LoggerFactoryDisposalBehaviorTests()
    {
        _monitor.Setup(x => x.Lock(It.IsAny<object>())).Returns(new Mock<IDisposable>().Object);
        _fileStream.Setup(x => x.Name).Returns("ccc");
        _fileStream.Setup(x => x.Stream).Returns(new MemoryStream());
        _fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);
        _fileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            .Returns(_fileStream.Object);
    }

    [TestMethod]
    public void Dispose_DisposesEveryDisposableProviderExceptTheFileLoggerProvider()
    {
        using FileLoggerProvider fileProvider = CreateFileProvider();
        Mock<ILoggerProvider> first = new();
        Mock<IDisposable> firstDisposable = first.As<IDisposable>();
        Mock<ILoggerProvider> second = new();
        Mock<IDisposable> secondDisposable = second.As<IDisposable>();
        Mock<ILoggerProvider> notDisposable = new();
        LoggerFactory factory = new([first.Object, fileProvider, notDisposable.Object, second.Object], LogLevel.Trace, _monitor.Object);

        factory.Dispose();

        firstDisposable.Verify(x => x.Dispose(), Times.Once);
        secondDisposable.Verify(x => x.Dispose(), Times.Once);
        Assert.IsFalse(fileProvider.FileLogger.IsFileHandleReleased, "The file logger provider is disposed explicitly by the host, not by the factory.");
    }

#if NETCOREAPP
    [TestMethod]
    public async Task DisposeAsync_DisposesEveryProviderExceptTheFileLoggerProviderPreferringAsyncDisposal()
    {
        await using FileLoggerProvider fileProvider = CreateFileProvider();
        Mock<ILoggerProvider> asyncProvider = new();
        Mock<IAsyncDisposable> asyncDisposable = asyncProvider.As<IAsyncDisposable>();
        asyncDisposable.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
        Mock<IDisposable> syncDisposableOnAsyncProvider = asyncProvider.As<IDisposable>();
        Mock<ILoggerProvider> syncProvider = new();
        Mock<IDisposable> syncDisposable = syncProvider.As<IDisposable>();
        Mock<ILoggerProvider> notDisposable = new();
        LoggerFactory factory = new([asyncProvider.Object, fileProvider, notDisposable.Object, syncProvider.Object], LogLevel.Trace, _monitor.Object);

        await factory.DisposeAsync();

        asyncDisposable.Verify(x => x.DisposeAsync(), Times.Once);
        syncDisposableOnAsyncProvider.Verify(x => x.Dispose(), Times.Never);
        syncDisposable.Verify(x => x.Dispose(), Times.Once);
        Assert.IsFalse(fileProvider.FileLogger.IsFileHandleReleased, "The file logger provider is disposed explicitly by the host, not by the factory.");
    }

    [TestMethod]
    public async Task LoggerFactoryProxy_DisposeAsyncTwice_DisposesTheInnerFactoryOnlyOnce()
    {
        Mock<ILoggerFactory> inner = new();
        Mock<IAsyncDisposable> asyncDisposable = inner.As<IAsyncDisposable>();
        asyncDisposable.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
        Mock<IDisposable> syncDisposable = inner.As<IDisposable>();
        LoggerFactoryProxy proxy = new();
        proxy.SetLoggerFactory(inner.Object);

        await proxy.DisposeAsync();
        await proxy.DisposeAsync();

        asyncDisposable.Verify(x => x.DisposeAsync(), Times.Once);
        syncDisposable.Verify(x => x.Dispose(), Times.Never);
    }
#endif

    [TestMethod]
    public void LoggerOfT_UsesTheGenericTypeDefinitionDisplayNameAsCategory()
    {
        Mock<ILoggerFactory> factory = new();
        factory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        _ = new Logger<Dictionary<string, int>>(factory.Object);

        factory.Verify(x => x.CreateLogger("System.Collections.Generic.Dictionary"), Times.Once);
    }

    private FileLoggerProvider CreateFileProvider()
        => new(
            new FileLoggerOptions("aaa", "bbb", fileName: "ccc", syncFlush: true),
            LogLevel.Trace,
            customDirectory: false,
            _clock.Object,
            new SystemTask(),
            _console.Object,
            _fileSystem.Object,
            _fileStreamFactory.Object);
}
