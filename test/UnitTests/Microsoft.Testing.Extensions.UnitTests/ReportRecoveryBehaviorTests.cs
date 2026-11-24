// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ctrf;

using System.Text.Json;

using Microsoft.Testing.Extensions.CtrfReport;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHostControllers;

using Moq;

using JournalConfiguration = ctrf::Microsoft.Testing.Extensions.ReportJournalConfiguration;
using JournalEnvironmentProvider = ctrf::Microsoft.Testing.Extensions.ReportJournalEnvironmentVariableProvider;
using RecoveryHandler = ctrf::Microsoft.Testing.Extensions.ReportProcessLifetimeHandler<Microsoft.Testing.Extensions.CtrfReport.CtrfReportGenerator, Microsoft.Testing.Extensions.CtrfReport.CapturedTestResult>;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class ReportRecoveryBehaviorTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task EnvironmentProvider_PreservesPathAndValidatesChildContract(bool enabled)
    {
        var fixture = new ReportBehaviorFixture(enabled ? new() { ["report-ctrf"] = [] } : []);
        ICommandLineOptions options = fixture.Services.GetCommandLineOptions();
        var journal = new JournalConfiguration("TEST_JOURNAL");
        var provider = new JournalEnvironmentProvider(options, fixture.Configuration.Object, fixture.FileSystem.Object, "report-ctrf", journal);
        var variables = new Mock<IEnvironmentVariables>();
        var assigned = new List<EnvironmentVariable>();
        variables.Setup(value => value.SetVariable(It.IsAny<EnvironmentVariable>())).Callback<EnvironmentVariable>(assigned.Add);

        Assert.AreEqual(enabled, await provider.IsEnabledAsync());
        await provider.UpdateAsync(variables.Object);
        await provider.UpdateAsync(variables.Object);

        Assert.HasCount(2, assigned);
        Assert.AreEqual(assigned[0].Value, assigned[1].Value);
        Assert.AreEqual(Path.GetFullPath("results"), Path.GetDirectoryName(assigned[0].Value));
        Assert.AreEqual("TEST_JOURNAL", assigned[0].Variable);
        Assert.IsFalse(assigned[0].IsSecret);
        Assert.IsTrue(assigned[0].IsLocked);
        Assert.AreEqual("ReportJournalEnvironmentVariableProvider.TEST_JOURNAL", provider.Uid);
        Assert.AreEqual(provider.Uid, provider.DisplayName);
        Assert.AreEqual(provider.Uid, provider.Description);
        Assert.IsFalse(string.IsNullOrEmpty(provider.Version));
        fixture.FileSystem.Verify(value => value.CreateDirectory("results"), Times.Once);
        string? path = assigned[0].Value;
        OwnedEnvironmentVariable? correct = new(provider, "TEST_JOURNAL", path, false, true);
        variables.Setup(value => value.TryGetVariable("TEST_JOURNAL", out correct)).Returns(true);
        Assert.IsTrue((await provider.ValidateTestHostEnvironmentVariablesAsync(variables.Object)).IsValid);
        OwnedEnvironmentVariable? wrong = new(provider, "TEST_JOURNAL", "wrong", false, true);
        variables.Setup(value => value.TryGetVariable("TEST_JOURNAL", out wrong)).Returns(true);
        Assert.AreEqual(
            "The report recovery environment variable 'TEST_JOURNAL' is missing or invalid.",
            (await provider.ValidateTestHostEnvironmentVariablesAsync(variables.Object)).ErrorMessage);
        OwnedEnvironmentVariable? missing = null;
        variables.Setup(value => value.TryGetVariable("TEST_JOURNAL", out missing)).Returns(false);
        Assert.AreEqual(
            "The report recovery environment variable 'TEST_JOURNAL' is missing or invalid.",
            (await provider.ValidateTestHostEnvironmentVariablesAsync(variables.Object)).ErrorMessage);
    }

    [TestMethod]
    public async Task Exit_MissingJournal_DoesNotRecoverOrPublish()
    {
        var fixture = new ReportBehaviorFixture(new() { ["report-ctrf"] = [] });
        using RecoveryHandler handler = CreateHandler(fixture, out _);
        Assert.IsTrue(await handler.IsEnabledAsync());
        await handler.BeforeTestHostProcessStartAsync(CancellationToken.None);
        await handler.OnTestHostProcessStartedAsync(new TestHostProcessInformation(314, 1, false), CancellationToken.None);

        await handler.OnTestHostProcessExitedAsync(new TestHostProcessInformation(314, 1, false), CancellationToken.None);

        Assert.IsEmpty(fixture.Artifacts);
        Assert.IsEmpty(fixture.Warnings);
        Assert.IsEmpty(fixture.WrittenFiles);
    }

    [TestMethod]
    [DataRow("null", true)]
    [DataRow("{invalid", true)]
    [DataRow("{\"Type\":2,\"ReportFileName\":\"missing.json\"}", false)]
    public async Task Exit_UnavailableOrCorruptTail_RegeneratesBoundedIncompleteReport(string tail, bool partial)
    {
        var fixture = new ReportBehaviorFixture(new()
        {
            ["report-ctrf"] = [],
            ["report-ctrf-filename"] = ["recovered.json"],
        });
        fixture.Environment.Setup(value => value.GetEnvironmentVariable("TESTINGPLATFORM_RETRY_RECOVERED_ARTIFACT_MANIFEST"))
            .Returns(Path.Combine("retry", "artifacts.jsonl"));
        using RecoveryHandler handler = CreateHandler(fixture, out string journalPath);
        fixture.InputFiles[journalPath] = """
            {"Type":0,"StartTime":"2026-08-31T12:00:00+00:00","ProcessId":42,"FrameworkUid":"original-fw","FrameworkVersion":"3.4.5","FrameworkDisplayName":"Original Framework"}
            {"Type":1,"Result":{"Uid":"first","DisplayName":"Recovered test","Status":"passed","Duration":"00:00:02"}}
            """ + "\n" + tail;

        await handler.OnTestHostProcessExitedAsync(new TestHostProcessInformation(314, 17, false), CancellationToken.None);

        FileArtifact artifact = Assert.IsInstanceOfType<FileArtifact>(Assert.ContainsSingle(fixture.Artifacts));
        Assert.AreEqual(Path.GetFullPath(Path.Combine("results", "recovered.json")), artifact.FileInfo.FullName);
        Assert.AreEqual("microsoft.testing.ctrf", artifact.Kind);
        using var report = JsonDocument.Parse(fixture.WrittenFiles[Path.Combine("results", "recovered.json")].Text);
        JsonElement results = report.RootElement.GetProperty("results");
        Assert.AreEqual("Original Framework", results.GetProperty("tool").GetProperty("name").GetString());
        Assert.AreEqual("3.4.5", results.GetProperty("tool").GetProperty("version").GetString());
        Assert.AreEqual(1, results.GetProperty("summary").GetProperty("tests").GetInt32());
        Assert.AreEqual("first", Assert.ContainsSingle(results.GetProperty("tests").EnumerateArray()).GetProperty("testId").GetString());
        Assert.AreEqual(2000L, Assert.ContainsSingle(results.GetProperty("tests").EnumerateArray()).GetProperty("duration").GetInt64());
        Assert.IsTrue(results.GetProperty("environment").GetProperty("extra").GetProperty("incomplete").GetBoolean());
        string expectedFirst = partial
            ? "The test host terminated before report generation completed. Recovered 1 terminal test result(s); the generated report is marked incomplete."
            : "The test host terminated before report artifact delivery was confirmed, but the completed report file was unavailable. Re-generated it from 1 journaled terminal test result(s) and marked it incomplete.";
        string[] expectedWarnings = partial
            ? [expectedFirst, "Report recovery stopped before the end of the journal because it was truncated, corrupt, or exceeded a safety limit. The recovered report contains only the valid bounded prefix."]
            : [expectedFirst];
        Assert.AreSequenceEqual(expectedWarnings, fixture.Warnings);
        fixture.FileSystem.Verify(value => value.DeleteFile(journalPath), Times.Once);
        string manifest = fixture.WrittenFiles[Path.Combine("retry", "artifacts.jsonl")].Text;
        string expectedManifest = Convert.ToBase64String(Encoding.UTF8.GetBytes(Path.Combine("results", "recovered.json")))
            + "\t" + Convert.ToBase64String(Encoding.UTF8.GetBytes("microsoft.testing.ctrf")) + Environment.NewLine;
        Assert.AreEqual(expectedManifest, manifest);
    }

    [TestMethod]
    public async Task Exit_ReadFailure_ReportsExactDiagnosticAndDeletesJournal()
    {
        var fixture = new ReportBehaviorFixture(new() { ["report-ctrf"] = [] });
        using RecoveryHandler handler = CreateHandler(fixture, out string path);
        fixture.InputFiles[path] = string.Empty;
        fixture.FileSystem.Setup(value => value.NewFileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            .Throws(new IOException("read denied"));

        await handler.OnTestHostProcessExitedAsync(new TestHostProcessInformation(314, 1, false), CancellationToken.None);

        Assert.AreSequenceEqual(
            ["The test host terminated before report generation completed, but the partial report could not be recovered: read denied"],
            fixture.Warnings);
        Assert.IsEmpty(fixture.Artifacts);
        fixture.FileSystem.Verify(value => value.DeleteFile(path), Times.Once);
    }

    [TestMethod]
    public async Task Exit_ManifestFailure_DoesNotLosePublishedCompletedArtifact()
    {
        var fixture = new ReportBehaviorFixture(new() { ["report-ctrf"] = [] });
        using RecoveryHandler handler = CreateHandler(fixture, out string path);
        fixture.InputFiles[path] = "{\"Type\":2,\"ReportFileName\":\"completed.json\"}";
        fixture.ExistingPaths.Add("completed.json");
        fixture.Environment.Setup(value => value.GetEnvironmentVariable("TESTINGPLATFORM_RETRY_RECOVERED_ARTIFACT_MANIFEST")).Returns("manifest.jsonl");
        fixture.FileSystem.Setup(value => value.NewFileStream("manifest.jsonl", FileMode.Append, FileAccess.Write, FileShare.Read))
            .Throws(new IOException("manifest denied"));

        await handler.OnTestHostProcessExitedAsync(new TestHostProcessInformation(314, 1, false), CancellationToken.None);

        Assert.AreEqual("completed.json", Assert.IsInstanceOfType<FileArtifact>(Assert.ContainsSingle(fixture.Artifacts)).FileInfo.Name);
        Assert.AreSequenceEqual(
            ["The test host terminated before report artifact delivery was confirmed. Re-published the completed report 'completed.json'."],
            fixture.Warnings);
        fixture.Logger.Verify(
            value => value.LogAsync(
                LogLevel.Warning,
                "Failed to register recovered report 'completed.json' with retry artifact consolidation: manifest denied",
                null, It.IsAny<Func<string, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task RecoveredFramework_RejectsExecutionRatherThanInventingSessions()
    {
        var framework = new ctrf::Microsoft.Testing.Extensions.RecoveredTestFramework(new()
        {
            FrameworkUid = "recovered",
            FrameworkVersion = "1.0",
            FrameworkDisplayName = "Recovered",
        });

        Assert.IsTrue(await framework.IsEnabledAsync());
        _ = await Assert.ThrowsExactlyAsync<NotSupportedException>(() => framework.CreateTestSessionAsync(null!));
        _ = await Assert.ThrowsExactlyAsync<NotSupportedException>(() => framework.CloseTestSessionAsync(null!));
        _ = await Assert.ThrowsExactlyAsync<NotSupportedException>(() => framework.ExecuteRequestAsync(null!));
        Assert.AreEqual("recovered", framework.Uid);
        Assert.AreEqual("1.0", framework.Version);
        Assert.AreEqual("Recovered", framework.Description);
    }

    private static RecoveryHandler CreateHandler(ReportBehaviorFixture fixture, out string path)
    {
        var journal = new JournalConfiguration("TEST_JOURNAL");
        path = journal.GetOrCreatePath(fixture.Configuration.Object, fixture.FileSystem.Object);
        return new RecoveryHandler(fixture.Services, "report-ctrf", journal,
            (services, metadata) => new CtrfReportGenerator(services, metadata),
            CtrfReportGenerator.DeserializeJournalRecord);
    }
}
