// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Policy;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;

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
