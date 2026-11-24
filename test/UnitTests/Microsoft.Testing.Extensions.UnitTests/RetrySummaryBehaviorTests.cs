// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Policy;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.ServerMode;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class RetrySummaryBehaviorTests
{
    [TestMethod]
    [DataRow(0, false)]
    [DataRow(2, false)]
    [DataRow(2, true)]
    public async Task Summary_UnknownCountsNeverInventSuiteTotals(int failures, bool stoppedEarly)
    {
        using var fixture = new TrxBehaviorFixture();
        IOutputDeviceDataProducer producer = Mock.Of<IOutputDeviceDataProducer>();
        using var cancellation = new CancellationTokenSource();

        await RetrySummaryReporter.ReportSummaryAsync(
            producer, fixture.Output.Object, Summary(known: false, failed: failures, stoppedEarly: stoppedEarly), cancellation.Token);

        string header = stoppedEarly
            ? string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetrySummaryFailedStoppedEarly, 2)
            : string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetrySummaryFailed, 2, 4);
        string[] expected = failures > 0
            ? [header, string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetrySummaryFailedLine, 2), DurationLine()]
            : [header, DurationLine()];
        Assert.AreSequenceEqual(expected, fixture.Displays.Select(value => ((TextOutputDeviceData)value.Data).Text));
        Assert.AreEqual(ConsoleColor.DarkRed, Color(fixture.Displays[0].Data));
        if (failures > 0)
        {
            Assert.AreEqual(ConsoleColor.DarkRed, Color(fixture.Displays[1].Data));
        }

        Assert.IsTrue(fixture.Displays.All(value => ReferenceEquals(producer, value.Producer) && value.Token == cancellation.Token));
    }

    [TestMethod]
    [DataRow(0, 3, 1, 2, true, DisplayName = "RetrySummary_RescuedRunIsGreen")]
    [DataRow(2, 5, 1, 2, false, DisplayName = "RetrySummary_RemainingFailuresStayRed")]
    [DataRow(3, 2, 1, 0, false, DisplayName = "RetrySummary_InconsistentCountsNeverGoNegative")]
    public async Task Summary_KnownCountsClampSucceededAndUseFinalOutcome(int failures, int total, int skipped, int succeeded, bool success)
    {
        using var fixture = new TrxBehaviorFixture();
        RetryRunSummary summary = Summary(known: true, failed: failures, total: total, skipped: skipped, success: success);

        await RetrySummaryReporter.ReportSummaryAsync(Mock.Of<IOutputDeviceDataProducer>(), fixture.Output.Object, summary, CancellationToken.None);

        Assert.AreSequenceEqual(
            [
                string.Format(CultureInfo.CurrentCulture, success ? Policy.Resources.ExtensionResources.RetrySummaryPassed : Policy.Resources.ExtensionResources.RetrySummaryFailed, 2, 4),
                string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetrySummaryTotalLine, total),
                string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetrySummaryFailedLine, failures),
                string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetrySummarySucceededLine, succeeded),
                string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetrySummarySkippedLine, skipped),
                DurationLine(),
            ],
            fixture.Displays.Select(value => ((TextOutputDeviceData)value.Data).Text));
        Assert.AreEqual(success ? ConsoleColor.DarkGreen : ConsoleColor.DarkRed, Color(fixture.Displays[0].Data));
        Assert.AreEqual(failures > 0 ? ConsoleColor.DarkRed : null, Color(fixture.Displays[2].Data));
        Assert.AreEqual(failures == 0 && succeeded > 0 ? ConsoleColor.DarkGreen : null, Color(fixture.Displays[3].Data));
    }

    [TestMethod]
    public async Task Summary_FlakyNamesCannotForgeTerminalLines()
    {
        using var fixture = new TrxBehaviorFixture();
        string hostileName = "prefix\0\t\n\r\u001b[31m\u007f\u0085suffix";
        const string VisibleName = "prefix\u2400\u2409\u240a\u240d\u241b[31m\u247f\u2485suffix";

        await RetrySummaryReporter.ReportSummaryAsync(
            Mock.Of<IOutputDeviceDataProducer>(), fixture.Output.Object,
            Summary(known: false, success: true, flaky: [hostileName, "ordinary \U0001f600"], showFlaky: true, retried: 2, executions: 5),
            CancellationToken.None);

        Assert.AreSequenceEqual(
            [
                string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetrySummaryPassed, 2, 4),
                string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetrySummaryFlakyLine, 2),
                string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetrySummaryRetriedLine, 2, 5),
                DurationLine(),
                string.Empty,
                Policy.Resources.ExtensionResources.RetrySummaryFlakyTestsHeader,
                string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetrySummaryFlakyTestLine, VisibleName),
                string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetrySummaryFlakyTestLine, "ordinary \U0001f600"),
            ],
            fixture.Displays.Select(value => ((TextOutputDeviceData)value.Data).Text));
        Assert.AreEqual(ConsoleColor.DarkYellow, Color(fixture.Displays[1].Data));
        Assert.AreEqual(ConsoleColor.DarkGray, Color(fixture.Displays[2].Data));
        Assert.AreEqual(ConsoleColor.DarkYellow, Color(fixture.Displays[6].Data));
        Assert.AreEqual(ConsoleColor.DarkYellow, Color(fixture.Displays[7].Data));
        Assert.DoesNotContain(text => text.Any(char.IsControl), fixture.Displays.Select(value => ((TextOutputDeviceData)value.Data).Text));
    }

    [TestMethod]
    public async Task Summary_HiddenFlakyTestsAreNotDisclosed()
    {
        using var fixture = new TrxBehaviorFixture();

        await RetrySummaryReporter.ReportSummaryAsync(
            Mock.Of<IOutputDeviceDataProducer>(), fixture.Output.Object,
            Summary(known: false, success: true, flaky: ["sensitive-name"], showFlaky: false),
            CancellationToken.None);

        Assert.AreSequenceEqual(
            [string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetrySummaryPassed, 2, 4), DurationLine()],
            fixture.Displays.Select(value => ((TextOutputDeviceData)value.Data).Text));
    }

    [TestMethod]
    public async Task Artifacts_EmptyTreeDoesNotPublish()
    {
        using var fixture = new TrxBehaviorFixture();
        fixture.FileSystem.Setup(value => value.GetFiles("attempt", "*.*", SearchOption.AllDirectories)).Returns([]);

        await RetrySummaryReporter.MoveArtifactsAsync(
            Mock.Of<IOutputDeviceDataProducer>(), fixture.Output.Object, fixture.FileSystem.Object, fixture.Logger.Object,
            "attempt", "published", CancellationToken.None);

        fixture.FileSystem.Verify(value => value.GetFiles("attempt", "*.*", SearchOption.AllDirectories), Times.Once);
        fixture.FileSystem.VerifyNoOtherCalls();
        Assert.IsEmpty(fixture.Displays);
        Assert.IsEmpty(fixture.Logs);
    }

    [TestMethod]
    public async Task Artifacts_FinalTreeIsCopiedBeforePublicationWithoutRemovingAttempts()
    {
        using var fixture = new TrxBehaviorFixture();
        string attempt = Path.Combine(fixture.Root, "Retries", "run", "2");
        string nested = Path.Combine(attempt, "nested", "report.trx");
        string attachment = Path.Combine(attempt, "artifacts", "trace.log");
        string merged = Path.Combine(fixture.Root, "merge", "logical.trx");
        fixture.FileSystem.Setup(value => value.GetFiles(attempt, "*.*", SearchOption.AllDirectories)).Returns([nested, attachment]);
        List<string> operations = [];
        fixture.FileSystem.Setup(value => value.CreateDirectory(It.IsAny<string>()))
            .Returns<string>(path =>
            {
                operations.Add("mkdir:" + path);
                return path;
            });
        fixture.FileSystem.Setup(value => value.CopyFile(It.IsAny<string>(), It.IsAny<string>(), true))
            .Callback<string, string, bool>((source, destination, _) => operations.Add("copy:" + source + "=>" + destination));
        fixture.Output.Setup(value => value.DisplayAsync(
            It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>((_, _, _) => operations.Add("published"))
            .Returns(Task.CompletedTask);
        var replacements = new Dictionary<string, string> { [nested] = merged };

        await RetrySummaryReporter.MoveArtifactsAsync(
            Mock.Of<IOutputDeviceDataProducer>(), fixture.Output.Object, fixture.FileSystem.Object, fixture.Logger.Object,
            attempt, fixture.Root, replacements, CancellationToken.None);

        Assert.AreSequenceEqual(
            [
                "mkdir:" + Path.Combine(fixture.Root, "nested"),
                "copy:" + merged + "=>" + Path.Combine(fixture.Root, "nested", "report.trx"),
                "mkdir:" + Path.Combine(fixture.Root, "artifacts"),
                "copy:" + attachment + "=>" + Path.Combine(fixture.Root, "artifacts", "trace.log"),
                "published",
            ], operations);
        Assert.AreSequenceEqual(
            [
                $"Copying file '{merged}' to '{Path.Combine(fixture.Root, "nested", "report.trx")}'",
                $"Copying file '{attachment}' to '{Path.Combine(fixture.Root, "artifacts", "trace.log")}'",
            ], fixture.Logs.Select(value => value.Message));
        fixture.FileSystem.Verify(value => value.DeleteFile(It.IsAny<string>()), Times.Never);
        fixture.FileSystem.Verify(value => value.MoveFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        fixture.Output.Verify(
            value => value.DisplayAsync(
            It.IsAny<IOutputDeviceDataProducer>(),
            It.Is<FormattedTextOutputDeviceData>(data => data.Text == string.Format(
                CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetryArtifactsPublished, 2, fixture.Root)),
            CancellationToken.None), Times.Once);
    }

    [TestMethod]
    public async Task Artifacts_TrxNamedAttachmentIsPreservedWithoutReportRegistration()
    {
        using var fixture = new TrxBehaviorFixture();
        string attempt = Path.Combine(fixture.Root, "Retries", "run", "2");
        string attachment = Path.Combine(attempt, "diagnostics.trx");
        byte[] content = Encoding.UTF8.GetBytes("retry diagnostics");
        fixture.ExistingFiles[attachment] = content;
        fixture.FileSystem.Setup(value => value.GetFiles(attempt, "*.*", SearchOption.AllDirectories)).Returns([attachment]);
        fixture.FileSystem.Setup(value => value.CopyFile(It.IsAny<string>(), It.IsAny<string>(), true))
            .Callback<string, string, bool>((source, destination, _) => fixture.ExistingFiles[destination] = fixture.GetBytes(source));

        await RetrySummaryReporter.MoveArtifactsAsync(
            Mock.Of<IOutputDeviceDataProducer>(), fixture.Output.Object, fixture.FileSystem.Object, fixture.Logger.Object,
            attempt, fixture.Root, CancellationToken.None);

        Assert.IsTrue(fixture.ExistingFiles.ContainsKey(attachment), "A TRX-named attachment must remain at its recorded path.");
        Assert.AreSequenceEqual(content, fixture.GetBytes(attachment));
        Assert.AreSequenceEqual(content, fixture.GetBytes(Path.Combine(fixture.Root, "diagnostics.trx")));
        fixture.FileSystem.Verify(value => value.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    [DataRow("report.trx", true, DisplayName = "RetryArtifacts_FinalTrx")]
    [DataRow("report.TRX", true, DisplayName = "RetryArtifacts_FinalTrxUppercase")]
    [DataRow("report.xml", false, DisplayName = "RetryArtifacts_UnprocessedXml")]
    [DataRow("report.ctrf.json", false, DisplayName = "RetryArtifacts_UnprocessedCtrf")]
    [DataRow("report.html", false, DisplayName = "RetryArtifacts_UnprocessedHtml")]
    [DataRow("trace.log", false, DisplayName = "RetryArtifacts_AttachmentLog")]
    public async Task Artifacts_OnlyUnprocessedFinalTrxIsRemovedAfterAllCopies(string fileName, bool removeSource)
    {
        using var fixture = new TrxBehaviorFixture();
        string attempt = Path.Combine(fixture.Root, "Retries", "run", "2");
        string report = Path.Combine(attempt, "nested", fileName);
        string attachment = Path.Combine(attempt, "artifacts", "attachment.txt");
        byte[] reportContents = Encoding.UTF8.GetBytes("report");
        byte[] attachmentContents = Encoding.UTF8.GetBytes("attachment");
        fixture.ExistingFiles[report] = reportContents;
        fixture.ExistingFiles[attachment] = attachmentContents;
        fixture.FileSystem.Setup(value => value.GetFiles(attempt, "*.*", SearchOption.AllDirectories)).Returns([report, attachment]);
        fixture.FileSystem.Setup(value => value.CopyFile(It.IsAny<string>(), It.IsAny<string>(), true))
            .Callback<string, string, bool>((source, destination, _) =>
            {
                fixture.ExistingFiles[destination] = fixture.GetBytes(source);
                fixture.Events.Add("copy:" + source);
            });

        await RetrySummaryReporter.MoveArtifactsAsync(
            Mock.Of<IOutputDeviceDataProducer>(), fixture.Output.Object, fixture.FileSystem.Object, fixture.Logger.Object,
            attempt, fixture.Root, new Dictionary<string, string>(), [report], onTrxMoved: null, CancellationToken.None);

        Assert.AreEqual(!removeSource, fixture.ExistingFiles.ContainsKey(report));
        Assert.AreSequenceEqual(reportContents, fixture.GetBytes(Path.Combine(fixture.Root, "nested", fileName)));
        Assert.AreSequenceEqual(attachmentContents, fixture.GetBytes(attachment));
        Assert.AreSequenceEqual(attachmentContents, fixture.GetBytes(Path.Combine(fixture.Root, "artifacts", "attachment.txt")));
        List<string> expectedEvents = ["copy:" + report, "copy:" + attachment];
        if (removeSource)
        {
            expectedEvents.Add("delete:" + report);
        }

        expectedEvents.Add("display");
        Assert.AreSequenceEqual(expectedEvents, fixture.Events);
    }

    [TestMethod]
    public async Task Artifacts_LaterCopyFailurePreservesFinalTrx()
    {
        using var fixture = new TrxBehaviorFixture();
        string attempt = Path.Combine(fixture.Root, "Retries", "run", "2");
        string report = Path.Combine(attempt, "report.trx");
        string attachment = Path.Combine(attempt, "attachment.txt");
        fixture.FileSystem.Setup(value => value.GetFiles(attempt, "*.*", SearchOption.AllDirectories)).Returns([report, attachment]);
        var original = new IOException("attachment copy denied");
        fixture.FileSystem.Setup(value => value.CopyFile(attachment, Path.Combine(fixture.Root, "attachment.txt"), true)).Throws(original);

        IOException exception = await Assert.ThrowsExactlyAsync<IOException>(() => RetrySummaryReporter.MoveArtifactsAsync(
            Mock.Of<IOutputDeviceDataProducer>(), fixture.Output.Object, fixture.FileSystem.Object, fixture.Logger.Object,
            attempt, fixture.Root, new Dictionary<string, string>(), [report], onTrxMoved: null, CancellationToken.None));

        Assert.AreSame(original, exception);
        fixture.FileSystem.Verify(value => value.CopyFile(report, Path.Combine(fixture.Root, "report.trx"), true), Times.Once);
        fixture.FileSystem.Verify(value => value.DeleteFile(It.IsAny<string>()), Times.Never);
        Assert.IsEmpty(fixture.Displays);
    }

    [TestMethod]
    public async Task Artifacts_DeleteFailureStopsBeforePublication()
    {
        using var fixture = new TrxBehaviorFixture();
        string attempt = Path.Combine(fixture.Root, "Retries", "run", "2");
        string report = Path.Combine(attempt, "report.trx");
        fixture.FileSystem.Setup(value => value.GetFiles(attempt, "*.*", SearchOption.AllDirectories)).Returns([report]);
        var original = new IOException("delete denied");
        fixture.FileSystem.Setup(value => value.DeleteFile(report)).Throws(original);

        IOException exception = await Assert.ThrowsExactlyAsync<IOException>(() => RetrySummaryReporter.MoveArtifactsAsync(
            Mock.Of<IOutputDeviceDataProducer>(), fixture.Output.Object, fixture.FileSystem.Object, fixture.Logger.Object,
            attempt, fixture.Root, new Dictionary<string, string>(), [report], onTrxMoved: null, CancellationToken.None));

        Assert.AreSame(original, exception);
        fixture.FileSystem.Verify(value => value.CopyFile(report, Path.Combine(fixture.Root, "report.trx"), true), Times.Once);
        Assert.IsEmpty(fixture.Displays);
    }

    [TestMethod]
    public async Task Artifacts_MovedTrxIsReportedAfterDeletionAndBeforePublication()
    {
        using var fixture = new TrxBehaviorFixture();
        string attempt = Path.Combine(fixture.Root, "Retries", "run", "2");
        string source = Path.Combine(attempt, "report.trx");
        string destination = Path.Combine(fixture.Root, "report.trx");
        fixture.ExistingFiles[source] = Encoding.UTF8.GetBytes("report");
        fixture.FileSystem.Setup(value => value.GetFiles(attempt, "*.*", SearchOption.AllDirectories)).Returns([source]);
        fixture.FileSystem.Setup(value => value.CopyFile(source, destination, true))
            .Callback(() => fixture.ExistingFiles[destination] = fixture.GetBytes(source));
        List<(string Source, string Destination)> moved = [];

        await RetrySummaryReporter.MoveArtifactsAsync(
            Mock.Of<IOutputDeviceDataProducer>(), fixture.Output.Object, fixture.FileSystem.Object, fixture.Logger.Object,
            attempt, fixture.Root, new Dictionary<string, string>(), [source],
            (original, published) =>
            {
                Assert.IsFalse(fixture.ExistingFiles.ContainsKey(original));
                Assert.IsTrue(fixture.ExistingFiles.ContainsKey(published));
                Assert.IsEmpty(fixture.Displays);
                moved.Add((original, published));
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.AreEqual((source, destination), Assert.ContainsSingle(moved));
        Assert.ContainsSingle(fixture.Displays);
    }

    [TestMethod]
    [DataRow(false, false, DisplayName = "RetryArtifacts_CopyFailureReplaysOriginalPath")]
    [DataRow(true, false, DisplayName = "RetryArtifacts_PartialMoveReplaysBothPaths")]
    [DataRow(false, true, DisplayName = "RetryArtifacts_ReplayFailurePreservesCopyException")]
    public async Task Artifacts_PublicationFailureReplaysPendingNotificationsAndPreservesOriginalException(bool partialMove, bool replayFails)
    {
        using var fixture = new TrxBehaviorFixture();
        string retryRoot = Path.Combine(fixture.Root, "Retries", "run");
        string attempt = Path.Combine(retryRoot, "2");
        Directory.CreateDirectory(attempt);
        try
        {
            string first = Path.Combine(attempt, "first.trx");
            string second = Path.Combine(attempt, "second.trx");
            string attachment = Path.Combine(attempt, "attachment.txt");
            File.WriteAllText(first, "first");
            File.WriteAllText(second, "second");
            File.WriteAllText(attachment, "attachment");
            fixture.FileSystem.Setup(value => value.GetFiles(attempt, "*.*", SearchOption.AllDirectories))
                .Returns(partialMove ? [first, second] : [first, attachment]);
            var original = new IOException("artifact publication failed");
            fixture.FileSystem.Setup(value => value.CopyFile(It.IsAny<string>(), It.IsAny<string>(), true))
                .Callback<string, string, bool>((source, destination, _) =>
                {
                    if (!partialMove && source == attachment)
                    {
                        throw original;
                    }

                    File.Copy(source, destination, overwrite: true);
                });
            fixture.FileSystem.Setup(value => value.DeleteFile(It.IsAny<string>()))
                .Callback<string>(path =>
                {
                    if (partialMove && path == second)
                    {
                        throw original;
                    }

                    File.Delete(path);
                });
            var protocol = new Mock<IPushOnlyProtocol>();
            protocol.SetupGet(value => value.IsServerMode).Returns(true);
            List<FileArtifactMessages> replayed = [];
            protocol.Setup(value => value.SendMessageAsync(It.IsAny<FileArtifactMessages>()))
                .Callback(new InvocationAction(call => replayed.Add((FileArtifactMessages)call.Arguments[0])))
                .Returns(() => replayFails ? Task.FromException(new IOException("replay failed")) : Task.CompletedTask);
            fixture.Services.AddService(protocol.Object);
            string manifests = RetryArtifactProcessor.GetDeferredTrxArtifactDirectory(retryRoot);
            List<RetryAttemptArtifact> artifacts = [];
            foreach (string path in partialMove ? new[] { first, second } : [first])
            {
                artifacts.Add(new RetryAttemptArtifact(path, "microsoft.testing.trx", 2, destinationPath: null));
                Assert.IsTrue(DotnetTestDataConsumer.TryDeferTrxArtifacts(
                    new FileArtifactMessages("execution", "instance", [new FileArtifactMessage(path, Path.GetFileName(path), null, null, null, "session", "microsoft.testing.trx")]),
                    attempt,
                    manifests));
            }

            var orchestrator = new RetryOrchestrator(fixture.Services);
            System.Reflection.MethodInfo processArtifacts = typeof(RetryOrchestrator).GetMethod(
                "ProcessArtifactsAsync",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

            IOException exception = await Assert.ThrowsExactlyAsync<IOException>(() => (Task)processArtifacts.Invoke(
                orchestrator,
                [
                    fixture.Output.Object, fixture.FileSystem.Object, fixture.Logger.Object, artifacts,
                    2, true, 2, 0, 1, false, TimeSpan.Zero, 2, attempt, fixture.Root, CancellationToken.None,
                ])!);

            Assert.AreSame(original, exception);
            string[] expected = partialMove ? [Path.Combine(fixture.Root, "first.trx"), second] : [first];
            Assert.AreSequenceEqual(
                expected.OrderBy(path => path, StringComparer.Ordinal),
                replayed.SelectMany(message => message.FileArtifacts).Select(artifact => artifact.FullPath!).OrderBy(path => path, StringComparer.Ordinal));
            Assert.ContainsOnly(File.Exists, expected);
            Assert.IsEmpty(fixture.Displays);
            if (replayFails)
            {
                Assert.Contains(
                    log => log.Level == Microsoft.Testing.Platform.Logging.LogLevel.Warning && log.Message.Contains("Failed to replay retry artifacts", StringComparison.Ordinal),
                    fixture.Logs);
            }
        }
        finally
        {
            Directory.Delete(fixture.Root, recursive: true);
        }
    }

    [TestMethod]
    public async Task Artifacts_CopyFailureStopsBeforePublication()
    {
        using var fixture = new TrxBehaviorFixture();
        string attempt = Path.Combine(fixture.Root, "Retries", "run", "2");
        string file = Path.Combine(attempt, "report.trx");
        fixture.FileSystem.Setup(value => value.GetFiles(attempt, "*.*", SearchOption.AllDirectories)).Returns([file]);
        var original = new IOException("copy denied");
        fixture.FileSystem.Setup(value => value.CopyFile(file, Path.Combine(fixture.Root, "report.trx"), true)).Throws(original);

        IOException exception = await Assert.ThrowsExactlyAsync<IOException>(() => RetrySummaryReporter.MoveArtifactsAsync(
            Mock.Of<IOutputDeviceDataProducer>(), fixture.Output.Object, fixture.FileSystem.Object, fixture.Logger.Object,
            attempt, fixture.Root, CancellationToken.None));

        Assert.AreSame(original, exception);
        Assert.IsEmpty(fixture.Displays);
        Assert.AreEqual(LogLevel.Debug, Assert.ContainsSingle(fixture.Logs).Level);
        fixture.FileSystem.Verify(value => value.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    private static ConsoleColor? Color(IOutputDeviceData data)
        => data is FormattedTextOutputDeviceData { ForegroundColor: SystemConsoleColor color } ? color.ConsoleColor : null;

    private static string DurationLine()
        => string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetrySummaryDurationLine, "1s 234ms");

    private static RetryRunSummary Summary(
        bool known, int failed = 0, int total = 0, int skipped = 0, bool success = false,
        bool stoppedEarly = false, IReadOnlyList<string>? flaky = null, bool showFlaky = false,
        int retried = 0, int executions = 0)
        => new()
        {
            ExitCodes = [2, success ? 0 : 2],
            AttemptCount = 2,
            UserMaxRetryCount = 3,
            SuiteCountsKnown = known,
            SuiteTotalTests = total,
            SuiteSkippedTests = skipped,
            FinalFailedTests = failed,
            RetriedTests = retried,
            RetriedExecutions = executions,
            FlakyTests = flaky ?? [],
            ShowFlakyTests = showFlaky,
            StoppedEarly = stoppedEarly,
            Elapsed = TimeSpan.FromMilliseconds(1234),
        };
}
