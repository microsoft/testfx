// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

using Microsoft.Testing.Extensions.CtrfReport;
using Microsoft.Testing.Extensions.HtmlReport;
using Microsoft.Testing.Extensions.JUnitReport;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class ReportLifecycleBehaviorTests
{
    [TestMethod]
    [DataRow("html")]
    [DataRow("junit")]
    [DataRow("ctrf")]
    public async Task FullLifecycle_GeneratesReportAndOrderedRecoveryJournal(string format)
    {
        string option = "report-" + format;
        var fixture = new ReportBehaviorFixture(new()
        {
            [option] = [],
            [option + "-filename"] = ["report." + (format == "html" ? "html" : format == "ctrf" ? "json" : "xml")],
            [PlatformCommandLineProvider.TestHostControllerPIDOptionKey] = ["1"],
        });
        const string journalPath = "journal.jsonl";
        fixture.Environment.Setup(value => value.GetEnvironmentVariable(
            "TESTINGPLATFORM_" + format.ToUpperInvariant() + "REPORT_JOURNAL")).Returns(journalPath);
        string reportPath = Path.Combine("results", "report." + (format == "html" ? "html" : format == "ctrf" ? "json" : "xml"));
        fixture.ExistingPaths.Add(reportPath);
        object instance = Create(format, fixture);
        using var disposable = (IDisposable)instance;
        var generator = (IDataConsumer)instance;
        var lifetime = (ITestSessionLifetimeHandler)instance;
        var session = new ReportBehaviorFixture.Session();

        Assert.IsTrue(await generator.IsEnabledAsync());
        await lifetime.OnTestSessionStartingAsync(session);
        await generator.ConsumeAsync(null!, Node("pass", PassedTestNodeStateProperty.CachedInstance), CancellationToken.None);
        await generator.ConsumeAsync(null!, Node("fail", new FailedTestNodeStateProperty("expected 1, got 2")), CancellationToken.None);
        await generator.ConsumeAsync(null!, Moq.Mock.Of<IData>(), CancellationToken.None);
        await fixture.FinishAsync(lifetime, session);

        SessionFileArtifact artifact = Assert.IsInstanceOfType<SessionFileArtifact>(Assert.ContainsSingle(fixture.Artifacts));
        Assert.AreEqual("report-session", artifact.SessionUid.Value);
        Assert.AreEqual("microsoft.testing." + format, artifact.Kind);
        Assert.AreEqual(Path.GetFullPath(reportPath), artifact.FileInfo.FullName);
        Assert.AreSequenceEqual(["warning", "artifact"], fixture.Events);
        string expectedWarning = format switch
        {
            "html" => string.Format(CultureInfo.InvariantCulture, HtmlReport.Resources.ExtensionResources.HtmlReportFileExistsAndWillBeOverwritten, reportPath),
            "junit" => string.Format(CultureInfo.InvariantCulture, JUnitReport.Resources.ExtensionResources.JUnitReportFileExistsAndWillBeOverwritten, reportPath),
            _ => string.Format(CultureInfo.InvariantCulture, CtrfReport.Resources.ExtensionResources.CtrfReportFileExistsAndWillBeOverwritten, reportPath),
        };
        Assert.AreSequenceEqual([expectedWarning], fixture.Warnings);
        string[] records = fixture.WrittenFiles[journalPath].Text.Split(['\n'], StringSplitOptions.RemoveEmptyEntries);
        Assert.HasCount(4, records);
        using var header = JsonDocument.Parse(records[0]);
        Assert.AreEqual(0, header.RootElement.GetProperty("Type").GetInt32());
        Assert.AreEqual(42, header.RootElement.GetProperty("ProcessId").GetInt32());
        Assert.AreEqual("fw", header.RootElement.GetProperty("FrameworkUid").GetString());
        Assert.AreEqual("1.2.3", header.RootElement.GetProperty("FrameworkVersion").GetString());
        Assert.AreEqual("Test Framework", header.RootElement.GetProperty("FrameworkDisplayName").GetString());
        Assert.AreEqual(ReportBehaviorFixture.Start, header.RootElement.GetProperty("StartTime").GetDateTimeOffset());
        for (int index = 1; index <= 2; index++)
        {
            using var record = JsonDocument.Parse(records[index]);
            Assert.AreEqual(1, record.RootElement.GetProperty("Type").GetInt32());
            Assert.AreEqual(index == 1 ? "pass" : "fail", record.RootElement.GetProperty("Result").GetProperty("Uid").GetString());
        }

        using var completion = JsonDocument.Parse(records[3]);
        Assert.AreEqual(2, completion.RootElement.GetProperty("Type").GetInt32());
        Assert.AreEqual(Path.GetFullPath(reportPath), completion.RootElement.GetProperty("ReportFileName").GetString());
        Assert.AreEqual(0, completion.RootElement.GetProperty("DroppedJournalRecords").GetInt32());
        Assert.IsTrue(fixture.WrittenFiles[journalPath].IsDisposed);

        string report = fixture.WrittenFiles[reportPath].Text;
        if (format == "junit")
        {
            var document = XDocument.Parse(report);
            Assert.AreSequenceEqual(["pass", "fail"], document.Descendants("testcase").Select(value => value.Attribute("name")!.Value));
            Assert.AreEqual("2", document.Root!.Attribute("tests")!.Value);
            Assert.AreEqual("1", document.Root.Attribute("failures")!.Value);
            Assert.AreEqual("expected 1, got 2", document.Descendants("failure").Single().Value);
        }
        else
        {
            using var document = JsonDocument.Parse(format == "html" ? HtmlReportEngine.ExtractReportJson(report) : report);
            JsonElement body = format == "html" ? document.RootElement : document.RootElement.GetProperty("results");
            Assert.AreEqual(2, body.GetProperty("summary").GetProperty(format == "html" ? "total" : "tests").GetInt32());
            Assert.AreSequenceEqual(
                ["pass", "fail"],
                body.GetProperty("tests").EnumerateArray().Select(value => value.GetProperty(format == "html" ? "uid" : "testId").GetString()));
        }
    }

    [TestMethod]
    [DataRow("html")]
    [DataRow("junit")]
    [DataRow("ctrf")]
    public async Task CanceledCallbacks_DoNotPublishOrWrite(string format)
    {
        var fixture = new ReportBehaviorFixture(new() { ["report-" + format] = [] });
        object instance = Create(format, fixture);
        using var disposable = (IDisposable)instance;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var session = new ReportBehaviorFixture.Session(cancellation.Token);

        OperationCanceledException start = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => ((ITestSessionLifetimeHandler)instance).OnTestSessionStartingAsync(session));
        OperationCanceledException consume = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => ((IDataConsumer)instance).ConsumeAsync(null!, Node("test", PassedTestNodeStateProperty.CachedInstance), cancellation.Token));
        OperationCanceledException finish = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => ((ITestSessionLifetimeHandler)instance).OnTestSessionFinishingAsync(session));

        Assert.AreEqual(cancellation.Token, start.CancellationToken);
        Assert.AreEqual(cancellation.Token, consume.CancellationToken);
        Assert.AreEqual(cancellation.Token, finish.CancellationToken);
        Assert.IsEmpty(fixture.Artifacts);
        Assert.IsEmpty(fixture.WrittenFiles);
    }

    private static object Create(string format, ReportBehaviorFixture fixture)
        => format switch
        {
            "html" => new HtmlReportGenerator(fixture.Services),
            "junit" => new JUnitReportGenerator(fixture.Services),
            _ => new CtrfReportGenerator(fixture.Services),
        };

    private static TestNodeUpdateMessage Node(string uid, TestNodeStateProperty state)
        => new(new SessionUid("source"), new TestNode { Uid = uid, DisplayName = uid, Properties = new(state) });
}
