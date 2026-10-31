// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.TestHost;

using Moq;

using TestSessionContext = Microsoft.Testing.Platform.Services.TestSessionContext;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TrxConsumerBehaviorTests
{
    private static readonly XNamespace Ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    [TestMethod]
    public async Task Session_FullLifecycleStreamsOnlyFinalResultsAndPublishesContract()
    {
        using var fixture = new TrxBehaviorFixture();
        using var cancellation = new CancellationTokenSource();
        var session = new TestSessionContext(cancellation.Token);
        TrxReportGenerator generator = fixture.CreateGenerator();
        await generator.OnTestSessionStartingAsync(session);
        await generator.ConsumeAsync(null!, Update("absent", null), cancellation.Token);
        await generator.ConsumeAsync(null!, Update("discovered", DiscoveredTestNodeStateProperty.CachedInstance), cancellation.Token);
        await generator.ConsumeAsync(null!, Update("running", InProgressTestNodeStateProperty.CachedInstance), cancellation.Token);
        await generator.ConsumeAsync(null!, Mock.Of<IData>(), cancellation.Token);
        await generator.ConsumeAsync(null!, Update("passed", new FailedTestNodeStateProperty(), new RetryAttemptProperty(1, isSuperseded: true)), cancellation.Token);
        Assert.IsFalse(fixture.Scheduler.HasWriter, "Nonfinal updates must not provision the lazy sidecar.");

        await generator.ConsumeAsync(null!, Update("passed", PassedTestNodeStateProperty.CachedInstance, new RetryAttemptProperty(2, isSuperseded: false)), cancellation.Token);
        await generator.ConsumeAsync(null!, Update("failed", new FailedTestNodeStateProperty(), new TrxExceptionProperty("failure detail", "stack detail")), cancellation.Token);
        await generator.ConsumeAsync(null!, Update("skipped", SkippedTestNodeStateProperty.CachedInstance), cancellation.Token);
        await generator.ConsumeAsync(null!, Update("timed-out", new TimeoutTestNodeStateProperty()), cancellation.Token);
        fixture.Clock.SetupGet(value => value.UtcNow).Returns(TrxBehaviorFixture.Start.AddSeconds(5));
        fixture.ExitCode.Setup(value => value.GetProcessExitCode()).Returns(2);
        await fixture.FinishAsync(generator, session);

        Assert.AreSequenceEqual(
            ["case passed", "case failed", "case skipped", "case timed-out"],
            fixture.Report.Descendants(Ns + "UnitTestResult").Select(value => (string?)value.Attribute("testName")));
        Assert.AreSequenceEqual(
            ["Passed", "Failed", "NotExecuted", "Failed"],
            fixture.Report.Descendants(Ns + "UnitTestResult").Select(value => (string?)value.Attribute("outcome")));
        XElement counters = fixture.Report.Descendants(Ns + "Counters").Single();
        Assert.AreEqual("4", (string?)counters.Attribute("total"));
        Assert.AreEqual("2", (string?)counters.Attribute("executed"));
        Assert.AreEqual("1", (string?)counters.Attribute("passed"));
        Assert.AreEqual("1", (string?)counters.Attribute("failed"));
        Assert.AreEqual("1", (string?)counters.Attribute("notExecuted"));
        Assert.AreEqual("1", (string?)counters.Attribute("timeout"));
        Assert.AreEqual("Failed", (string?)fixture.Report.Descendants(Ns + "ResultSummary").Single().Attribute("outcome"));
        Assert.AreEqual("failure detail", fixture.Report.Descendants(Ns + "ErrorInfo").Single().Element(Ns + "Message")!.Value);
        Assert.AreEqual("stack detail", fixture.Report.Descendants(Ns + "ErrorInfo").Single().Element(Ns + "StackTrace")!.Value);
        Assert.AreEqual(TrxBehaviorFixture.Start, (DateTimeOffset)fixture.Report.Descendants(Ns + "Times").Single().Attribute("start")!);
        Assert.AreEqual(TrxBehaviorFixture.Start.AddSeconds(5), (DateTimeOffset)fixture.Report.Descendants(Ns + "Times").Single().Attribute("finish")!);
        Assert.IsTrue(fixture.Report.Descendants(Ns + "TestMethod").All(value =>
            (string?)value.Attribute("adapterTypeName") == "executor://fixture.adapter/1.2.3" &&
            (string?)value.Attribute("className") == "Suite.Tests" && (string?)value.Attribute("name") == "Method"));
        string[] definitions = [.. fixture.Report.Descendants(Ns + "UnitTest").Select(value => (string)value.Attribute("id")!)];
        Assert.HasCount(4, definitions.Distinct());
        Assert.IsTrue(fixture.Report.Descendants(Ns + "TestEntry").All(value => definitions.Contains((string)value.Attribute("testId")!)));
        (IDataProducer Producer, IData Data) published = Assert.ContainsSingle(fixture.Publications);
        SessionFileArtifact artifact = Assert.IsInstanceOfType<SessionFileArtifact>(published.Data);
        Assert.AreSame(generator, published.Producer);
        Assert.AreEqual(session.SessionUid, artifact.SessionUid);
        Assert.AreEqual(fixture.ReportPath, artifact.FileInfo.FullName);
        Assert.AreEqual(TrxReportEngine.TrxArtifactKind, artifact.Kind);
        Assert.AreEqual(TrxReport.Resources.ExtensionResources.TrxReportArtifactDisplayName, artifact.DisplayName);
        Assert.AreEqual(TrxReport.Resources.ExtensionResources.TrxReportArtifactDescription, artifact.Description);
        fixture.Capability.Verify(value => value.Enable(), Times.Once);
        Assert.IsEmpty(fixture.Displays);
        Assert.AreEqual("TRX streaming store writer", fixture.Scheduler.WriterName);
        Assert.AreEqual(CancellationToken.None, fixture.Scheduler.WriterToken);
        Assert.DoesNotContain(path => path.EndsWith(".bin", StringComparison.Ordinal), fixture.Files.Keys);
        string deletion = Assert.ContainsSingle(fixture.Events.Where(value => value.StartsWith("delete:", StringComparison.Ordinal)));
        string sidecar = deletion["delete:".Length..];
        Assert.IsLessThan(fixture.Events.IndexOf(deletion), fixture.Events.IndexOf("dispose:" + sidecar));
        Assert.AreEqual("publish", fixture.Events[^1]);
    }

    [TestMethod]
    public async Task Session_ZeroResultsAvoidsWriterAndProducesEmptyReport()
    {
        using var fixture = new TrxBehaviorFixture();
        TrxReportGenerator generator = fixture.CreateGenerator();
        await generator.OnTestSessionStartingAsync(new TestSessionContext(CancellationToken.None));

        await fixture.FinishAsync(generator, TestContext.CancellationToken);

        Assert.IsFalse(fixture.Scheduler.HasWriter);
        Assert.IsNull(fixture.Scheduler.WriterName);
        Assert.IsEmpty(fixture.Scheduler.Delays);
        fixture.FileSystem.Verify(
            value => value.NewFileStream(
            It.IsAny<string>(), It.IsAny<FileMode>(), It.IsAny<FileAccess>(), It.IsAny<FileShare>()), Times.Never);
        Assert.IsEmpty(fixture.Report.Descendants(Ns + "UnitTestResult"));
        Assert.AreEqual("0", (string?)fixture.Report.Descendants(Ns + "Counters").Single().Attribute("total"));
        Assert.AreEqual("Completed", (string?)fixture.Report.Descendants(Ns + "ResultSummary").Single().Attribute("outcome"));
        Assert.HasCount(1, fixture.Publications);
        Assert.IsEmpty(fixture.Displays);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "TrxSession_NoCapability")]
    [DataRow(true, DisplayName = "TrxSession_UnsupportedCapability")]
    public async Task Session_UnsupportedCapabilityWarnsBeforePublication(bool present)
    {
        using var fixture = new TrxBehaviorFixture();
        fixture.Capability.SetupGet(value => value.IsSupported).Returns(false);
        fixture.Capabilities = present ? new TestFrameworkCapabilities(fixture.Capability.Object) : new TestFrameworkCapabilities();
        TrxReportGenerator generator = fixture.CreateGenerator();
        await generator.OnTestSessionStartingAsync(new TestSessionContext(CancellationToken.None));

        await fixture.FinishAsync(generator, TestContext.CancellationToken);

        (IOutputDeviceDataProducer Producer, IOutputDeviceData Data, CancellationToken Token) display = Assert.ContainsSingle(fixture.Displays);
        string expected = string.Format(
            CultureInfo.InvariantCulture,
            TrxReport.Resources.ExtensionResources.TrxReportFrameworkDoesNotSupportTrxReportCapability, "Fixture framework", "fixture.adapter");
        Assert.AreEqual(expected, Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(display.Data).Message);
        Assert.AreSame(generator, display.Producer);
        Assert.IsLessThan(fixture.Events.IndexOf("publish"), fixture.Events.IndexOf("display"));
        fixture.Capability.Verify(value => value.Enable(), Times.Never);
        Assert.AreEqual("0", (string?)fixture.Report.Descendants(Ns + "Counters").Single().Attribute("total"));
    }

    [TestMethod]
    [DataRow("consume")]
    [DataRow("starting")]
    [DataRow("finishing")]
    public async Task Callbacks_PreCanceledTokenStopsBeforeAnyWork(string callback)
    {
        using var fixture = new TrxBehaviorFixture();
        TrxReportGenerator generator = fixture.CreateGenerator();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var session = new TestSessionContext(cancellation.Token);

        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => callback switch
        {
            "consume" => generator.ConsumeAsync(null!, null!, cancellation.Token),
            "starting" => generator.OnTestSessionStartingAsync(session),
            _ => generator.OnTestSessionFinishingAsync(session),
        });

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        Assert.IsEmpty(fixture.Events);
        Assert.IsEmpty(fixture.Logs);
        fixture.FileSystem.VerifyNoOtherCalls();
        fixture.Capability.Verify(value => value.Enable(), Times.Never);
        Assert.IsFalse(fixture.Scheduler.HasWriter);
    }

    [TestMethod]
    public async Task Session_FinishBeforeStartCannotInventReport()
    {
        using var fixture = new TrxBehaviorFixture();
        TrxReportGenerator generator = fixture.CreateGenerator();

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => generator.OnTestSessionFinishingAsync(new TestSessionContext(CancellationToken.None)));

        Assert.Contains("Unexpected state", exception.Message);
        Assert.HasCount(1, fixture.Displays, "The capability warning precedes the missing-start invariant.");
        Assert.IsEmpty(fixture.Publications);
        Assert.IsEmpty(fixture.Files);
        fixture.FileSystem.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task Session_AttachmentFailureWarnsBeforePublicationAndRetainsOtherArtifacts()
    {
        using var fixture = new TrxBehaviorFixture();
        TrxReportGenerator generator = fixture.CreateGenerator();
        var session = new TestSessionContext(CancellationToken.None);
        await generator.OnTestSessionStartingAsync(session);
        IDataProducer first = Producer("collector-one", "First collector");
        IDataProducer second = Producer("collector-two", "Second collector");
        string firstPath = Path.Combine(fixture.Root, "first", "trace.log");
        string secondPath = Path.Combine(fixture.Root, "second", "trace.log");
        string missingPath = Path.Combine(fixture.Root, "missing.log");
        fixture.ExistingFiles[firstPath] = [1, 2, 3];
        fixture.ExistingFiles[secondPath] = [4, 5, 6];
        fixture.ExistingFiles[fixture.ReportPath] = Encoding.UTF8.GetBytes("stale report");
        fixture.CopyFailure = path =>
        {
            if (path == missingPath)
            {
                throw new IOException("missing attachment");
            }
        };
        await generator.ConsumeAsync(first, Artifact(session.SessionUid, firstPath), CancellationToken.None);
        await generator.ConsumeAsync(first, Artifact(session.SessionUid, missingPath), CancellationToken.None);
        await generator.ConsumeAsync(second, Artifact(session.SessionUid, secondPath), CancellationToken.None);

        await fixture.FinishAsync(generator, session);

        Assert.AreSequenceEqual(
            ["datacollector://collector-one/1.0", "datacollector://collector-two/1.0"],
            fixture.Report.Descendants(Ns + "Collector").Select(value => (string?)value.Attribute("uri")));
        Assert.AreSequenceEqual(
            ["First collector", "Second collector"],
            fixture.Report.Descendants(Ns + "Collector").Select(value => (string?)value.Attribute("collectorDisplayName")));
        Assert.AreSequenceEqual(
            [Path.Combine("agent", "trace.log"), Path.Combine("agent", "trace_1.log")],
            fixture.Report.Descendants(Ns + "A").Select(value => (string?)value.Attribute("href")));
        Assert.HasCount(2, fixture.Copies);
        Assert.AreSequenceEqual(new byte[] { 1, 2, 3 }, fixture.GetBytes(fixture.Copies[0].Destination));
        Assert.AreSequenceEqual(new byte[] { 4, 5, 6 }, fixture.GetBytes(fixture.Copies[1].Destination));
        Assert.AreSequenceEqual(
            [
                string.Format(CultureInfo.InvariantCulture, TrxReport.Resources.ExtensionResources.TrxFileExistsAndWillBeOverwritten, fixture.ReportPath),
                string.Format(CultureInfo.InvariantCulture, TrxReport.Resources.ExtensionResources.TrxAttachmentCopyFailed, missingPath, nameof(IOException)),
            ],
            fixture.Displays.Select(value => Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(value.Data).Message));
        Assert.AreEqual(
            Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(fixture.Displays[1].Data).Message,
            fixture.Report.Descendants(Ns + "RunInfo").Single().Element(Ns + "Text")!.Value);
        Assert.IsLessThan(fixture.Events.IndexOf("publish"), fixture.Events.LastIndexOf("display"));
        Assert.HasCount(1, fixture.Publications);
    }

    [TestMethod]
    public async Task Session_TruncationWarningsCountResultsNotFields()
    {
        using var fixture = new TrxBehaviorFixture();
        TrxReportGenerator generator = fixture.CreateGenerator();
        await generator.OnTestSessionStartingAsync(new TestSessionContext(CancellationToken.None));
        const int Limit = 1024 * 1024;
        const string Suffix = "\n... [truncated by TRX streaming store]";
        string text = new('x', Limit + 17);
        await generator.ConsumeAsync(null!, Update("output", PassedTestNodeStateProperty.CachedInstance,
            new TrxMessagesProperty([new StandardOutputTrxMessage(text), new StandardErrorTrxMessage(text)])), CancellationToken.None);
        await generator.ConsumeAsync(null!, Update("exception", new FailedTestNodeStateProperty(),
            new TrxExceptionProperty(text, text)), CancellationToken.None);

        await fixture.FinishAsync(generator, TestContext.CancellationToken);

        string warning = Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(Assert.ContainsSingle(fixture.Displays).Data).Message;
        Assert.AreEqual("TRX streaming store truncated captured text fields (stdout / stderr / exception messages / stack traces) on 2 test result(s) because individual fields exceeded the per-field size cap. The truncated regions are marked '[truncated by TRX streaming store]' in the TRX.", warning);
        XElement[] fields = [.. fixture.Report.Descendants().Where(value => value.Name.LocalName is "StdOut" or "StdErr" or "Message" or "StackTrace")];
        Assert.HasCount(4, fields);
        foreach (XElement field in fields)
        {
            Assert.AreEqual(Limit + Suffix.Length, field.Value.Length);
            Assert.AreEqual(new string('x', Limit), field.Value[..Limit]);
            Assert.EndsWith(Suffix, field.Value);
        }

        Assert.AreEqual("2", (string?)fixture.Report.Descendants(Ns + "Counters").Single().Attribute("total"));
        Assert.HasCount(1, fixture.Publications);
    }

    [TestMethod]
    public async Task Session_DroppedRecordsWarnWithoutClaimingCompleteResults()
    {
        using var fixture = new TrxBehaviorFixture();
        var original = new UnauthorizedAccessException("sidecar cannot be opened");
        fixture.FileSystem.Setup(value => value.NewFileStream(
            It.IsAny<string>(), FileMode.Create, FileAccess.Write, FileShare.Read)).Throws(original);
        TrxReportGenerator generator = fixture.CreateGenerator();
        await generator.OnTestSessionStartingAsync(new TestSessionContext(CancellationToken.None));
        await generator.ConsumeAsync(null!, Update("lost", PassedTestNodeStateProperty.CachedInstance), CancellationToken.None);

        await fixture.FinishAsync(generator, TestContext.CancellationToken);

        Assert.AreEqual("0", (string?)fixture.Report.Descendants(Ns + "Counters").Single().Attribute("total"));
        Assert.IsEmpty(fixture.Report.Descendants(Ns + "UnitTestResult"));

        var error = Assert.ContainsSingle(fixture.Logs.Where(value => value.Level == LogLevel.Error));
        Assert.AreSame(original, error.Error);
        Assert.Contains("1 record(s) were dropped", error.Message);
        Assert.Contains(
            value => value.Level == LogLevel.Warning &&
            value.Message == "TRX streaming store dropped 1 record(s) during the session; the generated TRX will be incomplete.", fixture.Logs);
        Assert.HasCount(1, fixture.Publications);
    }

    [TestMethod]
    public async Task Session_ReportFailureDisposesAndDeletesCompletedStore()
    {
        using var fixture = new TrxBehaviorFixture();
        TrxReportGenerator generator = fixture.CreateGenerator();
        await generator.OnTestSessionStartingAsync(new TestSessionContext(CancellationToken.None));
        await generator.ConsumeAsync(null!, Update("durable", PassedTestNodeStateProperty.CachedInstance), CancellationToken.None);
        var original = new UnauthorizedAccessException("report cannot be written");
        TrxBehaviorFixture.VirtualFile? sidecar = null;
        fixture.BeforeReportCreate = () =>
        {
            sidecar = fixture.Files.Single(value => value.Key.EndsWith(".bin", StringComparison.Ordinal)).Value;
            throw original;
        };

        UnauthorizedAccessException exception = await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(
            () => fixture.FinishAsync(generator, TestContext.CancellationToken));

        Assert.AreSame(original, exception);
        Assert.IsNotNull(sidecar);
        Assert.IsTrue(sidecar.IsDisposed);
        Assert.IsFalse(fixture.Files.ContainsKey(sidecar.Name));
        Assert.IsLessThan(fixture.Events.IndexOf("delete:" + sidecar.Name), fixture.Events.IndexOf("dispose:" + sidecar.Name));
        Assert.IsEmpty(fixture.Publications);
        Assert.IsFalse(fixture.Files.ContainsKey(fixture.ReportPath));
        generator.Dispose();
    }

    [TestMethod]
    public async Task Session_TimedOutWriterKeepsSidecarForRecovery()
    {
        using var fixture = new TrxBehaviorFixture();
        fixture.Scheduler.DelayOverride = (_, _) => Task.CompletedTask;
        TrxReportGenerator generator = fixture.CreateGenerator();
        await generator.OnTestSessionStartingAsync(new TestSessionContext(CancellationToken.None));
        await generator.ConsumeAsync(null!, Update("late-durable", PassedTestNodeStateProperty.CachedInstance), CancellationToken.None);
        fixture.BeforeReportCreate = () =>
        {
            // Completion has already sealed the queue. Explicitly run the captured writer here,
            // after ReadAll took its snapshot and before Dispose joins it. No clock or thread races.
            Task drain = fixture.Scheduler.DrainAsync();
            Assert.AreEqual(TaskStatus.RanToCompletion, drain.Status);
        };

        await generator.OnTestSessionFinishingAsync(new TestSessionContext(CancellationToken.None));

        KeyValuePair<string, TrxBehaviorFixture.VirtualFile> sidecar = Assert.ContainsSingle(fixture.Files.Where(value => value.Key.EndsWith(".bin", StringComparison.Ordinal)));
        Assert.IsTrue(sidecar.Value.IsDisposed);
        using var read = new MemoryStream(sidecar.Value.Bytes);
        Assert.AreEqual("late-durable", Assert.ContainsSingle(TrxTestResultSerializer.ReadAll(read)).Uid);
        Assert.AreEqual("0", (string?)fixture.Report.Descendants(Ns + "Counters").Single().Attribute("total"),
            "Results flushed after the read snapshot must not be retroactively invented in the report.");
        Assert.Contains(value => value.Level == LogLevel.Warning && value.Message.Contains("Approximately 1 record(s)", StringComparison.Ordinal), fixture.Logs);
        fixture.FileSystem.Verify(value => value.DeleteFile(sidecar.Key), Times.Never);
        Assert.HasCount(1, fixture.Publications);
    }

#if NETCOREAPP
    [TestMethod]
    public async Task Session_CancellationDuringSerializationDisposesStoreBeforePropagating()
    {
        using var fixture = new TrxBehaviorFixture();
        using var cancellation = new CancellationTokenSource();
        TrxReportGenerator generator = fixture.CreateGenerator();
        var session = new TestSessionContext(cancellation.Token);
        await generator.OnTestSessionStartingAsync(session);
        await generator.ConsumeAsync(null!, Update("durable", PassedTestNodeStateProperty.CachedInstance), cancellation.Token);
        fixture.BeforeReportCreate = cancellation.Cancel;

        OperationCanceledException exception = await Assert.ThrowsAsync<OperationCanceledException>(
            () => fixture.FinishAsync(generator, session));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        Assert.DoesNotContain(path => path.EndsWith(".bin", StringComparison.Ordinal), fixture.Files.Keys);
        string deleted = Assert.ContainsSingle(fixture.Events.Where(value => value.StartsWith("delete:", StringComparison.Ordinal)));
        Assert.IsLessThan(fixture.Events.IndexOf(deleted), fixture.Events.IndexOf("dispose:" + deleted["delete:".Length..]));
        Assert.IsEmpty(fixture.Publications);
    }
#endif

    [TestMethod]
    [DataRow(false, DisplayName = "TrxSession_MissingLifecycle")]
    [DataRow(true, DisplayName = "TrxSession_UnpreparedLifecycle")]
    public async Task Session_ControllerModeRequiresPreparedLifecycleBeforeAdvertisingFramework(bool hasLifecycle)
    {
        using var fixture = new TrxBehaviorFixture();
        fixture.Options[PlatformCommandLineProvider.TestHostControllerPIDOptionKey] = ["42"];
        using TrxTestApplicationLifecycleCallbacks? lifecycle = hasLifecycle
            ? new(fixture.CommandLine, fixture.Environment.Object) : null;
        TrxReportGenerator generator = fixture.CreateGenerator(lifecycle);

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => generator.OnTestSessionStartingAsync(new TestSessionContext(CancellationToken.None)));

        Assert.Contains("Unexpected state", exception.Message);
        fixture.Capability.Verify(value => value.Enable(), Times.Never);
        Assert.IsFalse(fixture.Scheduler.HasWriter);
        Assert.IsEmpty(fixture.Publications);
        fixture.FileSystem.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task Session_CanceledCompletionRetainsSidecarUntilDefensiveDisposal()
    {
        using var fixture = new TrxBehaviorFixture();
        using var cancellation = new CancellationTokenSource();
        TrxReportGenerator generator = fixture.CreateGenerator();
        var session = new TestSessionContext(cancellation.Token);
        await generator.OnTestSessionStartingAsync(session);
        await generator.ConsumeAsync(null!, Update("recover-after-cancellation", PassedTestNodeStateProperty.CachedInstance), cancellation.Token);
        fixture.Scheduler.DelayOverride = async (_, token) =>
        {
            Assert.AreEqual(cancellation.Token, token);
            cancellation.Cancel();
        };

        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => generator.OnTestSessionFinishingAsync(session));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        Assert.IsEmpty(fixture.Publications);
        Assert.IsFalse(fixture.Files.ContainsKey(fixture.ReportPath));
        await fixture.Scheduler.DrainAsync();
        KeyValuePair<string, TrxBehaviorFixture.VirtualFile> sidecar = Assert.ContainsSingle(fixture.Files);
        generator.Dispose();
        generator.Dispose();

        Assert.IsTrue(sidecar.Value.IsDisposed);
        Assert.IsTrue(fixture.Files.ContainsKey(sidecar.Key), "Defensive disposal releases ownership but retains the sidecar for recovery.");
        using var read = new MemoryStream(sidecar.Value.Bytes);
        Assert.AreEqual("recover-after-cancellation", Assert.ContainsSingle(TrxTestResultSerializer.ReadAll(read)).Uid);
        fixture.FileSystem.Verify(value => value.DeleteFile(It.IsAny<string>()), Times.Never);
        Assert.IsEmpty(fixture.Displays);
    }

    private static TestNodeUpdateMessage Update(string uid, TestNodeStateProperty? state, params IProperty[] additional)
    {
        PropertyBag properties = new(
            new TrxFullyQualifiedTypeNameProperty("Suite.Tests"),
            new TestMethodIdentifierProperty("assembly", "Suite", "Tests", "Method", 0, [], "System.Void"));
        if (state is not null)
        {
            properties.Add(state);
        }

        foreach (IProperty property in additional)
        {
            properties.Add(property);
        }

        return new(new SessionUid("fixture-session"), new TestNode { Uid = uid, DisplayName = "case " + uid, Properties = properties });
    }

    private static SessionFileArtifact Artifact(SessionUid session, string path)
        => new(session, new FileInfo(path), Path.GetFileName(path), "captured artifact");

    private static IDataProducer Producer(string uid, string displayName)
    {
        var producer = new Mock<IDataProducer>();
        producer.SetupGet(value => value.Uid).Returns(uid);
        producer.SetupGet(value => value.Version).Returns("1.0");
        producer.SetupGet(value => value.DisplayName).Returns(displayName);
        return producer.Object;
    }

    public TestContext TestContext { get; set; }
}
