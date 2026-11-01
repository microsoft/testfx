// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ctrf;

using System.Text.Json;

using Microsoft.Testing.Extensions.CtrfReport;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

using EngineContext = ctrf::Microsoft.Testing.Extensions.ReportEngineContext;
using JournalRecord = ctrf::Microsoft.Testing.Extensions.ReportJournalRecord<Microsoft.Testing.Extensions.CtrfReport.CapturedTestResult>;
using ParentEntry = ctrf::Microsoft.Testing.Extensions.ReportJournalParentEntry;
using RecoveredMetadata = ctrf::Microsoft.Testing.Extensions.RecoveredReportMetadata;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class ReportGeneratorContractBehaviorTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false, DisplayName = "Shared generator service constructor")]
    [DataRow(true, DisplayName = "Shared generator legacy full constructor")]
    public async Task ConstructorVariants_PreserveLifecycleInputsAndArtifactOrder(bool legacy)
    {
        var fixture = new ReportBehaviorFixture(new() { ["probe"] = [] });
        using ProbeGenerator generator = legacy ? new(fixture) : new(fixture.Services);
        using var sessionCancellation = new CancellationTokenSource();
        using var messageCancellation = new CancellationTokenSource();
        var session = new ReportBehaviorFixture.Session(sessionCancellation.Token);
        Assert.IsTrue(await generator.IsEnabledAsync());
        await generator.OnTestSessionStartingAsync(session);
        await generator.ConsumeAsync(null!, Node("first", "First", true), messageCancellation.Token);
        await generator.ConsumeAsync(null!, Node("ignored", "Ignored", false), messageCancellation.Token);
        await generator.ConsumeAsync(null!, Node("second", "Second", true), messageCancellation.Token);
        fixture.Clock.SetupGet(value => value.UtcNow).Returns(ReportBehaviorFixture.Start.AddHours(1));

        await generator.OnTestSessionFinishingAsync(session);

        Assert.AreSequenceEqual(new[] { "first:First", "second:Second" }, generator.RenderedTests.Select(value => value.Uid + ":" + value.DisplayName));
        Assert.AreEqual(ReportBehaviorFixture.Start, generator.RenderedStart);
        Assert.AreEqual(0, generator.RenderedExitCode);
        Assert.AreEqual(sessionCancellation.Token, generator.RenderedToken);
        Assert.AreNotEqual(messageCancellation.Token, generator.RenderedToken);
        Assert.AreSame(fixture.FileSystem.Object, generator.Deconstructed.FileSystem);
        Assert.AreSame(fixture.Module.Object, generator.Deconstructed.Module);
        Assert.AreSame(fixture.Environment.Object, generator.Deconstructed.Environment);
        Assert.AreSame(fixture.Services.GetCommandLineOptions(), generator.Deconstructed.Options);
        Assert.AreSame(fixture.Configuration.Object, generator.Deconstructed.Configuration);
        Assert.AreSame(fixture.Clock.Object, generator.Deconstructed.Clock);
        Assert.AreSame(fixture.Framework.Object, generator.Deconstructed.Framework);
        Assert.AreEqual(ReportBehaviorFixture.Start, generator.Deconstructed.Start);
        Assert.AreEqual(0, generator.Deconstructed.ExitCode);
        Assert.AreEqual(sessionCancellation.Token, generator.Deconstructed.Token);
        Assert.AreSequenceEqual(new[] { "probe warning" }, fixture.Warnings);
        Assert.AreSequenceEqual(new[] { "warning", "artifact" }, fixture.Events);
        SessionFileArtifact artifact = Assert.IsInstanceOfType<SessionFileArtifact>(Assert.ContainsSingle(fixture.Artifacts));
        Assert.AreEqual("report-session", artifact.SessionUid.Value);
        Assert.AreEqual("probe.report", artifact.FileInfo.Name);
        Assert.AreEqual("Probe artifact", artifact.DisplayName);
        Assert.AreEqual("Probe description", artifact.Description);
        Assert.IsNull(artifact.Kind);
    }

    [TestMethod]
    public void NullServices_RejectBeforeResolvingCollaborators()
    {
        ArgumentNullException error = Assert.ThrowsExactly<ArgumentNullException>(() => new ProbeGenerator((IServiceProvider)null!));

        Assert.AreEqual("serviceProvider", error.ParamName);
    }

    [TestMethod]
    public async Task RecoveredGeneration_RestoresParentsAndOriginalMetadataBeforeRendering()
    {
        var fixture = new ReportBehaviorFixture(new() { ["probe"] = [] });
        var metadata = new RecoveredMetadata
        {
            StartTime = ReportBehaviorFixture.Start.AddMinutes(-7),
            ProcessId = 314,
            FrameworkUid = "recovered-fw",
            FrameworkVersion = "3.2.1",
            FrameworkDisplayName = "Recovered framework",
            IsIncomplete = true,
        };
        using var generator = new ProbeGenerator(fixture, metadata);
        var result = new CapturedTestResult { Uid = "recovered", DisplayName = "Recovered result", Status = "passed" };
        var journal = new ctrf::Microsoft.Testing.Extensions.ReportJournalReadResult<CapturedTestResult>(
            metadata, [result],
            [
                new ParentEntry { Uid = "parent", DisplayName = "Root" },
                new ParentEntry { Uid = "child", DisplayName = "Child", ParentUid = "parent" },
            ],
            Completed: false, CompletedReportFileName: null, DroppedJournalRecords: 0, IsPartial: false);
        using var cancellation = new CancellationTokenSource();

        (string fileName, string? warning) = await generator.GenerateRecoveredReportAsync(journal, 19, cancellation.Token);

        Assert.AreEqual("probe.report", fileName);
        Assert.AreEqual("probe warning", warning);
        Assert.AreSequenceEqual(new[] { "parent:Root:<none>", "child:Child:parent" }, generator.RestoredParents);
        Assert.AreSame(result, Assert.ContainsSingle(generator.RenderedTests));
        Assert.AreEqual(metadata.StartTime, generator.RenderedStart);
        Assert.AreEqual(19, generator.RenderedExitCode);
        Assert.AreEqual(cancellation.Token, generator.RenderedToken);
        Assert.AreEqual(314, generator.Context.ProcessId);
        Assert.IsTrue(generator.Context.IsIncomplete);
        Assert.AreEqual("recovered-fw", generator.Deconstructed.Framework.Uid);
        Assert.AreEqual("3.2.1", generator.Deconstructed.Framework.Version);
        Assert.AreEqual("Recovered framework", generator.Deconstructed.Framework.DisplayName);
        Assert.IsEmpty(fixture.Artifacts);
    }

    [TestMethod]
    public async Task FaultedJournalTask_PreservesPublicationAndOriginalFailureDuringCleanup()
    {
        var fixture = new ReportBehaviorFixture(new() { ["probe"] = [] });
        fixture.Logger.Setup(value => value.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var original = new IOException("writer task failed");
        fixture.TaskService.Setup(value => value.RunLongRunning(It.IsAny<Func<Task>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromException(original));
        var generator = new ProbeGenerator(fixture, "journal.jsonl") { Warning = null };
        var session = new ReportBehaviorFixture.Session();
        await generator.OnTestSessionStartingAsync(session);
        await generator.ConsumeAsync(null!, Node("first", "First", true), CancellationToken.None);

        IOException error = await Assert.ThrowsExactlyAsync<IOException>(() => generator.OnTestSessionFinishingAsync(session));
        generator.Dispose();
        generator.Dispose();

        Assert.AreSame(original, error);
        Assert.ContainsSingle(fixture.Artifacts);
        Assert.IsEmpty(fixture.WrittenFiles);
        string expected = $"Failed to drain report recovery journal 'journal.jsonl' during disposal: {new AggregateException(original).Message}";
        fixture.Logger.Verify(
            value => value.Log(
            LogLevel.Debug, expected, null, It.IsAny<Func<string, Exception?, string>>()), Times.Once);
    }

    [TestMethod]
    public async Task JournalCloseFailure_DoesNotLoseCompletedRecordsOrPublishedArtifact()
    {
        var fixture = new ReportBehaviorFixture(new() { ["probe"] = [] });
        fixture.Logger.Setup(value => value.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        using var bytes = new MemoryStream();
        var stream = new Mock<IFileStream>();
        stream.SetupGet(value => value.Stream).Returns(bytes);
        stream.Setup(value => value.Dispose()).Throws(new IOException("close denied"));
        fixture.FileSystem.Setup(value => value.NewFileStream("journal.jsonl", FileMode.Append, FileAccess.Write, FileShare.ReadWrite)).Returns(stream.Object);
        using var generator = new ProbeGenerator(fixture, "journal.jsonl") { Warning = null };
        var session = new ReportBehaviorFixture.Session();
        await generator.OnTestSessionStartingAsync(session);
        await generator.ConsumeAsync(null!, Node("first", "First", true), CancellationToken.None);

        await fixture.FinishAsync(generator, session);

        string[] records = Encoding.UTF8.GetString(bytes.ToArray()).Split(['\n'], StringSplitOptions.RemoveEmptyEntries);
        Assert.HasCount(3, records);
        using var completion = JsonDocument.Parse(records[2]);
        Assert.AreEqual(2, completion.RootElement.GetProperty("Type").GetInt32());
        Assert.AreEqual(0, completion.RootElement.GetProperty("DroppedJournalRecords").GetInt32());
        Assert.AreEqual(Path.GetFullPath("probe.report"), completion.RootElement.GetProperty("ReportFileName").GetString());
        Assert.ContainsSingle(fixture.Artifacts);
        stream.Verify(value => value.Dispose(), Times.Once);
        fixture.Logger.Verify(
            value => value.Log(
            LogLevel.Debug, "Failed to close report recovery journal 'journal.jsonl': close denied",
            null, It.IsAny<Func<string, Exception?, string>>()), Times.Once);
    }

#if NETCOREAPP
    [TestMethod]
    [DataRow(-1, DisplayName = "Recovery record one byte below the inclusive limit")]
    [DataRow(0, DisplayName = "Recovery record exactly at the inclusive limit")]
    [DataRow(1, DisplayName = "Recovery record one byte above the inclusive limit")]
    public async Task JournalRecordLimit_UsesInclusiveByteBoundaryWithoutDroppingNormalResults(int offset)
    {
        const int recordLimit = 4 * 1024 * 1024;
        var fixture = new ReportBehaviorFixture(new() { ["probe"] = [] });
        var flushed = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var joined = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<Task>? writer = null;
        fixture.TaskService.Setup(value => value.RunLongRunning(It.IsAny<Func<Task>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Func<Task>, string, CancellationToken>((action, _, _) => writer = action)
            .Returns(joined.Task);
        using var bytes = new FlushSignalingStream(flushed);
        var stream = new Mock<IFileStream>();
        stream.SetupGet(value => value.Stream).Returns(bytes);
        fixture.FileSystem.Setup(value => value.NewFileStream("limit.jsonl", FileMode.Append, FileAccess.Write, FileShare.ReadWrite)).Returns(stream.Object);
        using var generator = new ProbeGenerator(fixture, "limit.jsonl") { Warning = null };
        var session = new ReportBehaviorFixture.Session();
        int fixedRecordBytes = ProbeGenerator.MeasureRecordBytes(Node("oversized", string.Empty, true));
        TestNodeUpdateMessage large = Node("oversized", new string('x', recordLimit - fixedRecordBytes + offset), true);
        Assert.AreEqual(recordLimit + offset, ProbeGenerator.MeasureRecordBytes(large));
        await generator.OnTestSessionStartingAsync(session);
        await generator.ConsumeAsync(null!, large, CancellationToken.None);
        await generator.ConsumeAsync(null!, Node("queued-after-large", "Queued", true), CancellationToken.None);
        Assert.IsNotNull(writer);
        // The writer must start even if the test is canceled so cleanup can seal and join it.
        var runningWriter = Task.Run(
            async () =>
        {
            try
            {
                await writer();
                joined.TrySetResult(null);
            }
            catch (Exception error)
            {
                joined.TrySetException(error);
            }
        }, CancellationToken.None);
        bool finishingStarted = false;
        try
        {
            // This is a failure guard, not a timing oracle. Flush occurs only after the prepared
            // batch's byte-limit decision, before the next update is supplied.
            await flushed.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
            await generator.ConsumeAsync(null!, Node("after-budget", "After budget", true), CancellationToken.None);
            finishingStarted = true;
            await generator.OnTestSessionFinishingAsync(session);
            await runningWriter.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

            Assert.AreSequenceEqual(new[] { "oversized", "queued-after-large", "after-budget" }, generator.RenderedTests.Select(value => value.Uid));
            Assert.ContainsSingle(fixture.Artifacts);
            string[] records = Encoding.UTF8.GetString(bytes.ToArray()).Split(['\n'], StringSplitOptions.RemoveEmptyEntries);
            Assert.HasCount(offset > 0 ? 2 : 5, records);
            using var completion = JsonDocument.Parse(records[^1]);
            Assert.AreEqual(2, completion.RootElement.GetProperty("Type").GetInt32());
            Assert.AreEqual(offset > 0 ? 3 : 0, completion.RootElement.GetProperty("DroppedJournalRecords").GetInt32());
            if (offset <= 0)
            {
                using var captured = JsonDocument.Parse(records[1]);
                Assert.AreEqual(large.TestNode.DisplayName, captured.RootElement.GetProperty("Result").GetProperty("DisplayName").GetString());
            }
        }
        finally
        {
            if (!finishingStarted)
            {
                await generator.OnTestSessionFinishingAsync(session);
            }

            await runningWriter.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None);
        }
    }
#endif

    private static TestNodeUpdateMessage Node(string uid, string name, bool terminal)
        => new(new SessionUid("source"), new TestNode
        {
            Uid = uid,
            DisplayName = name,
            Properties = terminal ? new(PassedTestNodeStateProperty.CachedInstance) : new(InProgressTestNodeStateProperty.CachedInstance),
        });

#if NETCOREAPP
    private sealed class FlushSignalingStream(TaskCompletionSource<object?> flushed) : MemoryStream
    {
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            Task result = base.FlushAsync(cancellationToken);
            flushed.TrySetResult(null);
            return result;
        }
    }
#endif

    private sealed class ProbeGenerator : ctrf::Microsoft.Testing.Extensions.ReportGeneratorBase<ProbeGenerator, CapturedTestResult>
    {
        public ProbeGenerator(IServiceProvider services)
            : base(services, "probe")
        {
        }

        public ProbeGenerator(ReportBehaviorFixture fixture)
            : base(
                fixture.Configuration.Object, fixture.Services.GetCommandLineOptions(), fixture.FileSystem.Object,
                fixture.Module.Object, fixture.MessageBus.Object, fixture.Clock.Object, fixture.Environment.Object,
                fixture.Output.Object, fixture.Framework.Object, fixture.Services.GetTestApplicationProcessExitCode(),
                fixture.LoggerFactory.Object.CreateLogger<ProbeGenerator>(), "probe")
        {
        }

        public ProbeGenerator(ReportBehaviorFixture fixture, string journalPath)
            : base(
                fixture.Configuration.Object, fixture.Services.GetCommandLineOptions(), fixture.FileSystem.Object,
                fixture.Module.Object, fixture.MessageBus.Object, fixture.Clock.Object, fixture.Environment.Object,
                fixture.Output.Object, fixture.Framework.Object, fixture.Services.GetTestApplicationProcessExitCode(),
                fixture.LoggerFactory.Object.CreateLogger<ProbeGenerator>(), fixture.TaskService.Object, "probe", journalPath)
        {
        }

        public ProbeGenerator(ReportBehaviorFixture fixture, RecoveredMetadata metadata)
            : base(fixture.Services, "probe", metadata)
        {
        }

        public override string Uid => "probe";

        public override string DisplayName => "Probe generator";

        public override string Description => "Recording generator contract";

        public string? Warning { get; set; } = "probe warning";

        public CapturedTestResult[] RenderedTests { get; private set; } = [];

        public DateTimeOffset RenderedStart { get; private set; }

        public int RenderedExitCode { get; private set; }

        public CancellationToken RenderedToken { get; private set; }

        public EngineContext Context { get; private set; }

        public (IFileSystem FileSystem, ITestApplicationModuleInfo Module, IEnvironment Environment,
            Platform.CommandLine.ICommandLineOptions Options, IConfiguration Configuration, IClock Clock,
            ITestFramework Framework, DateTimeOffset Start, int ExitCode, CancellationToken Token) Deconstructed
        { get; private set; }

        public List<string> RestoredParents { get; } = [];

        protected override string ArtifactDisplayName => "Probe artifact";

        protected override string ArtifactDescription => "Probe description";

        protected override string GetGenerationLogMessage(int testResultCount) => $"Rendering {testResultCount} captured results.";

        protected override string SerializeJournalRecord(JournalRecord record)
            => JsonSerializer.Serialize(record, typeof(JournalRecord), ReportJournalJsonSerializerContext.Default);

        public static int MeasureRecordBytes(TestNodeUpdateMessage update)
        {
            var result = new CapturedTestResult { Uid = update.TestNode.Uid.Value, DisplayName = update.TestNode.DisplayName!, Status = "passed" };
            var record = JournalRecord.CreateTest(result, parent: null);
            return Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(record, typeof(JournalRecord), ReportJournalJsonSerializerContext.Default)) + 1;
        }

        protected override CapturedTestResult? TryCapture(TestNodeUpdateMessage update)
            => update.TestNode.Properties.OfType<PassedTestNodeStateProperty>().Any()
                ? new CapturedTestResult { Uid = update.TestNode.Uid.Value, DisplayName = update.TestNode.DisplayName!, Status = "passed" }
                : null;

        protected override void RestoreParentEntry(ParentEntry parent)
            => RestoredParents.Add(parent.Uid + ":" + parent.DisplayName + ":" + (parent.ParentUid ?? "<none>"));

        protected override Task<(string FileName, string? Warning)> GenerateReportAsync(
            CapturedTestResult[] tests, DateTimeOffset testStartTime, int exitCode, CancellationToken cancellationToken)
        {
            RenderedTests = tests;
            RenderedStart = testStartTime;
            RenderedExitCode = exitCode;
            RenderedToken = cancellationToken;
            Context = CreateReportEngineContext(testStartTime, exitCode, cancellationToken);
            Context.Deconstruct(out IFileSystem fileSystem, out ITestApplicationModuleInfo module,
                out IEnvironment environment, out Platform.CommandLine.ICommandLineOptions options, out IConfiguration configuration,
                out IClock clock, out ITestFramework framework, out DateTimeOffset start, out int actualExitCode, out CancellationToken token);
            Deconstructed = (fileSystem, module, environment, options, configuration, clock, framework, start, actualExitCode, token);
            return Task.FromResult(("probe.report", Warning));
        }
    }
}
