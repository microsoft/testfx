// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using System.Text.Json;

using ghactions::Microsoft.Testing.Extensions.GitHubActionsReport;
using ghactions::Microsoft.Testing.Extensions.GitHubActionsReport.Resources;

using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.TestHost;

using Moq;

using GitHubActionsTerminalKind = ghactions::Microsoft.Testing.Extensions.TerminalKind;
using GitHubActionsTestFailureDetails = ghactions::Microsoft.Testing.Extensions.TestFailureDetails;
using GitHubActionsTestRecord = ghactions::Microsoft.Testing.Extensions.TestRecord;
using GitHubCiRunSummaryAggregate = ghactions::Microsoft.Testing.Extensions.CiRunSummaryAggregate;
using GitHubCiRunSummaryAggregation = ghactions::Microsoft.Testing.Extensions.CiRunSummaryAggregation;
using GitHubCiRunSummaryAttachment = ghactions::Microsoft.Testing.Extensions.CiRunSummaryAttachment;
using GitHubCiRunSummaryModule = ghactions::Microsoft.Testing.Extensions.CiRunSummaryModule;
using GitHubCiRunSummaryTest = ghactions::Microsoft.Testing.Extensions.CiRunSummaryTest;
using GitHubSummaryPostProcessor = ghactions::Microsoft.Testing.Extensions.GitHubActionsReport.GitHubActionsSummaryArtifactPostProcessor;

namespace Microsoft.Testing.Extensions.UnitTests;

public sealed partial class GitHubActionsSummaryReporterTests
{
    [TestMethod]
    [DataRow("failed", DisplayName = "Failed test attachment")]
    [DataRow("error", DisplayName = "Error test attachment")]
    [DataRow("timeout", DisplayName = "Timeout test attachment")]
    public async Task ConsumeAsync_FinalFailureKinds_StageBeforeSessionFinishes(string outcome)
    {
        using SummaryExecutionFixture fixture = new(exitCode: 2, stageAttachments: true);
        string source = WriteAttachmentSource(fixture, "failure.log", [0, 17, 255]);
        await fixture.Reporter.OnTestSessionStartingAsync(fixture.Session.Object);

        await fixture.Reporter.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateAttachmentUpdate(fixture, "Failure", CreateAttachmentState(outcome), source),
            TestContext.CancellationToken);

        string stagedPath = Assert.ContainsSingle(Directory.GetFiles(fixture.AttachmentsDirectory, "*.log", SearchOption.AllDirectories));
        Assert.AreSequenceEqual(new byte[] { 0, 17, 255 }, File.ReadAllBytes(stagedPath));
        Assert.IsEmpty(Directory.GetFiles(fixture.AttachmentsDirectory, "attachments.json", SearchOption.AllDirectories));

        await fixture.Reporter.OnTestSessionFinishingAsync(fixture.Session.Object);

        GitHubCiRunSummaryModule module = Assert.ContainsSingle(Assert.ContainsSingle(fixture.History.Writes));
        GitHubCiRunSummaryTest failure = Assert.ContainsSingle(module.Failures);
        GitHubCiRunSummaryAttachment attachment = Assert.ContainsSingle(failure.Attachments!);
        Assert.AreEqual(1, module.FailedTests);
        Assert.AreEqual("staged", attachment.Status);
        Assert.AreEqual(3L, attachment.SizeBytes);
        Assert.AreEqual(stagedPath, Path.Combine(fixture.AttachmentsDirectory, attachment.RelativePath!.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains(attachment.RelativePath, File.ReadAllText(fixture.SummaryPath));
        using JsonDocument manifest = ReadAttachmentManifest(fixture);
        JsonElement row = Assert.ContainsSingle(manifest.RootElement.GetProperty("tests").EnumerateArray());
        Assert.AreEqual("Failure", row.GetProperty("uid").GetString());
        Assert.AreEqual(attachment.RelativePath, row.GetProperty("attachments")[0].GetProperty("relativePath").GetString());
        Assert.IsEmpty(fixture.Warnings);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Unexpected file-system exception")]
    [DataRow(true, DisplayName = "Warning callback exception")]
    public async Task ConsumeAsync_AttachmentFailure_DoesNotDropFailedRecordOrHistory(bool warningCallbackFails)
    {
        using SummaryExecutionFixture fixture = new(historyOnly: true, exitCode: 2, stageAttachments: true);
        string missing = Path.Combine(fixture.DirectoryPath, "missing.log");
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(value => value.IsRegularFile(It.IsAny<string>())).Returns(true);
        // ArgumentException is a normal best-effort file error; use an unexpected failure to reach
        // the reporter isolation catch rather than the collector's unavailable-file path.
        fileSystem.Setup(value => value.NewFileStream(It.IsAny<string>(), FileMode.Open, FileAccess.Read, FileShare.Read))
            .Throws(new InvalidOperationException("unexpected attachment failure"));
        if (warningCallbackFails)
        {
            fixture.Output.Setup(value => value.DisplayAsync(
                It.IsAny<Microsoft.Testing.Platform.Extensions.OutputDevice.IOutputDeviceDataProducer>(),
                It.IsAny<IOutputDeviceData>(),
                It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("warning callback failed"));
        }

        GitHubActionsSummaryReporter reporter = warningCallbackFails ? fixture.Reporter : fixture.CreateReporter(fileSystem.Object);
        await reporter.OnTestSessionStartingAsync(fixture.Session.Object);

        await reporter.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateAttachmentUpdate(fixture, "Failure", new FailedTestNodeStateProperty("original failure"), missing),
            TestContext.CancellationToken);
        await reporter.OnTestSessionFinishingAsync(fixture.Session.Object);

        GitHubCiRunSummaryModule module = Assert.ContainsSingle(Assert.ContainsSingle(fixture.History.Writes));
        Assert.AreEqual(1, module.TotalTests);
        Assert.AreEqual(1, module.FailedTests);
        Assert.AreEqual(AtLeastOneTestFailedExitCode, module.ExitCode);
        GitHubCiRunSummaryTest failure = Assert.ContainsSingle(module.Failures);
        Assert.AreEqual("Tests.Failure", failure.FullyQualifiedName);
        Assert.AreEqual("original failure", failure.ErrorMessage);
        Assert.IsNull(failure.Attachments);
        Assert.AreEqual("failed", Assert.ContainsSingle(module.HistoryTests).Outcome);
        string diagnostic = warningCallbackFails ? "warning callback failed" : "unexpected attachment failure";
        Assert.Contains(
            invocation => invocation.Method.Name == nameof(ILogger.Log)
                && invocation.Arguments[1].ToString()!.Contains(diagnostic, StringComparison.Ordinal),
            fixture.Logger.Invocations);
        if (!warningCallbackFails)
        {
            Assert.Contains(GitHubActionsResources.AttachmentReportingWarning, fixture.Warnings);
        }
    }

    [TestMethod]
    public async Task ConsumeAsync_AttachmentCancellation_PropagatesWithoutSwallowing()
    {
        using SummaryExecutionFixture fixture = new(historyOnly: true, exitCode: 2, stageAttachments: true);
        using var cancellation = new CancellationTokenSource();
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(value => value.IsRegularFile(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(value => value.NewFileStream(It.IsAny<string>(), FileMode.Open, FileAccess.Read, FileShare.Read))
            .Throws(new OperationCanceledException(cancellation.Token));
        GitHubActionsSummaryReporter reporter = fixture.CreateReporter(fileSystem.Object);
        await reporter.OnTestSessionStartingAsync(fixture.Session.Object);

        OperationCanceledException error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => reporter.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateAttachmentUpdate(fixture, "Failure", new FailedTestNodeStateProperty("failure"), Path.Combine(fixture.DirectoryPath, "canceled.log")),
            cancellation.Token));

        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        fileSystem.Verify(value => value.NewFileStream(It.IsAny<string>(), FileMode.Open, FileAccess.Read, FileShare.Read), Times.Once);
        Assert.IsFalse(Directory.Exists(fixture.AttachmentsDirectory));
        Assert.IsEmpty(fixture.History.Writes);
        Assert.IsEmpty(fixture.Warnings);
    }

#if NETCOREAPP
    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    [DataRow(false, DisplayName = "FIFO with no writer")]
    [DataRow(true, DisplayName = "Symlink to FIFO with no writer")]
    public async Task SessionFinishing_LinuxFifo_RejectsBeforeOpenAndPreservesPrimarySummary(bool symlink)
    {
        using SummaryExecutionFixture fixture = new(exitCode: 2, stageAttachments: true);
        string fifo = Path.Combine(fixture.DirectoryPath, "no-writer.fifo");
        using (var process = new Process
        {
            StartInfo = new ProcessStartInfo("mkfifo")
            {
                UseShellExecute = false,
                ArgumentList = { "--", fifo },
            },
        })
        {
            Assert.IsTrue(process.Start());
            if (!process.WaitForExit(30_000))
            {
                process.Kill();
                process.WaitForExit();
                Assert.Fail("The FIFO fixture could not be created within the setup timeout.");
            }

            Assert.AreEqual(0, process.ExitCode);
        }

        string source = fifo;
        if (symlink)
        {
            source = Path.Combine(fixture.DirectoryPath, "fifo-link");
            File.CreateSymbolicLink(source, fifo);
        }

        Assert.IsTrue(File.Exists(source), "File.Exists alone must not be used to classify a FIFO.");
        var real = new SystemFileSystem();
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(value => value.IsRegularFile(It.IsAny<string>()))
            .Returns((string path) => real.IsRegularFile(path));
        fileSystem.Setup(value => value.CreateDirectory(It.IsAny<string>()))
            .Returns((string path) => real.CreateDirectory(path));
        fileSystem.Setup(value => value.ExistFile(It.IsAny<string>()))
            .Returns((string path) => real.ExistFile(path));
        fileSystem.Setup(value => value.DeleteFile(It.IsAny<string>()))
            .Callback((string path) => real.DeleteFile(path));
        fileSystem.Setup(value => value.MoveFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Callback((string from, string to, bool overwrite) => real.MoveFile(from, to, overwrite));
        fileSystem.Setup(value => value.NewFileStream(It.IsAny<string>(), It.IsAny<FileMode>(), It.IsAny<FileAccess>(), It.IsAny<FileShare>()))
            .Returns((string path, FileMode mode, FileAccess access, FileShare share) =>
            {
                Assert.AreNotEqual(source, path, "A no-writer FIFO must be rejected before attempting a blocking open.");
                return real.NewFileStream(path, mode, access, share);
            });
        GitHubActionsSummaryReporter reporter = fixture.CreateReporter(fileSystem.Object);
        await reporter.OnTestSessionStartingAsync(fixture.Session.Object);

        await reporter.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateAttachmentUpdate(fixture, "FifoFailure", new FailedTestNodeStateProperty("original test failure"), source),
            TestContext.CancellationToken);
        await reporter.OnTestSessionFinishingAsync(fixture.Session.Object);

        fileSystem.Verify(value => value.NewFileStream(source, It.IsAny<FileMode>(), It.IsAny<FileAccess>(), It.IsAny<FileShare>()), Times.Never);
        GitHubCiRunSummaryModule module = Assert.ContainsSingle(Assert.ContainsSingle(fixture.History.Writes));
        Assert.AreEqual(1, module.TotalTests);
        Assert.AreEqual(1, module.FailedTests);
        Assert.AreEqual(2, module.ExitCode);
        GitHubCiRunSummaryAttachment attachment = Assert.ContainsSingle(Assert.ContainsSingle(module.Failures).Attachments!);
        Assert.AreEqual("non-regular", attachment.Status);
        Assert.IsNull(attachment.RelativePath);
        Assert.AreEqual(0L, attachment.SizeBytes);
        Assert.Contains("non-regular", Assert.ContainsSingle(fixture.Warnings));
        string markdown = File.ReadAllText(fixture.SummaryPath);
        Assert.Contains("original test failure", markdown);
        Assert.Contains("| 1 | 0 | 1 | 0 | 0 | 0ms |", markdown);
        using JsonDocument manifest = ReadAttachmentManifest(fixture);
        Assert.AreEqual("non-regular", manifest.RootElement.GetProperty("tests")[0].GetProperty("attachments")[0].GetProperty("status").GetString());
    }
#endif

    [TestMethod]
    [DataRow("passed", false, DisplayName = "Passing test excluded")]
    [DataRow("skipped", false, DisplayName = "Skipped test excluded")]
    [DataRow("discovered", false, DisplayName = "Discovery excluded")]
    [DataRow("in-progress", false, DisplayName = "In-progress test excluded")]
    [DataRow("failed", true, DisplayName = "Superseded retry excluded")]
    public async Task ConsumeAsync_NonFailuresAndSupersededRetries_DoNotStage(string outcome, bool superseded)
    {
        using SummaryExecutionFixture fixture = new(stageAttachments: true);
        string source = WriteAttachmentSource(fixture, "ignored.log", [19]);
        await fixture.Reporter.OnTestSessionStartingAsync(fixture.Session.Object);

        await fixture.Reporter.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateAttachmentUpdate(fixture, "Ignored", CreateAttachmentState(outcome), source, superseded),
            TestContext.CancellationToken);
        await fixture.Reporter.OnTestSessionFinishingAsync(fixture.Session.Object);

        Assert.IsFalse(Directory.Exists(fixture.AttachmentsDirectory));
        GitHubCiRunSummaryModule module = Assert.ContainsSingle(Assert.ContainsSingle(fixture.History.Writes));
        Assert.AreEqual(0, module.FailedTests);
        Assert.IsEmpty(module.Failures);
        Assert.AreSequenceEqual(new byte[] { 19 }, File.ReadAllBytes(source));
        Assert.IsEmpty(fixture.Warnings);
    }

    [TestMethod]
    [DataRow(false, true, "true", DisplayName = "Attachment opt-in absent")]
    [DataRow(true, false, "true", DisplayName = "Master report option absent")]
    [DataRow(true, true, null, DisplayName = "GitHub Actions environment absent")]
    [DataRow(true, true, "false", DisplayName = "GitHub Actions environment false")]
    public async Task SessionFinishing_AttachmentsRequireOptInAndGitHubGate(bool optIn, bool master, string? githubActions)
    {
        using SummaryExecutionFixture fixture = new(exitCode: 2, stageAttachments: optIn, reportGh: master, githubActions: githubActions);
        string source = WriteAttachmentSource(fixture, "not-opted-in.log", [21]);
        await fixture.Reporter.OnTestSessionStartingAsync(fixture.Session.Object);

        await fixture.Reporter.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateAttachmentUpdate(fixture, "Failure", new FailedTestNodeStateProperty("failure"), source),
            TestContext.CancellationToken);
        await fixture.Reporter.OnTestSessionFinishingAsync(fixture.Session.Object);

        Assert.IsFalse(Directory.Exists(fixture.AttachmentsDirectory));
        GitHubCiRunSummaryModule module = Assert.ContainsSingle(Assert.ContainsSingle(fixture.History.Writes));
        Assert.AreEqual(1, module.FailedTests);
        Assert.IsNull(Assert.ContainsSingle(module.Failures).Attachments);
        Assert.AreSequenceEqual(new byte[] { 21 }, File.ReadAllBytes(source));
        Assert.IsEmpty(fixture.Warnings);
    }

    [TestMethod]
    [DataRow(true, false, DisplayName = "Summary disabled")]
    [DataRow(false, true, DisplayName = "Failure details disabled")]
    [DataRow(true, true, DisplayName = "Summary and failure details disabled")]
    public async Task SessionFinishing_SummaryOrFailureDetailsDisabled_StillStagesAttachments(bool summaryOff, bool detailsOff)
    {
        using SummaryExecutionFixture fixture = new(historyOnly: summaryOff, exitCode: 2, stageAttachments: true, includeFailureDetails: !detailsOff);
        string source = WriteAttachmentSource(fixture, "failure.log", [23, 24]);
        await fixture.Reporter.OnTestSessionStartingAsync(fixture.Session.Object);
        await fixture.Reporter.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateAttachmentUpdate(fixture, "Failure", new FailedTestNodeStateProperty("failure"), source),
            TestContext.CancellationToken);

        await fixture.Reporter.OnTestSessionFinishingAsync(fixture.Session.Object);

        using JsonDocument manifest = ReadAttachmentManifest(fixture);
        JsonElement attachment = manifest.RootElement.GetProperty("tests")[0].GetProperty("attachments")[0];
        Assert.AreEqual("staged", attachment.GetProperty("status").GetString());
        string relativePath = attachment.GetProperty("relativePath").GetString()!;
        Assert.AreSequenceEqual(new byte[] { 23, 24 }, File.ReadAllBytes(
            Path.Combine(fixture.AttachmentsDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.AreEqual(!summaryOff, File.Exists(fixture.SummaryPath));
        if (!summaryOff)
        {
            string markdown = File.ReadAllText(fixture.SummaryPath);
            Assert.Contains("### ❌ Failures (1)", markdown);
            Assert.Contains("Tests.Failure", markdown);
            Assert.DoesNotContain(relativePath, markdown);
            Assert.DoesNotContain("Attachments (", markdown);
        }

        Assert.IsEmpty(fixture.Artifacts);
        Assert.IsEmpty(fixture.Warnings);
    }

    [TestMethod]
    public async Task SessionFinishing_AttachmentsOnly_DoesNotEmitFragmentWhenSummaryPostProcessorIsDisabled()
    {
        using SummaryExecutionFixture fixture = new(
            historyOnly: true, exitCode: 2, stageAttachments: true, deferToArtifactPostProcessing: true, historyEnabled: false);
        string source = WriteAttachmentSource(fixture, "attachments-only.log", [25]);
        await fixture.Reporter.OnTestSessionStartingAsync(fixture.Session.Object);
        await fixture.Reporter.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateAttachmentUpdate(fixture, "Failure", new FailedTestNodeStateProperty("failure"), source),
            TestContext.CancellationToken);

        await fixture.Reporter.OnTestSessionFinishingAsync(fixture.Session.Object);

        using JsonDocument manifest = ReadAttachmentManifest(fixture);
        Assert.AreEqual("staged", manifest.RootElement.GetProperty("tests")[0].GetProperty("attachments")[0].GetProperty("status").GetString());
        Assert.IsEmpty(fixture.Artifacts, "Attachments alone must not emit a fragment for a disabled summary postprocessor.");
        Assert.IsFalse(Directory.Exists(Path.Combine(fixture.DirectoryPath, ".ci-summary-fragments")));
        Assert.IsFalse(File.Exists(fixture.SummaryPath));
        Assert.IsEmpty(fixture.Warnings);
    }

    [TestMethod]
    public async Task SessionFinishing_UnavailableAttachment_KeepsFailureAndWarns()
    {
        using SummaryExecutionFixture fixture = new(exitCode: 2, stageAttachments: true);
        string missing = Path.Combine(fixture.DirectoryPath, "missing.log");
        await fixture.Reporter.OnTestSessionStartingAsync(fixture.Session.Object);
        await fixture.Reporter.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateAttachmentUpdate(fixture, "Failure", new FailedTestNodeStateProperty("original failure remains"), missing),
            TestContext.CancellationToken);

        await fixture.Reporter.OnTestSessionFinishingAsync(fixture.Session.Object);

        GitHubCiRunSummaryModule module = Assert.ContainsSingle(Assert.ContainsSingle(fixture.History.Writes));
        Assert.AreEqual(AtLeastOneTestFailedExitCode, module.ExitCode);
        Assert.AreEqual(1, module.FailedTests);
        GitHubCiRunSummaryAttachment attachment = Assert.ContainsSingle(Assert.ContainsSingle(module.Failures).Attachments!);
        Assert.AreEqual("unavailable", attachment.Status);
        Assert.IsNull(attachment.RelativePath);
        Assert.AreEqual(0L, attachment.SizeBytes);
        Assert.Contains("missing.log", Assert.ContainsSingle(fixture.Warnings));
        string markdown = File.ReadAllText(fixture.SummaryPath);
        Assert.Contains("original failure remains", markdown);
        Assert.Contains("- `missing.log` — not staged; see manifest.", markdown);
        using JsonDocument manifest = ReadAttachmentManifest(fixture);
        Assert.AreEqual(
            JsonValueKind.Null,
            manifest.RootElement.GetProperty("tests")[0].GetProperty("attachments")[0].GetProperty("relativePath").ValueKind);
    }

    [TestMethod]
    public async Task SessionFinishing_AllFailuresStageBeyondTwentyRenderedFailures()
    {
        using SummaryExecutionFixture fixture = new(exitCode: 2, stageAttachments: true);
        string source = WriteAttachmentSource(fixture, "failure.log", [27]);
        await fixture.Reporter.OnTestSessionStartingAsync(fixture.Session.Object);
        for (int i = 0; i < 21; i++)
        {
            await fixture.Reporter.ConsumeAsync(
                Mock.Of<IDataProducer>(),
                CreateAttachmentUpdate(fixture, $"Failure{i:D2}", new FailedTestNodeStateProperty("failure"), source),
                TestContext.CancellationToken);
        }

        Assert.HasCount(21, Directory.GetFiles(fixture.AttachmentsDirectory, "*.log", SearchOption.AllDirectories));
        await fixture.Reporter.OnTestSessionFinishingAsync(fixture.Session.Object);

        GitHubCiRunSummaryModule module = Assert.ContainsSingle(Assert.ContainsSingle(fixture.History.Writes));
        Assert.AreEqual(21, module.FailedTests);
        Assert.HasCount(20, module.Failures);
        Assert.IsTrue(module.Failures.All(failure => failure.Attachments is [{ Status: "staged", SizeBytes: 1 }]));
        using JsonDocument manifest = ReadAttachmentManifest(fixture);
        JsonElement tests = manifest.RootElement.GetProperty("tests");
        Assert.AreEqual(21, tests.GetArrayLength());
        JsonElement final = tests[20];
        Assert.AreEqual("Failure20", final.GetProperty("uid").GetString());
        string finalRelativePath = final.GetProperty("attachments")[0].GetProperty("relativePath").GetString()!;
        Assert.AreSequenceEqual(new byte[] { 27 }, File.ReadAllBytes(
            Path.Combine(fixture.AttachmentsDirectory, finalRelativePath.Replace('/', Path.DirectorySeparatorChar))));
        string markdown = File.ReadAllText(fixture.SummaryPath);
        Assert.Contains("Tests.Failure19", markdown);
        Assert.DoesNotContain("Tests.Failure20", markdown);
        Assert.DoesNotContain(finalRelativePath, markdown);
        Assert.IsEmpty(fixture.Warnings);
    }

    [TestMethod]
    public async Task SessionFinishing_SameUidFinalRows_KeepDistinctAttachments()
    {
        using SummaryExecutionFixture fixture = new(exitCode: 2, stageAttachments: true);
        string first = WriteAttachmentSource(fixture, "first-row.log", [34]);
        string second = WriteAttachmentSource(fixture, "second-row.log", [35, 36]);
        await fixture.Reporter.OnTestSessionStartingAsync(fixture.Session.Object);
        foreach ((string displayName, string source) in new[] { ("Rows (one)", first), ("Rows (two)", second) })
        {
            TestNodeUpdateMessage update = CreateAttachmentUpdate(
                fixture, "shared-uid", new FailedTestNodeStateProperty("failure"), source, displayName: displayName);
            update.TestNode.Properties.Add(new TestMethodIdentifierProperty("Tests", string.Empty, "Tests", "Rows", 0, [], "System.Void"));
            await fixture.Reporter.ConsumeAsync(Mock.Of<IDataProducer>(), update, TestContext.CancellationToken);
        }

        await fixture.Reporter.OnTestSessionFinishingAsync(fixture.Session.Object);

        GitHubCiRunSummaryModule module = Assert.ContainsSingle(Assert.ContainsSingle(fixture.History.Writes));
        Assert.AreEqual(2, module.FailedTests);
        Assert.HasCount(2, module.Failures);
        GitHubCiRunSummaryAttachment firstAttachment = Assert.ContainsSingle(module.Failures.Single(test => test.DisplayName == "Rows (one)").Attachments!);
        GitHubCiRunSummaryAttachment secondAttachment = Assert.ContainsSingle(module.Failures.Single(test => test.DisplayName == "Rows (two)").Attachments!);
        Assert.AreNotEqual(firstAttachment.RelativePath, secondAttachment.RelativePath);
        Assert.AreSequenceEqual(new byte[] { 34 }, File.ReadAllBytes(Path.Combine(fixture.AttachmentsDirectory, firstAttachment.RelativePath!.Replace('/', Path.DirectorySeparatorChar))));
        Assert.AreSequenceEqual(new byte[] { 35, 36 }, File.ReadAllBytes(Path.Combine(fixture.AttachmentsDirectory, secondAttachment.RelativePath!.Replace('/', Path.DirectorySeparatorChar))));
        using JsonDocument manifest = ReadAttachmentManifest(fixture);
        JsonElement[] rows = manifest.RootElement.GetProperty("tests").EnumerateArray().ToArray();
        Assert.HasCount(2, rows);
        Assert.IsTrue(rows.All(row => row.GetProperty("uid").GetString() == "shared-uid"));
        Assert.AreSequenceEqual(
            new[] { "Rows (one)", "Rows (two)" },
            rows.Select(row => row.GetProperty("displayName").GetString()).OrderBy(name => name, StringComparer.Ordinal));
        Assert.IsEmpty(fixture.Warnings);
    }

    [TestMethod]
    public async Task SessionStarting_NewSession_UsesIndependentAttachmentManifest()
    {
        using SummaryExecutionFixture fixture = new(exitCode: 2, stageAttachments: true);
        string first = WriteAttachmentSource(fixture, "first-session.log", [37]);
        string second = WriteAttachmentSource(fixture, "second-session.log", [38, 39]);
        await fixture.Reporter.OnTestSessionStartingAsync(fixture.Session.Object);
        await fixture.Reporter.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateAttachmentUpdate(fixture, "Failure", new FailedTestNodeStateProperty("first session"), first),
            TestContext.CancellationToken);
        await fixture.Reporter.OnTestSessionFinishingAsync(fixture.Session.Object);
        fixture.Session.SetupGet(value => value.SessionUid).Returns(new SessionUid("second-attachment-session"));
        await fixture.Reporter.OnTestSessionStartingAsync(fixture.Session.Object);
        await fixture.Reporter.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateAttachmentUpdate(fixture, "Failure", new FailedTestNodeStateProperty("second session"), second),
            TestContext.CancellationToken);

        await fixture.Reporter.OnTestSessionFinishingAsync(fixture.Session.Object);

        Assert.HasCount(2, fixture.History.Writes);
        GitHubCiRunSummaryModule firstModule = Assert.ContainsSingle(fixture.History.Writes[0]);
        GitHubCiRunSummaryModule secondModule = Assert.ContainsSingle(fixture.History.Writes[1]);
        Assert.AreEqual(1, firstModule.FailedTests);
        Assert.AreEqual(1, secondModule.FailedTests);
        GitHubCiRunSummaryAttachment firstAttachment = Assert.ContainsSingle(Assert.ContainsSingle(firstModule.Failures).Attachments!);
        GitHubCiRunSummaryAttachment secondAttachment = Assert.ContainsSingle(Assert.ContainsSingle(secondModule.Failures).Attachments!);
        Assert.AreNotEqual(firstAttachment.RelativePath!.Split('/')[0], secondAttachment.RelativePath!.Split('/')[0]);
        Assert.AreSequenceEqual(new byte[] { 37 }, File.ReadAllBytes(Path.Combine(fixture.AttachmentsDirectory, firstAttachment.RelativePath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.AreSequenceEqual(new byte[] { 38, 39 }, File.ReadAllBytes(Path.Combine(fixture.AttachmentsDirectory, secondAttachment.RelativePath.Replace('/', Path.DirectorySeparatorChar))));
        string[] manifests = Directory.GetFiles(fixture.AttachmentsDirectory, "attachments.json", SearchOption.AllDirectories);
        Assert.HasCount(2, manifests);
        string?[] sessions = manifests.Select(path =>
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(path));
            Assert.AreEqual(1, manifest.RootElement.GetProperty("tests").GetArrayLength());
            return manifest.RootElement.GetProperty("sessionUid").GetString();
        }).OrderBy(session => session, StringComparer.Ordinal).ToArray();
        Assert.AreSequenceEqual(new[] { "second-attachment-session", "summary-coverage-session" }, sessions);
        Assert.IsEmpty(fixture.Warnings);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Retry recovers")]
    [DataRow(true, DisplayName = "Retry remains failed")]
    public async Task SessionFinishing_SupersededRetry_StagesOnlyFinalFailure(bool finalFailure)
    {
        using SummaryExecutionFixture fixture = new(exitCode: finalFailure ? 2 : 0, stageAttachments: true);
        string early = WriteAttachmentSource(fixture, "early.log", [29]);
        string final = WriteAttachmentSource(fixture, "final.log", [30, 31]);
        await fixture.Reporter.OnTestSessionStartingAsync(fixture.Session.Object);
        await fixture.Reporter.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateAttachmentUpdate(fixture, "Retry", new FailedTestNodeStateProperty("superseded failure"), early, superseded: true),
            TestContext.CancellationToken);
        Assert.IsFalse(Directory.Exists(fixture.AttachmentsDirectory));
        await fixture.Reporter.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateAttachmentUpdate(fixture, "Retry", finalFailure ? new FailedTestNodeStateProperty("final failure") : PassedTestNodeStateProperty.CachedInstance,
                final, attempt: 2),
            TestContext.CancellationToken);

        await fixture.Reporter.OnTestSessionFinishingAsync(fixture.Session.Object);

        GitHubCiRunSummaryModule module = Assert.ContainsSingle(Assert.ContainsSingle(fixture.History.Writes));
        Assert.AreEqual(1, module.TotalTests);
        Assert.AreEqual(finalFailure ? 1 : 0, module.FailedTests);
        Assert.AreEqual(finalFailure, Directory.Exists(fixture.AttachmentsDirectory));
        if (finalFailure)
        {
            using JsonDocument manifest = ReadAttachmentManifest(fixture);
            JsonElement row = Assert.ContainsSingle(manifest.RootElement.GetProperty("tests").EnumerateArray());
            JsonElement attachment = Assert.ContainsSingle(row.GetProperty("attachments").EnumerateArray());
            Assert.AreEqual("final.log", attachment.GetProperty("fileName").GetString());
            Assert.AreSequenceEqual(new byte[] { 30, 31 }, File.ReadAllBytes(
                Path.Combine(fixture.AttachmentsDirectory, attachment.GetProperty("relativePath").GetString()!.Replace('/', Path.DirectorySeparatorChar))));
            Assert.IsEmpty(Directory.GetFiles(fixture.AttachmentsDirectory, "*early*", SearchOption.AllDirectories));
        }
        else
        {
            Assert.IsNull(Assert.ContainsSingle(module.FlakyTests).Attachments);
            Assert.DoesNotContain("Attachments (", File.ReadAllText(fixture.SummaryPath));
        }

        Assert.IsEmpty(fixture.Warnings);
    }

    [TestMethod]
    public async Task SessionFinishing_DeferredFragment_RetainsAttachmentsForAggregateRendering()
    {
        using SummaryExecutionFixture fixture = new(exitCode: 2, stageAttachments: true, deferToArtifactPostProcessing: true);
        string source = WriteAttachmentSource(fixture, "deferred.log", [32, 33]);
        await fixture.Reporter.OnTestSessionStartingAsync(fixture.Session.Object);
        await fixture.Reporter.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateAttachmentUpdate(fixture, "Deferred", new FailedTestNodeStateProperty("deferred failure"), source),
            TestContext.CancellationToken);

        await fixture.Reporter.OnTestSessionFinishingAsync(fixture.Session.Object);

        SessionFileArtifact artifact = Assert.ContainsSingle(fixture.Artifacts);
        Assert.AreEqual(GitHubSummaryPostProcessor.FragmentArtifactKind, artifact.Kind);
        GitHubCiRunSummaryAggregate aggregate = GitHubCiRunSummaryAggregation.ReadAndAggregate(
            [new InputArtifact(artifact.FileInfo.FullName, artifact.Kind, null, null, null, null)],
            GitHubSummaryPostProcessor.Provider, new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None));
        GitHubCiRunSummaryTest failure = Assert.ContainsSingle(Assert.ContainsSingle(aggregate.Modules).Failures);
        Assert.IsNotNull(failure.Attachments, "Deferred failure fragments must retain attachment metadata.");
        GitHubCiRunSummaryAttachment attachment = Assert.ContainsSingle(failure.Attachments);
        Assert.AreEqual("staged", attachment.Status);
        Assert.AreEqual(2L, attachment.SizeBytes);
        Assert.AreSequenceEqual(new byte[] { 32, 33 }, File.ReadAllBytes(
            Path.Combine(fixture.AttachmentsDirectory, attachment.RelativePath!.Replace('/', Path.DirectorySeparatorChar))));
        string markdown = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate).Markdown;
        Assert.Contains("deferred failure", markdown);
        Assert.Contains($"- `{attachment.RelativePath}` (2 bytes)", markdown);
        using JsonDocument manifest = ReadAttachmentManifest(fixture);
        Assert.AreEqual(
            attachment.RelativePath,
            manifest.RootElement.GetProperty("tests")[0].GetProperty("attachments")[0].GetProperty("relativePath").GetString());
        Assert.IsFalse(File.Exists(fixture.SummaryPath));
        Assert.IsEmpty(fixture.History.Writes);
        Assert.IsEmpty(fixture.Warnings);
    }

    [TestMethod]
    public async Task CreateModule_FragmentRoundTrip_PreservesAttachmentsOnlyOnFailures()
    {
        using SummaryExecutionFixture fixture = new();
        var attachment = new GitHubCiRunSummaryAttachment { FileName = "failure.log", RelativePath = "module/1/1-failure.log", SizeBytes = 37, Status = "staged" };
        GitHubActionsTestRecord[] records =
        [
            new("Failure", "Tests.Failure", GitHubActionsTerminalKind.Failed, TimeSpan.FromSeconds(1),
                new GitHubActionsTestFailureDetails(null, null, null, null, 0) { Attachments = [attachment] }),
        ];
        GitHubCiRunSummaryModule module = GitHubCiRunSummaryAggregation.CreateModule(
            records, "Tests", Path.Combine(fixture.DirectoryPath, "Tests.dll"), "net8.0", "x64", "execution", "session", 1, 2);

        string path = await GitHubCiRunSummaryAggregation.WriteFragmentAsync(fixture.DirectoryPath, "github-actions", "github-actions", module);
        using var fragment = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement jsonModule = fragment.RootElement.GetProperty("module");
        JsonElement jsonAttachment = Assert.ContainsSingle(jsonModule.GetProperty("failures")[0].GetProperty("attachments").EnumerateArray());
        Assert.AreEqual("module/1/1-failure.log", jsonAttachment.GetProperty("relativePath").GetString());
        Assert.AreEqual("staged", jsonAttachment.GetProperty("status").GetString());
        Assert.AreEqual(37L, jsonAttachment.GetProperty("sizeBytes").GetInt64());
        Assert.IsFalse(jsonModule.GetProperty("slowestTests")[0].TryGetProperty("attachments", out _));
        GitHubCiRunSummaryAggregate aggregate = GitHubCiRunSummaryAggregation.ReadAndAggregate(
            [new InputArtifact(path, GitHubSummaryPostProcessor.FragmentArtifactKind, null, null, null, null)],
            "github-actions", new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None));

        GitHubCiRunSummaryModule restored = Assert.ContainsSingle(aggregate.Modules);
        GitHubCiRunSummaryAttachment restoredAttachment = Assert.ContainsSingle(Assert.ContainsSingle(restored.Failures).Attachments!);
        Assert.AreEqual("failure.log", restoredAttachment.FileName);
        Assert.AreEqual("module/1/1-failure.log", restoredAttachment.RelativePath);
        Assert.AreEqual(37L, restoredAttachment.SizeBytes);
        Assert.AreEqual("staged", restoredAttachment.Status);
        Assert.IsNull(Assert.ContainsSingle(restored.SlowestTests).Attachments);
        Assert.Contains("- `module/1/1-failure.log` (37 bytes)", GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate).Markdown);
        string directMarkdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "Tests", "net8.0", AtLeastOneTestFailedExitCode);
        Assert.Contains("- `module/1/1-failure.log` (37 bytes)", directMarkdown);
        Assert.DoesNotContain("```text", directMarkdown, "Attachment-only failures must not fabricate a diagnostic message.");
    }

    [TestMethod]
    public async Task ReadAndAggregate_LegacyFragmentWithoutAttachments_RemainsCompatible()
    {
        using SummaryExecutionFixture fixture = new();
        string path = Path.Combine(fixture.DirectoryPath, "legacy.json");
        // Independent legacy schema, not a round trip through today's serializer.
        File.WriteAllText(
            path,
            """
            {
              "schemaVersion": 1,
              "provider": "github-actions",
              "module": {
                "assemblyName": "Legacy.Tests", "modulePath": "Legacy.Tests.dll",
                "targetFramework": "net8.0", "architecture": "x64",
                "executionId": "old-execution", "sessionUid": "old-session", "attemptNumber": 1,
                "exitCode": 2, "totalTests": 1, "passedTests": 0, "failedTests": 1, "skippedTests": 0,
                "failures": [{ "displayName": "Failure", "fullyQualifiedName": "Legacy.Failure", "errorMessage": "legacy message" }]
              }
            }
            """);

        GitHubCiRunSummaryAggregate aggregate = GitHubCiRunSummaryAggregation.ReadAndAggregate(
            [new InputArtifact(path, GitHubSummaryPostProcessor.FragmentArtifactKind, null, null, null, null)],
            "github-actions", new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None));

        Assert.AreEqual(1, aggregate.FailedTests);
        Assert.IsNull(Assert.ContainsSingle(Assert.ContainsSingle(aggregate.Modules).Failures).Attachments);
        string markdown = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate).Markdown;
        Assert.Contains("legacy message", markdown);
        Assert.Contains("Legacy.Failure", markdown);
        Assert.DoesNotContain("Attachments (", markdown);

        // The helper model is also compiled into AzureDevOpsReport; legacy optional metadata must stay additive there.
        string azurePath = Path.Combine(fixture.DirectoryPath, "legacy-azure.json");
        File.WriteAllText(azurePath, File.ReadAllText(path).Replace("\"github-actions\"", "\"azure-devops\""));
        CiRunSummaryAggregate azure = CiRunSummaryAggregation.ReadAndAggregate(
            [new InputArtifact(azurePath, "microsoft.testing.azure-devops-summary-fragment", null, null, null, null)],
            "azure-devops", new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None));
        Assert.AreEqual(1, azure.FailedTests);
        Assert.IsNull(Assert.ContainsSingle(Assert.ContainsSingle(azure.Modules).Failures).Attachments);
    }

    [TestMethod]
    public async Task ReadAndAggregate_ProcessRetries_RenderLastFailureAndKeepEarlierArchive()
    {
        using SummaryExecutionFixture fixture = new();
        string earlySource = WriteAttachmentSource(fixture, "early.log", [41]);
        string finalSource = WriteAttachmentSource(fixture, "last.log", [42, 43]);
        var firstCollector = new GitHubActionsAttachmentCollector(fixture.AttachmentsDirectory, new Microsoft.Testing.Platform.Helpers.SystemFileSystem(), _ => Task.CompletedTask);
        var lastCollector = new GitHubActionsAttachmentCollector(fixture.AttachmentsDirectory, new Microsoft.Testing.Platform.Helpers.SystemFileSystem(), _ => Task.CompletedTask);
        GitHubCiRunSummaryAttachment[] early = (await firstCollector.CaptureAsync(
            CreateAttachmentUpdate(fixture, "Retry", new FailedTestNodeStateProperty("early"), earlySource).TestNode, TestContext.CancellationToken))!;
        GitHubCiRunSummaryAttachment[] last = (await lastCollector.CaptureAsync(
            CreateAttachmentUpdate(fixture, "Retry", new FailedTestNodeStateProperty("last"), finalSource).TestNode, TestContext.CancellationToken))!;
        GitHubCiRunSummaryModule first = CreateRetryModule("first-session", 1, 0, 1);
        first.Failures = [new() { FullyQualifiedName = "Tests.Retry", DisplayName = "Tests.Retry", ErrorMessage = "early failure", Attachments = early }];
        GitHubCiRunSummaryModule final = CreateRetryModule("last-session", 2, 0, 1);
        final.Failures = [new() { FullyQualifiedName = "Tests.Retry", DisplayName = "Tests.Retry", ErrorMessage = "last failure", Attachments = last }];
        await firstCollector.WriteManifestAsync(first, TestContext.CancellationToken);
        await lastCollector.WriteManifestAsync(final, TestContext.CancellationToken);
        string firstPath = await GitHubCiRunSummaryAggregation.WriteFragmentAsync(fixture.DirectoryPath, "github-actions", "github-actions", first);
        string finalPath = await GitHubCiRunSummaryAggregation.WriteFragmentAsync(fixture.DirectoryPath, "github-actions", "github-actions", final);
        var processor = new GitHubSummaryPostProcessor(
            new Microsoft.Testing.Extensions.UnitTests.Helpers.TestCommandLineOptions([]),
            Mock.Of<Microsoft.Testing.Platform.Helpers.IEnvironment>(),
            new Microsoft.Testing.Platform.Helpers.SystemFileSystem(),
            Mock.Of<Microsoft.Testing.Platform.Logging.ILoggerFactory>(), static () => true);
        var context = new ArtifactPostProcessingContext(
            ArtifactPostProcessingTruncationReason.None, ArtifactPostProcessingMode.RetryAttempts,
            new ArtifactPostProcessingRunSummary(1, 0, 1, 0, TimeSpan.Zero, 2, 1));

        ProcessedArtifact? output = await processor.ProcessAsync(
            [
                new InputArtifact(firstPath, GitHubSummaryPostProcessor.FragmentArtifactKind, null, null, null, "1"),
                new InputArtifact(finalPath, GitHubSummaryPostProcessor.FragmentArtifactKind, null, null, null, "2"),
            ], fixture.DirectoryPath, context, TestContext.CancellationToken);

        Assert.IsNotNull(output);
        GitHubCiRunSummaryAggregate aggregate = GitHubCiRunSummaryAggregation.ReadAndAggregate(
            [new InputArtifact(output.Path, output.Kind, null, null, null, null)],
            "github-actions", new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None));
        GitHubCiRunSummaryAttachment attachment = Assert.ContainsSingle(Assert.ContainsSingle(Assert.ContainsSingle(aggregate.Modules).Failures).Attachments!);
        Assert.AreEqual(last[0].RelativePath, attachment.RelativePath);
        string markdown = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate).Markdown;
        Assert.Contains("last failure", markdown);
        Assert.Contains(last[0].RelativePath!, markdown);
        Assert.DoesNotContain(early[0].RelativePath!, markdown);
        Assert.AreSequenceEqual(new byte[] { 41 }, File.ReadAllBytes(Path.Combine(fixture.AttachmentsDirectory, early[0].RelativePath!.Replace('/', Path.DirectorySeparatorChar))));
        Assert.AreSequenceEqual(new byte[] { 42, 43 }, File.ReadAllBytes(Path.Combine(fixture.AttachmentsDirectory, last[0].RelativePath!.Replace('/', Path.DirectorySeparatorChar))));
        Assert.HasCount(2, Directory.GetFiles(fixture.AttachmentsDirectory, "attachments.json", SearchOption.AllDirectories));
    }

    [TestMethod]
    public void BuildMarkdown_WithAttachments_RendersExactArchiveRelativePaths()
    {
        GitHubActionsTestRecord[] records =
        [
            new("Failure", "Tests.Failure", GitHubActionsTerminalKind.Failed, TimeSpan.Zero,
                new GitHubActionsTestFailureDetails("expected failure", null, null, null, 0)
                {
                    Attachments =
                    [
                        new() { FileName = "failure.log", RelativePath = "module/1/1-failure.log", SizeBytes = 47, Status = "staged" },
                        new() { FileName = "missing.log", Status = "unavailable" },
                    ],
                }),
        ];

        string markdown = GitHubActionsSummaryReporter.BuildMarkdown(records, "Tests", "net8.0", AtLeastOneTestFailedExitCode);

        Assert.AreEqual(
            """
            <!-- microsoft-testing-platform:github:project-section -->
            ## ❌ Test Run Summary — Tests (net8.0)

            | Total | Passed | Failed | Skipped | Flaky | Duration |
            |---:|---:|---:|---:|---:|---:|
            | 1 | 0 | 1 | 0 | 0 | 0ms |

            ### ❌ Failures (1)

            <details>
            <summary><code>Tests.Failure</code> — 0ms</summary>

            ```text
            expected failure
            ```

            **Attachments (paths inside the workflow artifact):**

            - `module/1/1-failure.log` (47 bytes)
            - `missing.log` — not staged; see manifest.

            </details>



            """.Replace("\r\n", "\n"),
            markdown.Replace("\r\n", "\n"));
    }

    private static TestNodeStateProperty CreateAttachmentState(string outcome) => outcome switch
    {
        "failed" => new FailedTestNodeStateProperty("failed attachment"),
        "error" => new ErrorTestNodeStateProperty(new InvalidOperationException("error attachment")),
        "timeout" => new TimeoutTestNodeStateProperty(new TimeoutException("timeout attachment")),
        "passed" => PassedTestNodeStateProperty.CachedInstance,
        "skipped" => SkippedTestNodeStateProperty.CachedInstance,
        "discovered" => DiscoveredTestNodeStateProperty.CachedInstance,
        _ => InProgressTestNodeStateProperty.CachedInstance,
    };

    private static TestNodeUpdateMessage CreateAttachmentUpdate(
        SummaryExecutionFixture fixture, string uid, TestNodeStateProperty state, string source, bool superseded = false, int attempt = 1, string? displayName = null)
        => new(fixture.Session.Object.SessionUid, new TestNode
        {
            Uid = uid,
            DisplayName = displayName ?? $"Tests.{uid}",
            Properties = new PropertyBag(state, new RetryAttemptProperty(attempt, superseded), new FileArtifactProperty(new FileInfo(source), "Failure diagnostics")),
        });

    private static string WriteAttachmentSource(SummaryExecutionFixture fixture, string name, byte[] bytes)
    {
        string sourceDirectory = Path.Combine(fixture.DirectoryPath, "source");
        Directory.CreateDirectory(sourceDirectory);
        string path = Path.Combine(sourceDirectory, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static JsonDocument ReadAttachmentManifest(SummaryExecutionFixture fixture)
        => JsonDocument.Parse(File.ReadAllText(Assert.ContainsSingle(
            Directory.GetFiles(fixture.AttachmentsDirectory, "attachments.json", SearchOption.AllDirectories))));
}
