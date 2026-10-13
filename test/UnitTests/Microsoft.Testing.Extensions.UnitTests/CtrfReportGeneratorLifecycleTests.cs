// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

using Microsoft.Testing.Extensions.CtrfReport;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class CtrfReportGeneratorLifecycleTests
{
    private readonly Mock<IFileSystem> _fileSystemMock = new();
    private readonly Mock<IEnvironment> _environmentMock = new();
    private readonly Mock<ITestFramework> _testFrameworkMock = new();
    private readonly Mock<IConfiguration> _configurationMock = new();
    private readonly Mock<ITestApplicationModuleInfo> _testApplicationModuleInfoMock = new();

    [TestMethod]
    [DataRow(-1, 0, true)]
    [DataRow(0, 0, true)]
    [DataRow(1, 1, false)]
    public async Task ControllerChild_ByteBudgetAcceptsExactBoundaryAndReservesCompletionAsync(int boundaryOffset, int expectedDropped, bool expectedResultRecord)
    {
        var completed = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var timeout = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<Task>? writer = null;
        var task = new Mock<ITask>();
        task.Setup(value => value.RunLongRunning(It.IsAny<Func<Task>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Func<Task>, string, CancellationToken>((action, _, _) => writer = action)
            .Returns(completed.Task);
        task.Setup(value => value.Delay(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).Returns(timeout.Task);
        GeneratorTestContext context = CreateGenerator(optionIsSet: true, isControllerChild: true, task: task.Object);
        var session = new TestSessionContextStub();
        await context.Generator.OnTestSessionStartingAsync(session);
        await context.Generator.ConsumeAsync(null!, CreateMessage("passed", PassedTestNodeStateProperty.CachedInstance), CancellationToken.None);
        Type generatorBase = context.Generator.GetType().BaseType!;
        Type handler = typeof(CtrfReportEngine).Assembly.GetType("Microsoft.Testing.Extensions.ReportProcessLifetimeHandler`2", true)!;
        long maximum = Convert.ToInt64(handler.GetField("MaxJournalBytes", BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue(), CultureInfo.InvariantCulture);
        int reserve = (int)generatorBase.GetField("JournalCompletionReserveBytes", BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!;
        var queue = (System.Collections.IEnumerable)generatorBase.GetField("_journalQueue", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(context.Generator)!;
        MethodInfo serialize = context.Generator.GetType().GetMethod("SerializeJournalRecord", BindingFlags.Instance | BindingFlags.NonPublic)!;
        int dataBytes = queue.Cast<object>().Sum(record => Encoding.UTF8.GetByteCount((string)serialize.Invoke(context.Generator, [record])!) + 1);
        generatorBase.GetField("_journalBytesWritten", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(context.Generator, maximum - reserve - dataBytes + boundaryOffset);

        Task finish = context.Generator.OnTestSessionFinishingAsync(session);
        Assert.IsNotNull(writer);
        await writer();
        completed.SetResult(null);
        await finish;
        context.Generator.Dispose();

        Assert.ContainsSingle(context.PublishedArtifacts);
        using var report = JsonDocument.Parse(context.ReportStream.GetUtf8Content());
        Assert.AreEqual(1, report.RootElement.GetProperty("results").GetProperty("summary").GetProperty("passed").GetInt32());
        string[] records = context.JournalStream.GetUtf8Content().Split(['\n'], StringSplitOptions.RemoveEmptyEntries);
        using var completion = JsonDocument.Parse(records[^1]);
        Assert.AreEqual(2, completion.RootElement.GetProperty("Type").GetInt32());
        Assert.AreEqual(expectedDropped, completion.RootElement.GetProperty("DroppedJournalRecords").GetInt32());
        Assert.AreEqual(expectedResultRecord, records.Any(record => record.Contains("\"Type\":1", StringComparison.Ordinal)));
        Assert.IsLessThanOrEqualTo(maximum, (long)generatorBase.GetField("_journalBytesWritten", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(context.Generator)!);
        Assert.IsTrue(context.JournalStream.IsDisposed);
    }

    [TestMethod]
    [DataRow(3, 0, true)]
    [DataRow(2, 1, false)]
    [DataRow(1, 2, false)]
    public async Task ControllerChild_RecordBudgetReservesCompletionAndPreservesNormalReportAsync(int availableRecords, int expectedDropped, bool expectedResultRecord)
    {
        var completed = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var timeout = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<Task>? writer = null;
        var task = new Mock<ITask>();
        task.Setup(value => value.RunLongRunning(It.IsAny<Func<Task>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Func<Task>, string, CancellationToken>((action, _, _) => writer = action)
            .Returns(completed.Task);
        task.Setup(value => value.Delay(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).Returns(timeout.Task);
        GeneratorTestContext context = CreateGenerator(optionIsSet: true, isControllerChild: true, task: task.Object);
        Type handler = typeof(CtrfReportEngine).Assembly.GetType("Microsoft.Testing.Extensions.ReportProcessLifetimeHandler`2", true)!;
        int maximum = (int)handler.GetField("MaxJournalRecordCount", BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!;
        Type generatorBase = context.Generator.GetType().BaseType!;
        generatorBase.GetField("_journalRecordsWritten", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(context.Generator, maximum - availableRecords);
        var session = new TestSessionContextStub();

        await context.Generator.OnTestSessionStartingAsync(session);
        await context.Generator.ConsumeAsync(null!, CreateMessage("passed", PassedTestNodeStateProperty.CachedInstance), CancellationToken.None);
        Task finish = context.Generator.OnTestSessionFinishingAsync(session);
        Assert.IsNotNull(writer);
        await writer();
        completed.SetResult(null);
        await finish;
        context.Generator.Dispose();

        Assert.ContainsSingle(context.PublishedArtifacts);
        using var report = JsonDocument.Parse(context.ReportStream.GetUtf8Content());
        Assert.AreEqual(1, report.RootElement.GetProperty("results").GetProperty("summary").GetProperty("passed").GetInt32());
        string[] records = context.JournalStream.GetUtf8Content().Split(['\n'], StringSplitOptions.RemoveEmptyEntries);
        using var completion = JsonDocument.Parse(records[^1]);
        Assert.AreEqual(2, completion.RootElement.GetProperty("Type").GetInt32());
        Assert.AreEqual(expectedDropped, completion.RootElement.GetProperty("DroppedJournalRecords").GetInt32());
        Assert.AreEqual(expectedResultRecord, records.Any(record => record.Contains("\"Type\":1", StringComparison.Ordinal)));
        Assert.AreEqual(maximum, (int)generatorBase.GetField("_journalRecordsWritten", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(context.Generator)!);
        Assert.IsTrue(context.JournalStream.IsDisposed);
    }

    [TestMethod]
    public async Task ControllerChild_CompletionTimeoutPreservesPublishedReportAndCanDrainCapturedWriterAsync()
    {
        var completed = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<Task>? writer = null;
        var task = new Mock<ITask>();
        task.Setup(value => value.RunLongRunning(It.IsAny<Func<Task>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Func<Task>, string, CancellationToken>((action, _, _) => writer = action)
            .Returns(completed.Task);
        task.Setup(value => value.Delay(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        GeneratorTestContext context = CreateGenerator(optionIsSet: true, isControllerChild: true, task: task.Object);
        var session = new TestSessionContextStub();

        await context.Generator.OnTestSessionStartingAsync(session);
        await context.Generator.ConsumeAsync(null!, CreateMessage("passed", PassedTestNodeStateProperty.CachedInstance), CancellationToken.None);
        await context.Generator.OnTestSessionFinishingAsync(session);

        Assert.ContainsSingle(context.PublishedArtifacts);
        using var report = JsonDocument.Parse(context.ReportStream.GetUtf8Content());
        Assert.AreEqual(1, report.RootElement.GetProperty("results").GetProperty("summary").GetProperty("tests").GetInt32());
        context.LoggerMock.Verify(
            value => value.LogAsync(
                LogLevel.Warning,
                It.Is<string>(message => message.Contains("writer did not drain within the hang timeout", StringComparison.Ordinal)),
                null, It.IsAny<Func<string, Exception?, string>>()), Times.Once);
        context.Generator.Dispose();
        context.Generator.Dispose();
        Assert.IsNotNull(writer);
        await writer();
        completed.SetResult(null);
        Assert.IsTrue(context.JournalStream.IsDisposed);
        Assert.Contains("\"Type\":2", context.JournalStream.GetUtf8Content());
    }

    [TestMethod]
    public async Task IsEnabledAsync_ReturnsTrue_WhenOptionIsSetAsync()
    {
        GeneratorTestContext context = CreateGenerator(optionIsSet: true);

        Assert.IsTrue(await context.Generator.IsEnabledAsync().ConfigureAwait(false));
    }

    [TestMethod]
    public async Task IsEnabledAsync_ReturnsFalse_WhenOptionIsNotSetAsync()
    {
        GeneratorTestContext context = CreateGenerator(optionIsSet: false);

        Assert.IsFalse(await context.Generator.IsEnabledAsync().ConfigureAwait(false));
    }

    [TestMethod]
    public async Task OnTestSessionFinishingAsync_WithoutPriorStart_ThrowsUnreachableExceptionAsync()
    {
        GeneratorTestContext context = CreateGenerator(optionIsSet: true);

        // UnreachableException is an internal, per-assembly polyfill on non-NETCOREAPP TFMs, so asserting on the
        // generic type parameter would compare against this test assembly's copy and fail due to type identity
        // mismatch across assemblies. Assert on the full type name instead.
        Exception exception = await Assert.ThrowsAsync<Exception>(
            () => context.Generator.OnTestSessionFinishingAsync(new TestSessionContextStub())).ConfigureAwait(false);

        Assert.AreEqual(typeof(UnreachableException).FullName, exception.GetType().FullName);
    }

    [TestMethod]
    public async Task FullLifecycle_ConsumesTestNodesAndPublishesArtifactAsync()
    {
        GeneratorTestContext context = CreateGenerator(optionIsSet: true);
        var sessionContext = new TestSessionContextStub();

        await context.Generator.OnTestSessionStartingAsync(sessionContext).ConfigureAwait(false);
        await context.Generator.ConsumeAsync(
            null!,
            CreateMessage("passed", new PassedTestNodeStateProperty()),
            CancellationToken.None).ConfigureAwait(false);
        await context.Generator.ConsumeAsync(
            null!,
            CreateMessage("failed", new FailedTestNodeStateProperty()),
            CancellationToken.None).ConfigureAwait(false);
        await context.Generator.ConsumeAsync(
            null!,
            Mock.Of<IData>(),
            CancellationToken.None).ConfigureAwait(false);
        await context.Generator.OnTestSessionFinishingAsync(sessionContext).ConfigureAwait(false);

        Assert.HasCount(1, context.PublishedArtifacts);
        SessionFileArtifact artifact = context.PublishedArtifacts[0];
        Assert.AreEqual(sessionContext.SessionUid.Value, artifact.SessionUid.Value);
        Assert.EndsWith(".json", artifact.FileInfo.FullName, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(context.ReportStream.GetUtf8Content());
        JsonElement summary = document.RootElement.GetProperty("results").GetProperty("summary");
        Assert.AreEqual(2, summary.GetProperty("tests").GetInt32());
        Assert.AreEqual(1, summary.GetProperty("passed").GetInt32());
        Assert.AreEqual(1, summary.GetProperty("failed").GetInt32());
    }

    [TestMethod]
    public async Task ControllerChild_WritesPersistentJournalAndCompletesAfterArtifactPublicationAsync()
    {
        GeneratorTestContext context = CreateGenerator(optionIsSet: true, isControllerChild: true);
        var sessionContext = new TestSessionContextStub();

        await context.Generator.OnTestSessionStartingAsync(sessionContext).ConfigureAwait(false);
        await context.Generator.ConsumeAsync(
            null!,
            CreateMessage("passed", PassedTestNodeStateProperty.CachedInstance),
            CancellationToken.None).ConfigureAwait(false);
        await context.Generator.OnTestSessionFinishingAsync(sessionContext).ConfigureAwait(false);

        Assert.HasCount(1, context.JournalContentsAtArtifactPublication);
        Assert.DoesNotContain(@"""Type"":2", context.JournalContentsAtArtifactPublication[0]);
        Assert.Contains(@"""Type"":2", context.JournalStream.GetUtf8Content());
        Assert.IsTrue(context.JournalStream.FlushCount is 2 or 3);
        Assert.IsTrue(context.JournalStream.IsDisposed);
        _fileSystemMock.Verify(
            fileSystem => fileSystem.NewFileStream(
                context.JournalPath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite),
            Times.Once);

        context.Generator.Dispose();
    }

    [TestMethod]
    public async Task ControllerChild_WhenJournalWriteFails_StillPublishesNormalArtifactAsync()
    {
        GeneratorTestContext context = CreateGenerator(optionIsSet: true, isControllerChild: true, journalWriteFails: true);
        var sessionContext = new TestSessionContextStub();

        await context.Generator.OnTestSessionStartingAsync(sessionContext).ConfigureAwait(false);
        await context.Generator.ConsumeAsync(
            null!,
            CreateMessage("passed", PassedTestNodeStateProperty.CachedInstance),
            CancellationToken.None).ConfigureAwait(false);
        await context.Generator.OnTestSessionFinishingAsync(sessionContext).ConfigureAwait(false);

        Assert.HasCount(1, context.PublishedArtifacts);
        Assert.IsTrue(context.JournalStream.IsDisposed);
        using var document = JsonDocument.Parse(context.ReportStream.GetUtf8Content());
        Assert.AreEqual(1, document.RootElement.GetProperty("results").GetProperty("summary").GetProperty("passed").GetInt32());
    }

    [TestMethod]
    public async Task ControllerChild_ConsumeDoesNotWaitForJournalDiskWriteAsync()
    {
        GeneratorTestContext context = CreateGenerator(optionIsSet: true, isControllerChild: true, blockJournalWrites: true);
        var sessionContext = new TestSessionContextStub();

        Task start = context.Generator.OnTestSessionStartingAsync(sessionContext);
        Assert.IsTrue(start.IsCompleted);
        await start.ConfigureAwait(false);
        Task consume = context.Generator.ConsumeAsync(
            null!,
            CreateMessage("passed", PassedTestNodeStateProperty.CachedInstance),
            CancellationToken.None);
        Assert.IsTrue(consume.IsCompleted);
        await consume.ConfigureAwait(false);

        context.JournalStream.ReleaseWrites();
        await context.Generator.OnTestSessionFinishingAsync(sessionContext).ConfigureAwait(false);

        Assert.Contains(@"""Type"":2", context.JournalStream.GetUtf8Content());
    }

    [TestMethod]
    public async Task ControllerChild_BoundedJournalOverflow_DoesNotBlockAndNormalReportStillCompletesAsync()
    {
        GeneratorTestContext context = CreateGenerator(optionIsSet: true, isControllerChild: true, blockJournalWrites: true);
        var sessionContext = new TestSessionContextStub();
        int queueCapacity = (int)typeof(CtrfReportEngine).Assembly
            .GetType("Microsoft.Testing.Extensions.ReportGeneratorBase`2", throwOnError: true)!
            .GetField("JournalQueueCapacity", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetRawConstantValue()!;
        int resultCount = queueCapacity + 1;

        await context.Generator.OnTestSessionStartingAsync(sessionContext).ConfigureAwait(false);
        await context.JournalStream.WaitForWriteStartAsync().ConfigureAwait(false);
        for (int i = 0; i < resultCount; i++)
        {
            Task consume = context.Generator.ConsumeAsync(
                null!,
                CreateMessage(i.ToString(CultureInfo.InvariantCulture), PassedTestNodeStateProperty.CachedInstance),
                CancellationToken.None);
            Assert.IsTrue(consume.IsCompleted);
            await consume.ConfigureAwait(false);
        }

        context.JournalStream.ReleaseWrites();
        await context.Generator.OnTestSessionFinishingAsync(sessionContext).ConfigureAwait(false);

        Assert.HasCount(1, context.PublishedArtifacts);
        using var document = JsonDocument.Parse(context.ReportStream.GetUtf8Content());
        Assert.AreEqual(
            resultCount,
            document.RootElement.GetProperty("results").GetProperty("summary").GetProperty("tests").GetInt32());
        context.LoggerMock.Verify(
            logger => logger.Log(
                LogLevel.Warning,
                It.Is<string>(message => message.Contains("dropped", StringComparison.OrdinalIgnoreCase)),
                null,
                It.IsAny<Func<string, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task OnTestSessionFinishingAsync_WithoutWarning_PublishesArtifactWithoutOutputAsync()
    {
        GeneratorTestContext context = CreateGenerator(optionIsSet: true);
        var sessionContext = new TestSessionContextStub();

        await context.Generator.OnTestSessionStartingAsync(sessionContext).ConfigureAwait(false);
        await context.Generator.OnTestSessionFinishingAsync(sessionContext).ConfigureAwait(false);

        Assert.HasCount(1, context.PublishedArtifacts);
        context.OutputDeviceMock.Verify(
            outputDevice => outputDevice.DisplayAsync(
                It.IsAny<IOutputDeviceDataProducer>(),
                It.IsAny<IOutputDeviceData>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private GeneratorTestContext CreateGenerator(
        bool optionIsSet,
        bool isControllerChild = false,
        bool journalWriteFails = false,
        bool blockJournalWrites = false,
        ITask? task = null)
    {
        var reportStream = new MemoryFileStream();
        var journalStream = new MemoryFileStream(journalWriteFails, blockJournalWrites);
        const string journalPath = "report-recovery.jsonl";
        var messageBusMock = new Mock<IMessageBus>();
        var outputDeviceMock = new Mock<IOutputDevice>();
        var loggerFactoryMock = new Mock<ILoggerFactory>();
        var loggerMock = new Mock<ILogger>();
        var testApplicationProcessExitCodeMock = new Mock<ITestApplicationProcessExitCode>();
        List<SessionFileArtifact> publishedArtifacts = [];
        List<string> journalContentsAtArtifactPublication = [];

        _ = _fileSystemMock.Setup(fileSystem => fileSystem.ExistFile(It.IsAny<string>())).Returns(false);
        _ = _fileSystemMock
            .Setup(fileSystem => fileSystem.NewFileStream(It.IsAny<string>(), It.IsAny<FileMode>()))
            .Returns(reportStream);
        _ = _fileSystemMock
            .Setup(fileSystem => fileSystem.NewFileStream(journalPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            .Returns(journalStream);
        _ = _configurationMock.SetupGet(configuration => configuration[It.IsAny<string>()]).Returns(string.Empty);
        _ = _environmentMock.SetupGet(environment => environment.MachineName).Returns("MachineName");
        _ = _environmentMock
            .Setup(environment => environment.GetEnvironmentVariable(It.IsAny<string>()))
            .Returns("user");
        _ = _environmentMock
            .Setup(environment => environment.GetEnvironmentVariable(CtrfReportGenerator.JournalEnvironmentVariableName))
            .Returns(journalPath);
        _ = _environmentMock.SetupGet(environment => environment.ProcessId).Returns(42);
        _ = _testApplicationModuleInfoMock
            .Setup(moduleInfo => moduleInfo.GetCurrentTestApplicationFullPath())
            .Returns("TestAppPath");
        _ = _testFrameworkMock.SetupGet(testFramework => testFramework.Uid).Returns("fake-uid");
        _ = _testFrameworkMock.SetupGet(testFramework => testFramework.Version).Returns("0.0.0");
        _ = _testFrameworkMock.SetupGet(testFramework => testFramework.DisplayName).Returns("Fake");
        _ = messageBusMock
            .Setup(messageBus => messageBus.PublishAsync(It.IsAny<IDataProducer>(), It.IsAny<IData>()))
            .Callback<IDataProducer, IData>((_, data) =>
            {
                journalContentsAtArtifactPublication.Add(journalStream.GetUtf8Content());
                publishedArtifacts.Add((SessionFileArtifact)data);
            })
            .Returns(Task.CompletedTask);
        _ = loggerFactoryMock
            .Setup(loggerFactory => loggerFactory.CreateLogger(It.IsAny<string>()))
            .Returns(loggerMock.Object);
        _ = testApplicationProcessExitCodeMock
            .Setup(exitCode => exitCode.GetProcessExitCode())
            .Returns(0);

        Dictionary<string, string[]> options = [];
        if (optionIsSet)
        {
            options[CtrfReportGeneratorCommandLine.CtrfReportOptionName] = [];
        }

        if (isControllerChild)
        {
            options[PlatformCommandLineProvider.TestHostControllerPIDOptionKey] = ["1"];
        }

        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(_configurationMock.Object);
        serviceProvider.AddService(new TestCommandLineOptions(options));
        serviceProvider.AddService(_fileSystemMock.Object);
        serviceProvider.AddService(_testApplicationModuleInfoMock.Object);
        serviceProvider.AddService(messageBusMock.Object);
        serviceProvider.AddService(Mock.Of<IClock>(clock => clock.UtcNow == DateTimeOffset.UtcNow));
        serviceProvider.AddService(_environmentMock.Object);
        serviceProvider.AddService(outputDeviceMock.Object);
        serviceProvider.AllowTestAdapterFrameworkRegistration = true;
        serviceProvider.AddService(_testFrameworkMock.Object);
        serviceProvider.AddService(testApplicationProcessExitCodeMock.Object);
        serviceProvider.AddService(loggerFactoryMock.Object);
        serviceProvider.AddService(task ?? new SystemTask());

        return new(
            new CtrfReportGenerator(serviceProvider),
            outputDeviceMock,
            publishedArtifacts,
            reportStream,
            journalStream,
            journalContentsAtArtifactPublication,
            journalPath,
            loggerMock);
    }

    private static TestNodeUpdateMessage CreateMessage(string uid, IProperty state)
        => new(
            new SessionUid("source-session"),
            new TestNode
            {
                Uid = uid,
                DisplayName = uid,
                Properties = new PropertyBag(state),
            });

    private sealed class GeneratorTestContext(
        CtrfReportGenerator generator,
        Mock<IOutputDevice> outputDeviceMock,
        List<SessionFileArtifact> publishedArtifacts,
        MemoryFileStream reportStream,
        MemoryFileStream journalStream,
        List<string> journalContentsAtArtifactPublication,
        string journalPath,
        Mock<ILogger> loggerMock)
    {
        public CtrfReportGenerator Generator { get; } = generator;

        public Mock<IOutputDevice> OutputDeviceMock { get; } = outputDeviceMock;

        public List<SessionFileArtifact> PublishedArtifacts { get; } = publishedArtifacts;

        public MemoryFileStream ReportStream { get; } = reportStream;

        public MemoryFileStream JournalStream { get; } = journalStream;

        public List<string> JournalContentsAtArtifactPublication { get; } = journalContentsAtArtifactPublication;

        public string JournalPath { get; } = journalPath;

        public Mock<ILogger> LoggerMock { get; } = loggerMock;
    }

    private sealed class TestSessionContextStub : ITestSessionContext
    {
        public SessionUid SessionUid { get; } = new("finishing-session");

        public CancellationToken CancellationToken { get; } = CancellationToken.None;
    }

    private sealed class MemoryFileStream : IFileStream
    {
        private readonly TaskCompletionSource<object?> _writeGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<object?> _writeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CountingMemoryStream _stream;

        public MemoryFileStream()
            : this(throwOnWrite: false, blockWrites: false)
        {
        }

        public MemoryFileStream(bool throwOnWrite, bool blockWrites)
        {
            if (!blockWrites)
            {
                _writeGate.SetResult(null);
            }

            _stream = new(throwOnWrite, _writeGate.Task, _writeStarted);
        }

        public int FlushCount => _stream.FlushCount;

        public bool IsDisposed { get; private set; }

        Stream IFileStream.Stream => _stream;

        string IFileStream.Name => string.Empty;

        public string GetUtf8Content() => Encoding.UTF8.GetString(_stream.ToArray());

        public Task WaitForWriteStartAsync() => _writeStarted.Task;

        public void ReleaseWrites() => _writeGate.TrySetResult(null);

        void IDisposable.Dispose()
        {
            IsDisposed = true;
            _stream.Dispose();
        }

#if NETCOREAPP
        ValueTask IAsyncDisposable.DisposeAsync() => _stream.DisposeAsync();
#endif

        private sealed class CountingMemoryStream(
            bool throwOnWrite,
            Task writeGate,
            TaskCompletionSource<object?> writeStarted) : MemoryStream
        {
            public int FlushCount { get; private set; }

            public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                writeStarted.TrySetResult(null);
                await writeGate.ConfigureAwait(false);
                if (throwOnWrite)
                {
                    throw new IOException("Simulated journal write failure.");
                }

                await base.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            }

#if NETCOREAPP
            public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                writeStarted.TrySetResult(null);
                await writeGate.ConfigureAwait(false);
                if (throwOnWrite)
                {
                    throw new IOException("Simulated journal write failure.");
                }

                await base.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
#endif

            public override void Flush()
            {
                FlushCount++;
                base.Flush();
            }
        }
    }
}
