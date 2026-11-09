// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class DiagnosticLoggingInformationTests
{
    private static readonly TimeSpan CompletionLimit = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CleanupLimit = TimeSpan.FromSeconds(30);

    public TestContext TestContext { get; set; } = null!;

    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task SynchronousLoggerFormatterCanLogRecursively(bool useAsync)
    {
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);

        var fileStreamFactory = new Mock<IFileStreamFactory>();
        fileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<FileMode>(), It.IsAny<FileAccess>(), It.IsAny<FileShare>()))
            .Returns((string path, FileMode _, FileAccess _, FileShare _) => CreateFileStream(path));

        FileLoggerProvider provider = new(
            new FileLoggerOptions("diagnostics", "test", "test.diag", syncFlush: true),
            LogLevel.Debug,
            customDirectory: false,
            Mock.Of<IClock>(),
            new SystemTask(),
            Mock.Of<IConsole>(),
            fileSystem.Object,
            fileStreamFactory.Object);
        ILogger logger = provider.CreateLogger("test");

        // A queued thread-pool task can exceed the deadline before logging starts on a busy .NET Framework host.
        Task logTask = Task.Factory.StartNew(
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
            TestContext.CancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();
        Task completedTask = await Task.WhenAny(logTask, Task.Delay(TimeSpan.FromSeconds(5), TestContext.CancellationToken));

        Assert.AreSame(logTask, completedTask, "Recursive logging from a formatter deadlocked.");
        await logTask;
        await DisposeHelper.DisposeAsync(provider);

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

        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);

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

        FileLoggerProvider provider = new(
            new FileLoggerOptions(initialDirectory, "test", FileName, syncFlush: false),
            LogLevel.Debug,
            customDirectory: false,
            Mock.Of<IClock>(),
            new SystemTask(),
            Mock.Of<IConsole>(),
            fileSystem.Object,
            fileStreamFactory.Object);
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

        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);

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

        FileLoggerProvider provider = new(
            new FileLoggerOptions(initialDirectory, "test", FileName, syncFlush: true),
            LogLevel.Debug,
            customDirectory: false,
            Mock.Of<IClock>(),
            new SystemTask(),
            Mock.Of<IConsole>(),
            fileSystem.Object,
            fileStreamFactory.Object);
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

        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);
        fileSystem
            .Setup(x => x.MoveFile(initialPath, conflictingPath, false))
            .Throws(new IOException("Destination file already exists."));

        var fileStreamFactory = new Mock<IFileStreamFactory>();
        fileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<FileMode>(), It.IsAny<FileAccess>(), It.IsAny<FileShare>()))
            .Returns((string path, FileMode _, FileAccess _, FileShare _) => CreateFileStream(path));

        FileLoggerProvider provider = new(
            new FileLoggerOptions(initialDirectory, "test", FileName, syncFlush: true),
            LogLevel.Debug,
            customDirectory: false,
            Mock.Of<IClock>(),
            new SystemTask(),
            Mock.Of<IConsole>(),
            fileSystem.Object,
            fileStreamFactory.Object);
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

    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task SynchronousLoggerCreatedBeforeRelocationWaitsForReplacement(bool useAsync)
    {
        string initialDirectory = Path.Combine("initial", "diagnostics");
        string resultsDirectory = Path.Combine("final", "results");
        const string FileName = "test.diag";
        const string Message = "Written during relocation.";

        var disposeStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowDispose = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var synchronousWaitAttempted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);

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

        using FileLoggerProvider provider = new(
            new FileLoggerOptions(initialDirectory, "test", FileName, syncFlush: true),
            LogLevel.Debug,
            customDirectory: false,
            Mock.Of<IClock>(),
            new SystemTask(),
            Mock.Of<IConsole>(),
            fileSystem.Object,
            fileStreamFactory.Object);
        ILogger logger = provider.CreateLogger("test");

        Task relocationTask = Task.Factory.StartNew(
            () => provider.CheckLogFolderAndMoveToTheNewIfNeededAsync(resultsDirectory),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();
        Task logTask = Task.CompletedTask;
        bool completedBeforeReplacement;

        try
        {
            await disposeStarted.Task.WaitAsync(CompletionLimit, TestContext.CancellationToken);

            logTask = useAsync
                ? logger.LogAsync(LogLevel.Debug, Message, null, LoggingExtensions.Formatter)
                : Task.Factory.StartNew(
                    LogSynchronouslyAfterWaitProbe,
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
            if (!useAsync)
            {
                Task first = await Task.WhenAny(synchronousWaitAttempted.Task, logTask).WaitAsync(CompletionLimit, TestContext.CancellationToken);
                Assert.AreSame(synchronousWaitAttempted.Task, first, "Synchronous logging did not attempt the held relocation gate.");
            }

            completedBeforeReplacement = logTask.IsCompleted;
        }
        finally
        {
            allowDispose.TrySetResult(true);
            // Cleanup must join the workers even when the test's cancellation token has been canceled.
            await Task.WhenAll(relocationTask, logTask).WaitAsync(CleanupLimit, CancellationToken.None);
        }

        Assert.IsFalse(completedBeforeReplacement, "Logging completed before relocation installed the replacement logger.");
        Assert.IsNotNull(replacementStream);
        string expected = useAsync
            ? """
              0001-01-01T00:00:00.0000000+00:00 test DEBUG Written during relocation.

              """
            : """
              [00:00:00.000 test - DEBUG] Written during relocation.

              """;
        Assert.AreEqual(
            expected.Replace("\r\n", "\n"),
            Encoding.UTF8.GetString(replacementStream.ToArray()).Replace("\r\n", "\n"));

#pragma warning disable CA1416 // Thread interruption is unavailable on browser, but this test runs on desktop test hosts.
        void LogSynchronouslyAfterWaitProbe()
        {
            Assert.IsFalse(Thread.CurrentThread.IsThreadPoolThread, "The wait probe must not interrupt a thread-pool worker.");
            try
            {
                Assert.ThrowsExactly<ThreadInterruptedException>(
                    () => logger.Log(LogLevel.Debug, Message, null, InterruptOnNextWait));
            }
            finally
            {
                try
                {
                    Thread.Sleep(0);
                }
                catch (ThreadInterruptedException)
                {
                    // Clear this probe's pending interrupt if logging failed before it could block.
                }
            }

            synchronousWaitAttempted.TrySetResult(true);
            logger.LogDebug(Message);
        }

        static string InterruptOnNextWait(string state, Exception? exception)
        {
            // Returning from this formatter enters the relocation wait; only blocking consumes the interrupt.
            Thread.CurrentThread.Interrupt();
            return state;
        }
#pragma warning restore CA1416

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

        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(false);

        var fileStreamFactory = new Mock<IFileStreamFactory>();
        fileStreamFactory
            .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<FileMode>(), It.IsAny<FileAccess>(), It.IsAny<FileShare>()))
            .Returns((string path, FileMode _, FileAccess _, FileShare _) => CreateFileStream(path));

        using FileLoggerProvider provider = new(
            new FileLoggerOptions(initialDirectory, "test", FileName, syncFlush: true),
            LogLevel.Debug,
            customDirectory: false,
            Mock.Of<IClock>(),
            new SystemTask(),
            Mock.Of<IConsole>(),
            fileSystem.Object,
            fileStreamFactory.Object);
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
