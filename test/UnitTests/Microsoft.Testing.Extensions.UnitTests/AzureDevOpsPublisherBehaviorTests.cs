// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class AzureDevOpsPublisherBehaviorTests
{
    [TestMethod]
    [DataRow(false, DisplayName = "Publisher_MissingConfiguration")]
    [DataRow(true, DisplayName = "Publisher_MissingConfigurationAndBrokenDiagnostics")]
    public async Task Start_ConfigurationAndDiagnosticFailuresLeavePublisherInert(bool diagnosticsFail)
    {
        using var fixture = new CiReporterFixture();
        fixture.EnableAzureDevOps();
        fixture.Variables["SYSTEM_ACCESSTOKEN"] = null;
        if (diagnosticsFail)
        {
            fixture.LogError = new IOException("logger unavailable");
            fixture.DisplayError = _ => new IOException("output unavailable");
        }

        using AzureDevOpsTestResultsPublisher publisher = fixture.Publisher();
        Assert.IsTrue(await publisher.IsEnabledAsync());
        await publisher.OnTestSessionStartingAsync(new CiReporterFixture.Session());
        await publisher.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Ignored", new FailedTestNodeStateProperty()), CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new CiReporterFixture.Session());
        publisher.Dispose();

        Assert.IsNull(publisher.RunId);
        fixture.Client.VerifyNoOtherCalls();
        Assert.IsEmpty(fixture.Opens);
        (IOutputDeviceDataProducer owner, IOutputDeviceData data, CancellationToken token) = Assert.ContainsSingle(fixture.DisplayAttempts);
        Assert.AreSame(publisher, owner);
        Assert.AreEqual("AzureDevOpsTestResultsPublisher", owner.Uid);
        Assert.AreEqual(AzureDevOpsResources.DisplayName, owner.DisplayName);
        Assert.AreEqual(AzureDevOpsResources.Description, owner.Description);
        Assert.IsNotEmpty(owner.Version);
        Assert.Contains("SYSTEM_ACCESSTOKEN", Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(data).Message);
        if (diagnosticsFail)
        {
            Assert.IsEmpty(fixture.Displays);
            Assert.Contains("output unavailable", fixture.LogText);
        }
    }

    [TestMethod]
    [DataRow(0, DisplayName = "Publisher_CreateIoFailure")]
    [DataRow(1, DisplayName = "Publisher_InternalCreateCancellation")]
    [DataRow(2, DisplayName = "Publisher_CallerCanceledStart")]

    public async Task Start_FailedAcquisitionResetsStateButCallerCancellationPropagates(int failure)
    {
        using var fixture = new CiReporterFixture();
        fixture.EnableAzureDevOps();
        using var cancellation = new CancellationTokenSource();
        if (failure == 2)
        {
            cancellation.Cancel();
        }

        fixture.Client.Setup(value => value.CreateTestRunAsync(It.IsAny<AzureDevOpsPublishConfiguration>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure == 1 ? new TaskCanceledException("internal timeout") : new IOException("create unavailable"));
        using AzureDevOpsTestResultsPublisher publisher = fixture.Publisher();

        if (failure == 2)
        {
            OperationCanceledException error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
                publisher.OnTestSessionStartingAsync(new CiReporterFixture.Session(cancellation.Token)));
            Assert.AreEqual(cancellation.Token, error.CancellationToken);
            Assert.IsEmpty(fixture.Displays);
        }
        else
        {
            await publisher.OnTestSessionStartingAsync(new CiReporterFixture.Session());
            Assert.Contains(
                AzureDevOpsResources.AzureDevOpsLivePublishingCreateRunFailed,
                Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(Assert.ContainsSingle(fixture.Displays)).Message);
        }

        await publisher.OnTestSessionFinishingAsync(new CiReporterFixture.Session());
        Assert.IsNull(publisher.RunId);
        fixture.Client.Verify(value => value.UpdateTestRunStateAsync(It.IsAny<AzureDevOpsPublishConfiguration>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    [DataRow(false, false, "Completed", DisplayName = "Publisher_RenewalFailureStillCompletes")]
    [DataRow(true, false, "Aborted", DisplayName = "Publisher_CanceledFinishStillCloses")]
    [DataRow(false, true, "Aborted", DisplayName = "Publisher_AdapterFailureAborts")]
    public async Task Finish_RenewalFailureReportsQueuedResultsAndStillClosesRun(bool canceled, bool adapterFailure, string state)
    {
        using var fixture = new CiReporterFixture();
        fixture.EnableAzureDevOps();
        fixture.ExitCode.SetupGet(value => value.HasTestAdapterTestSessionFailure).Returns(adapterFailure);
        using AzureDevOpsTestResultsPublisher publisher = fixture.Publisher();
        await publisher.OnTestSessionStartingAsync(new CiReporterFixture.Session());
        fixture.BeforeOpen = (path, mode, _, _) =>
        {
            if (path.Contains(".participant.") && mode == FileMode.Create)
            {
                throw new IOException("lease renewal denied");
            }
        };
        await publisher.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Queued", new PassedTestNodeStateProperty()), CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        if (canceled)
        {
            cancellation.Cancel();
        }

        await publisher.OnTestSessionFinishingAsync(new CiReporterFixture.Session(cancellation.Token));

        fixture.Client.Verify(value => value.PublishTestResultsWithSubResultsAsync(It.IsAny<AzureDevOpsPublishConfiguration>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<AzureDevOpsTestCaseResult>>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Client.Verify(value => value.UpdateTestRunStateAsync(It.IsAny<AzureDevOpsPublishConfiguration>(), 77, state, It.Is<CancellationToken>(token => token.CanBeCanceled && !token.IsCancellationRequested)), Times.Once);
        WarningMessageOutputDeviceData dropped = Assert.ContainsSingle(fixture.Displays.OfType<WarningMessageOutputDeviceData>());
        Assert.AreEqual(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingResultsDropped, 1), dropped.Message);
        Assert.AreEqual(CancellationToken.None, fixture.DisplayAttempts.Single(attempt => ReferenceEquals(attempt.Data, dropped)).Token);
        Assert.Contains("lease renewal denied", fixture.LogText);
    }

    [TestMethod]
    public async Task Finish_DeferredAttachmentCancellationIsCountedWithoutRepublishingParent()
    {
        using var fixture = new CiReporterFixture();
        fixture.EnableAzureDevOps();
        Inherit(fixture, withMap: true);
        using var foreignCancellation = new CancellationTokenSource();
        fixture.Client.Setup(value => value.UpdateTestResultsWithSubResultsAsync(It.IsAny<AzureDevOpsPublishConfiguration>(), 77, It.IsAny<IReadOnlyList<AzureDevOpsTestCaseResult>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(foreignCancellation.Token));
        using AzureDevOpsTestResultsPublisher publisher = fixture.Publisher(CiReporterFixture.PublisherOptions(1));
        await publisher.OnTestSessionStartingAsync(new CiReporterFixture.Session());
        TestNodeUpdateMessage update = CiReporterFixture.Update("Tests.Accepted", new FailedTestNodeStateProperty("failed"),
            new StandardOutputProperty("first output"), new StandardErrorProperty("second output"));

        OperationCanceledException seedError = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            publisher.ConsumeAsync(null!, update, CancellationToken.None));
        Assert.AreEqual(foreignCancellation.Token, seedError.CancellationToken);
        List<string> uploads = [];
        fixture.Client.Setup(value => value.UploadTestResultAttachmentAsync(It.IsAny<AzureDevOpsPublishConfiguration>(), 77, 101, null, It.IsAny<AzureDevOpsTestResultAttachment>(), It.IsAny<CancellationToken>()))
            .Returns<AzureDevOpsPublishConfiguration, int, int, int?, AzureDevOpsTestResultAttachment, CancellationToken>((_, _, _, _, attachment, _) =>
            {
                uploads.Add(attachment.FileName);
                return attachment.FileName == "stdout.log"
                    ? Task.FromException(new OperationCanceledException("deferred upload canceled"))
                    : Task.CompletedTask;
            });

        await publisher.OnTestSessionFinishingAsync(new CiReporterFixture.Session());

        Assert.AreSequenceEqual(["stdout.log", "stderr.log"], uploads);
        fixture.Client.Verify(value => value.PublishTestResultsWithSubResultsAsync(It.IsAny<AzureDevOpsPublishConfiguration>(), 77, It.IsAny<IReadOnlyList<AzureDevOpsTestCaseResult>>(), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Client.Verify(value => value.UpdateTestResultsWithSubResultsAsync(It.IsAny<AzureDevOpsPublishConfiguration>(), 77, It.IsAny<IReadOnlyList<AzureDevOpsTestCaseResult>>(), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Client.Verify(value => value.UpdateTestRunStateAsync(It.IsAny<AzureDevOpsPublishConfiguration>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.AreEqual(
            string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingAttachmentsDropped, 1),
            Assert.ContainsSingle(fixture.Displays.OfType<WarningMessageOutputDeviceData>()).Message);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "RunAttachment_IoFailureContinuesDrain")]
    [DataRow(true, DisplayName = "RunAttachment_CancellationStopsDrain")]
    public async Task Finish_RunAttachmentFailureDoesNotHideFollowingArtifact(bool canceled)
    {
        using var fixture = new CiReporterFixture();
        fixture.EnableAzureDevOps();
        Inherit(fixture);
        using AzureDevOpsTestResultsPublisher publisher = fixture.Publisher();
        await publisher.OnTestSessionStartingAsync(new CiReporterFixture.Session());
        foreach (string name in new[] { "first.COVERAGE", "ignored.log", "second.cobertura.xml" })
        {
            await publisher.ConsumeAsync(null!, new SessionFileArtifact(new SessionUid("ci-session"), new FileInfo(Path.Combine(fixture.Root, name)), name, "owned coverage"), CancellationToken.None);
        }

        await publisher.ConsumeAsync(null!, new SessionFileArtifact(
            new SessionUid("ci-session"),
            new FileInfo(Path.GetPathRoot(fixture.Root)!), "root metadata only"), CancellationToken.None);
        List<string> uploads = [];
        fixture.Client.Setup(value => value.UploadTestRunAttachmentAsync(It.IsAny<AzureDevOpsPublishConfiguration>(), 77, It.IsAny<AzureDevOpsTestResultAttachment>(), It.IsAny<CancellationToken>()))
            .Returns<AzureDevOpsPublishConfiguration, int, AzureDevOpsTestResultAttachment, CancellationToken>((_, _, attachment, _) =>
            {
                uploads.Add(attachment.FileName);
                Assert.AreEqual("CodeCoverage", attachment.AttachmentType);
                Assert.AreEqual("owned coverage", attachment.Comment);
                return attachment.FileName == "first.COVERAGE"
                    ? Task.FromException(canceled ? new OperationCanceledException("upload canceled") : new IOException("upload denied"))
                    : Task.CompletedTask;
            });

        await publisher.OnTestSessionFinishingAsync(new CiReporterFixture.Session());

        Assert.AreSequenceEqual(canceled ? ["first.COVERAGE"] : ["first.COVERAGE", "second.cobertura.xml"], uploads);
        if (canceled)
        {
            Assert.IsEmpty(fixture.Displays);
        }
        else
        {
            Assert.HasCount(2, fixture.Displays.OfType<WarningMessageOutputDeviceData>());
            Assert.Contains("upload denied", fixture.Displays.OfType<WarningMessageOutputDeviceData>().First().Message);
            Assert.AreEqual(
                string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingAttachmentsDropped, 1),
                fixture.Displays.OfType<WarningMessageOutputDeviceData>().Last().Message);
        }
    }

    [TestMethod]
    public async Task Consume_AttachmentLeaseFailureCountsWholeDeferredSet()
    {
        using var fixture = new CiReporterFixture();
        fixture.EnableAzureDevOps();
        using AzureDevOpsTestResultsPublisher publisher = fixture.Publisher(CiReporterFixture.PublisherOptions(1));
        await publisher.OnTestSessionStartingAsync(new CiReporterFixture.Session());
        fixture.Client.Setup(value => value.PublishTestResultsWithSubResultsAsync(It.IsAny<AzureDevOpsPublishConfiguration>(), 77, It.IsAny<IReadOnlyList<AzureDevOpsTestCaseResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AzureDevOpsPublishedTestResult(91, new Dictionary<int, int>())])
            .Callback(() => fixture.BeforeOpen = (path, mode, _, _) =>
            {
                if (path.Contains(".participant.") && mode == FileMode.Create)
                {
                    throw new IOException("attachment lease denied");
                }
            });

        await publisher.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Attached", new FailedTestNodeStateProperty("failed"),
            new StandardOutputProperty("out"), new StandardErrorProperty("err")), CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new CiReporterFixture.Session());

        fixture.Client.Verify(value => value.UploadTestResultAttachmentAsync(It.IsAny<AzureDevOpsPublishConfiguration>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<AzureDevOpsTestResultAttachment>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.AreEqual(
            string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingAttachmentsDropped, 2),
            Assert.ContainsSingle(fixture.Displays.OfType<WarningMessageOutputDeviceData>()).Message);
        Assert.Contains("attachment lease denied", fixture.LogText);
        fixture.Client.Verify(value => value.UpdateTestRunStateAsync(It.IsAny<AzureDevOpsPublishConfiguration>(), 77, "Completed", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Flush_FailedBatchPreservesOrderUntilForcedRetry()
    {
        using var fixture = new CiReporterFixture();
        fixture.EnableAzureDevOps();
        Inherit(fixture);
        List<string[]> batches = [];
        fixture.Client.Setup(value => value.PublishTestResultsWithSubResultsAsync(It.IsAny<AzureDevOpsPublishConfiguration>(), 77, It.IsAny<IReadOnlyList<AzureDevOpsTestCaseResult>>(), It.IsAny<CancellationToken>()))
            .Returns<AzureDevOpsPublishConfiguration, int, IReadOnlyList<AzureDevOpsTestCaseResult>, CancellationToken>((_, _, results, _) =>
            {
                batches.Add([.. results.Select(result => result.AutomatedTestName)]);
                return batches.Count == 1
                    ? Task.FromException<IReadOnlyList<AzureDevOpsPublishedTestResult>?>(new IOException("first batch rejected"))
                    : Task.FromResult<IReadOnlyList<AzureDevOpsPublishedTestResult>?>([.. results.Select((_, index) => new AzureDevOpsPublishedTestResult(200 + index, new Dictionary<int, int>()))]);
            });
        using AzureDevOpsTestResultsPublisher publisher = fixture.Publisher(CiReporterFixture.PublisherOptions(2));
        await publisher.OnTestSessionStartingAsync(new CiReporterFixture.Session());
        foreach (string name in new[] { "Tests.A", "Tests.B", "Tests.C" })
        {
            await publisher.ConsumeAsync(null!, CiReporterFixture.Update(name, new PassedTestNodeStateProperty()), CancellationToken.None);
        }

        Assert.HasCount(1, batches);
        await publisher.OnTestSessionFinishingAsync(new CiReporterFixture.Session());

        Assert.HasCount(3, batches);
        Assert.AreSequenceEqual(["Tests.A", "Tests.B"], batches[0]);
        Assert.AreSequenceEqual(["Tests.A", "Tests.B"], batches[1]);
        Assert.AreSequenceEqual(["Tests.C"], batches[2]);
        Assert.IsEmpty(fixture.Displays);
        Assert.Contains("first batch rejected", fixture.LogText);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "PersistedMap_NonforcedFallbackIsRetriedAtFinish")]
    [DataRow(true, DisplayName = "PersistedMap_ForcedFallbackRetriesWithinFinalFlush")]
    public async Task Flush_UnremovablePersistedMapFallsBackToIndependentCreate(bool forceOnly)
    {
        using var fixture = new CiReporterFixture();
        fixture.EnableAzureDevOps();
        string map = Inherit(fixture, withMap: true);
        var first = new AzureDevOpsTestCaseResult("Tests.Fallback", "reporter.tests.dll", "Tests.Fallback", "Failed", 10, "old failure", null, null, null);
        fixture.PutText(map, JsonSerializer.Serialize(new AzureDevOpsResultMapFile(123, 77,
        [
            new AzureDevOpsResultMapEntry(first.AutomatedTestStorage, first.AutomatedTestName, first.TestCaseTitle, 71, [AzureDevOpsResultIdStore.CreateFirstAttempt(first)])
            {
                LastPublishedSubResultSequenceId = 1,
            },
        ])));
        fixture.BeforeDelete = path =>
        {
            if (path == map)
            {
                throw new IOException("persisted map is locked");
            }
        };
        int creates = 0;
        fixture.Client.Setup(value => value.PublishTestResultsWithSubResultsAsync(It.IsAny<AzureDevOpsPublishConfiguration>(), 77, It.IsAny<IReadOnlyList<AzureDevOpsTestCaseResult>>(), It.IsAny<CancellationToken>()))
            .Returns<AzureDevOpsPublishConfiguration, int, IReadOnlyList<AzureDevOpsTestCaseResult>, CancellationToken>((_, _, results, _) =>
            {
                AzureDevOpsTestCaseResult result = Assert.ContainsSingle(results);
                Assert.IsNull(result.Id);
                Assert.AreEqual("Tests.Fallback", result.AutomatedTestName);
                Assert.AreEqual("Passed", result.Outcome);
                return ++creates == 1
                    ? Task.FromException<IReadOnlyList<AzureDevOpsPublishedTestResult>?>(new IOException("fallback create failed"))
                    : Task.FromResult<IReadOnlyList<AzureDevOpsPublishedTestResult>?>([new AzureDevOpsPublishedTestResult(92, new Dictionary<int, int>())]);
            });
        using AzureDevOpsTestResultsPublisher publisher = fixture.Publisher(CiReporterFixture.PublisherOptions(forceOnly ? 2 : 1));
        await publisher.OnTestSessionStartingAsync(new CiReporterFixture.Session());

        await publisher.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Fallback", new PassedTestNodeStateProperty()), CancellationToken.None);
        Assert.AreEqual(forceOnly ? 0 : 1, creates);
        await publisher.OnTestSessionFinishingAsync(new CiReporterFixture.Session());

        Assert.AreEqual(2, creates);
        fixture.Client.Verify(value => value.UpdateTestResultsWithSubResultsAsync(It.IsAny<AzureDevOpsPublishConfiguration>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<AzureDevOpsTestCaseResult>>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains("persisted map is locked", fixture.LogText);
        Assert.Contains("fallback create failed", fixture.LogText);
        Assert.IsEmpty(fixture.Displays);
    }

    [TestMethod]
    [DataRow(0, 5000L, 1L, DisplayName = "Result_PassedMidpoint")]
    [DataRow(0, -5000L, -1L, DisplayName = "Result_NegativeMidpoint")]
    [DataRow(1, 15000L, 2L, DisplayName = "Result_FailedExceptionPrecedence")]
    [DataRow(2, 15000L, 2L, DisplayName = "Result_ErrorExceptionPrecedence")]
    [DataRow(3, 15000L, 2L, DisplayName = "Result_Skipped")]
    [DataRow(4, 15000L, 2L, DisplayName = "Result_TimeoutExplicitReason")]
    [DataRow(5, 15000L, 2L, DisplayName = "Result_CanceledExceptionPrecedence")]
    [DataRow(6, 15000L, 2L, DisplayName = "Result_TimeoutWhitespaceFallback")]
    [DataRow(7, 0L, 0L, DisplayName = "Result_CustomStateIgnored")]
    [DataRow(8, 0L, 0L, DisplayName = "Result_StatelessIgnored")]
    [DataRow(9, 0L, 0L, DisplayName = "Result_DiscoveredIgnored")]
    [DataRow(10, 0L, 0L, DisplayName = "Result_InProgressIgnored")]
    public void ResultFactory_TerminalPartitionsKeepOutcomeAndDiagnosticPrecedence(int partition, long ticks, long expectedMilliseconds)
    {
        using var fixture = new CiReporterFixture();
        IProperty? state = State(partition);
        TestNodeUpdateMessage update = CiReporterFixture.UpdateNamed("Tests.Result", "specific data row",
        [
            .. state is null ? Array.Empty<IProperty>() : [state],
            new TimingProperty(new TimingInfo(CiReporterFixture.Epoch, CiReporterFixture.Epoch.AddTicks(ticks), TimeSpan.FromTicks(ticks))),
            new FileArtifactProperty(new FileInfo(Path.Combine(fixture.Root, "owned.log")), "artifact label"),
            new StandardOutputProperty(string.Empty),
            new StandardErrorProperty("error log"),
        ]);

        AzureDevOpsTestCaseResultWithAttachments? captured = AzureDevOpsTestResultsPublisher.CreateTestCaseResult(update.TestNode, "storage.dll");

        if (partition >= 7)
        {
            Assert.IsNull(captured);
            return;
        }

        Assert.IsNotNull(captured);
        Assert.AreEqual("Tests.Result (specific data row)", captured.Result.AutomatedTestName);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.CanonicalTestNameMetadataPrefix + "12", captured.Result.Comment);
        Assert.AreEqual("specific data row", captured.Result.TestCaseTitle);
        Assert.AreEqual("storage.dll", captured.Result.AutomatedTestStorage);
        Assert.AreEqual(expectedMilliseconds, captured.Result.DurationInMs);
        Assert.AreEqual(CiReporterFixture.Epoch, captured.Result.StartedDate);
        Assert.AreEqual(CiReporterFixture.Epoch.AddTicks(ticks), captured.Result.CompletedDate);
        Assert.AreEqual(partition switch { 0 => "Passed", 3 => "NotExecuted", 5 => "Aborted", _ => "Failed" }, captured.Result.Outcome);
        Assert.AreEqual(
            partition switch
            {
                0 => "passed explanation",
                3 => "skipped explanation",
                4 => string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingTimeoutErrorMessageWithReason, "explicit timeout"),
                6 => AzureDevOpsResources.AzureDevOpsLivePublishingTimeoutErrorMessage,
                _ => "exception detail",
            }, captured.Result.ErrorMessage);
        Assert.AreEqual("Completed", captured.Result.State);
        Assert.HasCount(partition is 0 or 3 ? 0 : 2, captured.Attachments);
        if (captured.Attachments.Count > 0)
        {
            Assert.AreEqual(Path.Combine(fixture.Root, "owned.log"), captured.Attachments[0].FilePath);
            Assert.AreEqual("artifact label", captured.Attachments[0].Comment);
            Assert.AreEqual("stderr.log", captured.Attachments[1].FileName);
            Assert.AreEqual("error log", captured.Attachments[1].InlineContent);
            Assert.AreEqual("GeneralAttachment", captured.Attachments[1].AttachmentType);
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Result_DuplicateStdout")]
    [DataRow(true, DisplayName = "Result_DuplicateStderr")]
    public void ResultFactory_DuplicateConsolePropertiesAreRejected(bool stderr)
    {
        IProperty first = stderr ? new StandardErrorProperty("one") : new StandardOutputProperty("one");
        IProperty second = stderr ? new StandardErrorProperty("two") : new StandardOutputProperty("two");
        TestNode node = CiReporterFixture.Update("Tests.Duplicate", new FailedTestNodeStateProperty(), first, second).TestNode;

        InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(() => AzureDevOpsTestResultsPublisher.CreateTestCaseResult(node, "tests.dll"));

        Assert.Contains(stderr ? nameof(StandardErrorProperty) : nameof(StandardOutputProperty), error.Message);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "InlineTruncation_Ascii")]
    [DataRow(1, DisplayName = "InlineTruncation_TwoByte")]
    [DataRow(2, DisplayName = "InlineTruncation_ThreeByte")]
    [DataRow(3, DisplayName = "InlineTruncation_SupplementaryPair")]
    [DataRow(4, DisplayName = "InlineTruncation_LoneHighAtCut")]
    [DataRow(5, DisplayName = "InlineTruncation_ExactByteLimit")]
    public void ResultFactory_Utf8TruncationHonorsScalarBoundaries(int partition)
    {
        const int Limit = 262144;
        const string Marker = "\n...[truncated]";
        const int Budget = Limit - 15;
        int prefixLength = partition switch { 1 => Budget - 1, 2 => Budget - 2, 3 or 4 => Budget - 3, _ => Budget };
        string prefix = new('a', prefixLength);
        string scalar = partition switch { 1 => "é", 2 => "界", 3 => "😀", 4 => "\uD800", _ => "x" };
        string input = partition == 5 ? new string('é', Limit / 2) : prefix + scalar + new string('z', 32);
        TestNode node = CiReporterFixture.Update("Tests.Inline", new FailedTestNodeStateProperty("failure"), new StandardOutputProperty(input)).TestNode;

        AzureDevOpsTestCaseResultWithAttachments? result = AzureDevOpsTestResultsPublisher.CreateTestCaseResult(node, "tests.dll");

        Assert.IsNotNull(result);
        AzureDevOpsTestResultAttachment attachment = Assert.ContainsSingle(result.Attachments);
        Assert.AreEqual("stdout.log", attachment.FileName);
        Assert.AreEqual("ConsoleLog", attachment.AttachmentType);
        Assert.AreEqual(partition == 5 ? input : prefix + Marker, attachment.InlineContent);
        Assert.IsLessThanOrEqualTo(Limit, Encoding.UTF8.GetByteCount(attachment.InlineContent!));
    }

    private static string Inherit(CiReporterFixture fixture, bool withMap = false)
    {
        fixture.Variables[AzureDevOpsConstants.TestRunIdEnvironmentVariableName] = "123:77";
        string map = Path.Combine(fixture.Root, "results-map.json");
        if (withMap)
        {
            fixture.Variables[AzureDevOpsConstants.ResultMapPathEnvironmentVariableName] = "123:" + map;
        }

        return map;
    }

    private static IProperty? State(int partition)
        => partition switch
        {
            0 => new PassedTestNodeStateProperty("passed explanation"),
            1 => new FailedTestNodeStateProperty(new InvalidOperationException("exception detail"), "ignored explanation"),
            2 => new ErrorTestNodeStateProperty(new InvalidOperationException("exception detail"), "ignored explanation"),
            3 => new SkippedTestNodeStateProperty("skipped explanation"),
            4 => new TimeoutTestNodeStateProperty(new InvalidOperationException("exception detail"), "explicit timeout"),
#pragma warning disable CS0618, MTP0001
            5 => new CancelledTestNodeStateProperty(new InvalidOperationException("exception detail"), "ignored explanation"),
#pragma warning restore CS0618, MTP0001
            6 => new TimeoutTestNodeStateProperty(new InvalidOperationException("exception detail"), " "),
            7 => new CustomState(),
            8 => null,
            9 => new DiscoveredTestNodeStateProperty(),
            _ => new InProgressTestNodeStateProperty(),
        };

    private sealed class CustomState() : TestNodeStateProperty("custom state");
}
