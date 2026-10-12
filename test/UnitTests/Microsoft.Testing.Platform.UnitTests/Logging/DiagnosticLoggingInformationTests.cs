// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class DiagnosticLoggingInformationTests
{
    public TestContext TestContext { get; set; } = null!;

    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task SynchronousLoggerFormatterCanLogRecursively(bool useAsync)
    {
        Mock<IFileSystem> fileSystem = CreateFileSystemMock();
        Mock<IFileStreamFactory> fileStreamFactory = CreateFileStreamFactoryMock();

        FileLoggerProvider provider = CreateProvider(fileSystem, fileStreamFactory, "diagnostics", syncFlush: true);
        ILogger logger = provider.CreateLogger("test");

        var logTask = Task.Run(
            async () =>
            {
                if (useAsync)
                {
                    await logger.LogAsync(LogLevel.Debug, "outer", null, FormatAndLogNested);
                }
                else
                {
                    logger.Log(LogLevel.Debug, "outer", null, FormatAndLogNested);
                }
            },
            TestContext.CancellationToken);
        Task completedTask = await Task.WhenAny(logTask, Task.Delay(TimeSpan.FromSeconds(5), TestContext.CancellationToken));

        Assert.AreSame(logTask, completedTask, "Recursive logging from a formatter deadlocked.");
        await logTask;
        provider.Dispose();

        string FormatAndLogNested(string state, Exception? exception)
        {
            logger.LogDebug("nested");
            return state;
        }
    }

    [TestMethod]
    public async Task AsyncLoggerCreatedBeforeRelocationQueuesLogForReplacement()
    {
        string initialDirectory = Path.Combine("initial", "diagnostics");
        string resultsDirectory = Path.Combine("final", "results");
        const string FileName = "test.diag";

        var disposeStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowDispose = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Mock<IFileSystem> fileSystem = CreateFileSystemMock();

        int streamIndex = 0;
        MemoryStream? replacementStream = null;
        var fileStreamFactory = new Mock<IFileStreamFactory>();
        fileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<FileMode>(), It.IsAny<FileAccess>(), It.IsAny<FileShare>()))
            .Returns((string path, FileMode _, FileAccess _, FileShare _) =>
            {
                var stream = new MemoryStream();
                int currentStreamIndex = Interlocked.Increment(ref streamIndex);
                if (currentStreamIndex == 2)
                {
                    replacementStream = stream;
                }

                return CreateFileStream(path, currentStreamIndex == 1 ? BlockFirstStreamDisposalAsync : null, stream);
            });

        FileLoggerProvider provider = CreateProvider(fileSystem, fileStreamFactory, initialDirectory, syncFlush: false, fileName: FileName);
        ILogger logger = provider.CreateLogger("test");

        var relocationTask = Task.Run(
            () => provider.CheckLogFolderAndMoveToTheNewIfNeededAsync(resultsDirectory),
            TestContext.CancellationToken);
        await disposeStarted.Task;

        logger.LogDebug("Written during relocation.");
        allowDispose.SetResult(true);
        await relocationTask;

#if NETCOREAPP
        await provider.DisposeAsync();
#else
        provider.Dispose();
#endif

        Assert.IsNotNull(replacementStream);
        string content = Encoding.UTF8.GetString(replacementStream.ToArray());
        Assert.Contains("Written during relocation.", content);

        async Task BlockFirstStreamDisposalAsync()
        {
            disposeStarted.TrySetResult(true);
            await allowDispose.Task;
        }
    }

    [TestMethod]
    public async Task RelocationInstallsReplacementBeforeRethrowingDisposalFailure()
    {
        string initialDirectory = Path.Combine("initial", "diagnostics");
        string resultsDirectory = Path.Combine("final", "results");
        const string FileName = "test.diag";

        Mock<IFileSystem> fileSystem = CreateFileSystemMock();

        int streamIndex = 0;
        var fileStreamFactory = new Mock<IFileStreamFactory>();
        fileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<FileMode>(), It.IsAny<FileAccess>(), It.IsAny<FileShare>()))
            .Returns((string path, FileMode _, FileAccess _, FileShare _) =>
                CreateFileStream(
                    path,
                    disposeException: Interlocked.Increment(ref streamIndex) == 1
                        ? new IOException("Simulated disposal failure.")
                        : null));

        FileLoggerProvider provider = CreateProvider(fileSystem, fileStreamFactory, initialDirectory, syncFlush: true, fileName: FileName);
        ILogger logger = provider.CreateLogger("test");
        var information = new DiagnosticLoggingInformation(provider);

        IOException exception = await Assert.ThrowsExactlyAsync<IOException>(
            () => provider.CheckLogFolderAndMoveToTheNewIfNeededAsync(resultsDirectory));

        Assert.AreEqual("Simulated disposal failure.", exception.Message);
        Assert.AreEqual(Path.GetFullPath(resultsDirectory), information.LogFile.DirectoryName);
        Assert.StartsWith("test_", information.LogFile.Name);
        Assert.AreNotEqual(FileName, information.LogFile.Name);
        logger.LogDebug("Written after failed relocation.");

#if NETCOREAPP
        await provider.DisposeAsync();
#else
        provider.Dispose();
#endif
    }

    [TestMethod]
    public async Task RelocationUsesUniqueReplacementBeforeRethrowingMoveFailure()
    {
        string initialDirectory = Path.Combine("initial", "diagnostics");
        string resultsDirectory = Path.Combine("final", "results");
        const string FileName = "test.diag";
        string initialPath = Path.Combine(initialDirectory, FileName);
        string conflictingPath = Path.Combine(resultsDirectory, FileName);

        Mock<IFileSystem> fileSystem = CreateFileSystemMock();
        fileSystem
            .Setup(x => x.MoveFile(initialPath, conflictingPath, false))
            .Throws(new IOException("Destination file already exists."));

        Mock<IFileStreamFactory> fileStreamFactory = CreateFileStreamFactoryMock();

        FileLoggerProvider provider = CreateProvider(fileSystem, fileStreamFactory, initialDirectory, syncFlush: true, fileName: FileName);
        ILogger logger = provider.CreateLogger("test");
        var information = new DiagnosticLoggingInformation(provider);

        IOException exception = await Assert.ThrowsExactlyAsync<IOException>(
            () => provider.CheckLogFolderAndMoveToTheNewIfNeededAsync(resultsDirectory));

        Assert.AreEqual("Destination file already exists.", exception.Message);
        Assert.AreEqual(Path.GetFullPath(resultsDirectory), information.LogFile.DirectoryName);
        Assert.StartsWith("test_", information.LogFile.Name);
        Assert.AreNotEqual(FileName, information.LogFile.Name);
        logger.LogDebug("Written after failed move.");

#if NETCOREAPP
        await provider.DisposeAsync();
#else
        provider.Dispose();
#endif
    }

    [TestMethod]
    public async Task SynchronousLoggerCreatedBeforeRelocationWaitsForReplacement()
    {
        string initialDirectory = Path.Combine("initial", "diagnostics");
        string resultsDirectory = Path.Combine("final", "results");
        const string FileName = "test.diag";

        var disposeStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowDispose = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var logThreadStarted = new ManualResetEventSlim();
        var logThreadCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Exception? logException = null;

        Mock<IFileSystem> fileSystem = CreateFileSystemMock();

        int streamIndex = 0;
        var fileStreamFactory = new Mock<IFileStreamFactory>();
        fileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<FileMode>(), It.IsAny<FileAccess>(), It.IsAny<FileShare>()))
            .Returns((string path, FileMode _, FileAccess _, FileShare _) =>
                CreateFileStream(path, Interlocked.Increment(ref streamIndex) == 1 ? BlockFirstStreamDisposalAsync : null));

        using FileLoggerProvider provider = CreateProvider(fileSystem, fileStreamFactory, initialDirectory, syncFlush: true, fileName: FileName);
        ILogger logger = provider.CreateLogger("test");

        var relocationTask = Task.Run(
            () => provider.CheckLogFolderAndMoveToTheNewIfNeededAsync(resultsDirectory),
            TestContext.CancellationToken);
        await disposeStarted.Task;

        var logThread = new Thread(
            () =>
            {
                logThreadStarted.Set();
                try
                {
                    logger.LogDebug("Written during relocation.");
                }
                catch (Exception ex)
                {
                    logException = ex;
                }
                finally
                {
                    logThreadCompleted.TrySetResult(true);
                }
            });
#pragma warning disable CA1416 // Threading is unavailable on browser, but this unit test runs on desktop test hosts.
        logThread.Start();

        try
        {
            logThreadStarted.Wait(TestContext.CancellationToken);
            bool reachedRelocationGate = SpinWait.SpinUntil(
                () => (logThread.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0 || logThreadCompleted.Task.IsCompleted,
                TimeSpan.FromSeconds(5));
#pragma warning restore CA1416

            Assert.IsTrue(reachedRelocationGate, "The logging thread did not reach the relocation gate.");
            Assert.IsFalse(logThreadCompleted.Task.IsCompleted, $"Logging completed before relocation installed the replacement logger: {logException}");
        }
        finally
        {
            allowDispose.TrySetResult(true);
        }

        await relocationTask;
        await logThreadCompleted.Task.WaitAsync(TestContext.CancellationToken);
        logThread.Join();
        Assert.IsNull(logException);

        async Task BlockFirstStreamDisposalAsync()
        {
            disposeStarted.TrySetResult(true);
            await allowDispose.Task;
        }
    }

    [TestMethod]
    public async Task LogFileReflectsFileLoggerRelocation()
    {
        string initialDirectory = Path.Combine("initial", "diagnostics");
        string resultsDirectory = Path.Combine("final", "results");
        const string FileName = "test.diag";
        string initialPath = Path.Combine(initialDirectory, FileName);
        string finalPath = Path.Combine(resultsDirectory, FileName);

        Mock<IFileSystem> fileSystem = CreateFileSystemMock();
        Mock<IFileStreamFactory> fileStreamFactory = CreateFileStreamFactoryMock();

        using FileLoggerProvider provider = CreateProvider(fileSystem, fileStreamFactory, initialDirectory, syncFlush: true, fileName: FileName);
        var information = new DiagnosticLoggingInformation(provider);
        ILogger logger = provider.CreateLogger("test");

        Assert.IsTrue(information.SynchronousWrite);
        Assert.AreEqual(LogLevel.Debug, information.LogLevel);
        Assert.AreEqual(Path.GetFullPath(initialPath), information.LogFile.FullName);

        await provider.CheckLogFolderAndMoveToTheNewIfNeededAsync(resultsDirectory);

        Assert.AreEqual(Path.GetFullPath(finalPath), information.LogFile.FullName);
        Assert.AreEqual(Path.GetFullPath(resultsDirectory), information.LogFile.DirectoryName);
        fileSystem.Verify(x => x.MoveFile(initialPath, finalPath, false), Times.Once);

        logger.LogDebug("Written after relocation.");
    }

    [TestMethod]
    public async Task CheckLogFolderAndMoveToTheNewIfNeededAsync_CustomDirectory_SkipsRelocation()
    {
        string initialDirectory = Path.Combine("initial", "diagnostics");
        string resultsDirectory = Path.Combine("final", "results");
        const string FileName = "test.diag";

        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);

        Mock<IFileStreamFactory> fileStreamFactory = CreateFileStreamFactoryMock();

        using FileLoggerProvider provider = CreateProvider(fileSystem, fileStreamFactory, initialDirectory, syncFlush: true, fileName: FileName, customDirectory: true);
        FileLogger originalLogger = provider.FileLogger;

        await provider.CheckLogFolderAndMoveToTheNewIfNeededAsync(resultsDirectory);

        Assert.AreSame(originalLogger, provider.FileLogger);
        fileSystem.Verify(x => x.MoveFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    public async Task CheckLogFolderAndMoveToTheNewIfNeededAsync_AlreadyInTargetDirectory_SkipsRelocation()
    {
        string initialDirectory = Path.Combine("initial", "diagnostics");
        const string FileName = "test.diag";

        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);

        Mock<IFileStreamFactory> fileStreamFactory = CreateFileStreamFactoryMock();

        using FileLoggerProvider provider = CreateProvider(fileSystem, fileStreamFactory, initialDirectory, syncFlush: true, fileName: FileName);
        FileLogger originalLogger = provider.FileLogger;

        await provider.CheckLogFolderAndMoveToTheNewIfNeededAsync(initialDirectory);

        Assert.AreSame(originalLogger, provider.FileLogger);
        fileSystem.Verify(x => x.MoveFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    private static Mock<IFileSystem> CreateFileSystemMock()
    {
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);
        return fileSystem;
    }

    private static Mock<IFileStreamFactory> CreateFileStreamFactoryMock()
    {
        var fileStreamFactory = new Mock<IFileStreamFactory>();
        fileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<FileMode>(), It.IsAny<FileAccess>(), It.IsAny<FileShare>()))
            .Returns((string path, FileMode _, FileAccess _, FileShare _) => CreateFileStream(path));
        return fileStreamFactory;
    }

    private static FileLoggerProvider CreateProvider(
        Mock<IFileSystem> fileSystem,
        Mock<IFileStreamFactory> fileStreamFactory,
        string directory,
        bool syncFlush,
        string fileName = "test.diag",
        bool customDirectory = false)
        => new(
            new FileLoggerOptions(directory, "test", fileName, syncFlush),
            LogLevel.Debug,
            customDirectory,
            Mock.Of<IClock>(),
            new SystemTask(),
            Mock.Of<IConsole>(),
            fileSystem.Object,
            fileStreamFactory.Object);

    private static IFileStream CreateFileStream(
        string path,
        Func<Task>? disposeAsync = null,
        MemoryStream? memoryStream = null,
        Exception? disposeException = null)
    {
        memoryStream ??= new();
        var fileStream = new Mock<IFileStream>();
        fileStream.SetupGet(x => x.Name).Returns(path);
        fileStream.SetupGet(x => x.Stream).Returns(memoryStream);
        fileStream.Setup(x => x.Dispose()).Callback(() =>
        {
            disposeAsync?.Invoke().GetAwaiter().GetResult();
            memoryStream.Dispose();
            if (disposeException is not null)
            {
                throw disposeException;
            }
        });
#if NETCOREAPP
        fileStream.Setup(x => x.DisposeAsync()).Returns(() => new ValueTask(DisposeAsync()));
#endif
        return fileStream.Object;

#if NETCOREAPP
        async Task DisposeAsync()
        {
            if (disposeAsync is not null)
            {
                await disposeAsync();
            }

            memoryStream.Dispose();
            if (disposeException is not null)
            {
                throw disposeException;
            }
        }
#endif
    }
}
