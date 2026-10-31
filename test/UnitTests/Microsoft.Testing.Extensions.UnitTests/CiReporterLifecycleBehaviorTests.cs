// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.OutputDevice;

using Moq;

using GitHub = ghactions::Microsoft.Testing.Extensions.GitHubActionsReport;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class CiReporterLifecycleBehaviorTests
{
    [TestMethod]
    [DataRow(false, DisplayName = "AzureDevOps_FailedOpen")]
    [DataRow(true, DisplayName = "GitHubActions_FailedOpen")]
    public async Task Group_OpenFailureDoesNotInventClosingCommand(bool github)
    {
        using var fixture = new CiReporterFixture();
        ITestSessionLifetimeHandler reporter = Group(fixture, github);
        fixture.DisplayError = _ => new IOException("opening unavailable");

        await reporter.OnTestSessionStartingAsync(new CiReporterFixture.Session());
        fixture.DisplayError = null;
        await reporter.OnTestSessionFinishingAsync(new CiReporterFixture.Session());

        Assert.IsEmpty(fixture.Displays);
        Assert.HasCount(1, fixture.DisplayAttempts);
        Assert.Contains("Unexpected exception in OnTestSessionStartingAsync", fixture.LogText);
        Assert.Contains("opening unavailable", fixture.LogText);

        await reporter.OnTestSessionStartingAsync(new CiReporterFixture.Session());
        await reporter.OnTestSessionFinishingAsync(new CiReporterFixture.Session());
        Assert.HasCount(2, fixture.Displays);
        Assert.AreEqual(github ? "::endgroup::" : "##[endgroup]", Command(fixture.Displays[1]));
    }

    [TestMethod]
    [DataRow(false, DisplayName = "AzureDevOps_FailedClose")]
    [DataRow(true, DisplayName = "GitHubActions_FailedClose")]
    public async Task Group_CloseFailureCanBeRetriedWithoutDuplicateClose(bool github)
    {
        using var fixture = new CiReporterFixture();
        ITestSessionLifetimeHandler reporter = Group(fixture, github);
        await reporter.OnTestSessionStartingAsync(new CiReporterFixture.Session());
        fixture.DisplayError = _ => new IOException("closing unavailable");

        await reporter.OnTestSessionFinishingAsync(new CiReporterFixture.Session());
        fixture.DisplayError = null;
        await reporter.OnTestSessionFinishingAsync(new CiReporterFixture.Session());
        await reporter.OnTestSessionFinishingAsync(new CiReporterFixture.Session());

        Assert.HasCount(3, fixture.DisplayAttempts);
        Assert.HasCount(2, fixture.Displays);
        Assert.AreEqual(github ? "::endgroup::" : "##[endgroup]", Command(fixture.Displays[1]));
        Assert.Contains("Unexpected exception in OnTestSessionFinishingAsync", fixture.LogText);
        Assert.Contains("closing unavailable", fixture.LogText);
        Assert.IsTrue(fixture.DisplayAttempts.All(attempt => ReferenceEquals(reporter, attempt.Owner)));
    }

    [TestMethod]
    [DataRow(false, false, DisplayName = "AzureDevOps_CanceledStart")]
    [DataRow(false, true, DisplayName = "AzureDevOps_CanceledFinish")]
    [DataRow(true, false, DisplayName = "GitHubActions_CanceledStart")]
    [DataRow(true, true, DisplayName = "GitHubActions_CanceledFinish")]
    public async Task Group_CancellationPrecedesOutput(bool github, bool finishing)
    {
        using var fixture = new CiReporterFixture();
        ITestSessionLifetimeHandler reporter = Group(fixture, github);
        if (finishing)
        {
            await reporter.OnTestSessionStartingAsync(new CiReporterFixture.Session());
        }

        int before = fixture.DisplayAttempts.Count;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var session = new CiReporterFixture.Session(cancellation.Token);

        OperationCanceledException error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => finishing
            ? reporter.OnTestSessionFinishingAsync(session)
            : reporter.OnTestSessionStartingAsync(session));

        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        Assert.HasCount(before, fixture.DisplayAttempts);
        Assert.IsEmpty(fixture.Logs);
        if (finishing)
        {
            await reporter.OnTestSessionFinishingAsync(new CiReporterFixture.Session());
            Assert.AreEqual(github ? "::endgroup::" : "##[endgroup]", Command(fixture.Displays.Last()));
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "AzureDevOps_UnknownAssembly")]
    [DataRow(true, DisplayName = "GitHubActions_DisplayNameFallback")]
    public async Task Group_FallbackNameIsEscapedAndTraceExplainsEnablement(bool github)
    {
        using var fixture = new CiReporterFixture();
        fixture.Module.Setup(value => value.TryGetAssemblyName()).Returns((string?)null);
        fixture.Module.Setup(value => value.GetDisplayName()).Returns("Fallback%\r\nTests");
        ITestSessionLifetimeHandler reporter = Group(fixture, github);
        Assert.IsTrue(await reporter.IsEnabledAsync());
        await reporter.OnTestSessionStartingAsync(new CiReporterFixture.Session());
        await ((IDataConsumer)reporter).ConsumeAsync(null!, Mock.Of<IData>(), new CancellationToken(canceled: true));

        string tfm = ghactions::Microsoft.Testing.Extensions.TargetFrameworkMonikerHelper.GetTargetFrameworkMonikerIncludingPlatform();
        string expected = github
            ? $"::group::Tests: Fallback%25%0D%0ATests ({tfm})"
            : $"##[group]{string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.LogGroupHeader, $"unknown ({tfm})")}";
        Assert.AreEqual(expected, Command(Assert.ContainsSingle(fixture.Displays)));
        IOutputDeviceDataProducer owner = Assert.ContainsSingle(fixture.DisplayAttempts).Owner;
        Assert.AreSame<Microsoft.Testing.Platform.Extensions.IExtension>(reporter, owner);
        Assert.AreEqual(github ? "GitHubActionsReporter" : "AzureDevOpsLogGroupReporter", owner.Uid);
        Assert.IsNotEmpty(owner.Version);
        Assert.IsNotEmpty(owner.DisplayName);
        Assert.IsNotEmpty(owner.Description);
        if (github)
        {
            Assert.AreEqual("GitHubActionsReport groups is enabled.", Assert.ContainsSingle(fixture.Logs).Message);
            fixture.Variables["GITHUB_ACTIONS"] = null;
            Assert.IsFalse(await reporter.IsEnabledAsync());
            Assert.AreEqual("GitHubActionsReport groups is disabled.", fixture.Logs.Last().Message);
        }
    }

    [TestMethod]
    [DataRow(0, DisplayName = "Annotation_MissingQuarantine")]
    [DataRow(1, DisplayName = "Annotation_UnreadableQuarantine")]
    public async Task Annotation_ConfigurationFailureWarnsOnceAndKeepsFinalFailures(int failure)
    {
        using var fixture = new CiReporterFixture();
        fixture.EnableAzureDevOps();
        string path = Path.Combine(fixture.Root, "quarantine.txt");
        fixture.Options[AzureDevOpsCommandLineOptions.AzureDevOpsQuarantineFile] = [path];
        fixture.Options[AzureDevOpsCommandLineOptions.AzureDevOpsReportSeverity] = ["WaRnInG"];
        fixture.Options[AzureDevOpsCommandLineOptions.AzureDevOpsStackFrameFilter] = ["[", "^Generated\\."];
        if (failure == 1)
        {
            fixture.PutText(path, "unused");
            fixture.FileSystem.Setup(value => value.ReadAllText(path)).Throws(new IOException("read denied"));
        }

        AzureDevOpsReporter reporter = Annotation(fixture);
        Assert.IsTrue(await reporter.IsEnabledAsync());
        Assert.IsTrue(await reporter.IsEnabledAsync());
        foreach (TestNodeStateProperty state in FailureStates())
        {
            await reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Final", state), CancellationToken.None);
        }

        string expectedWarning = failure == 0
            ? string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.QuarantineFileMissingWarning, path)
            : string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.QuarantineFileLoadFailedWarning, path, "read denied");
        Assert.HasCount(1, fixture.Logs.Where(entry => entry.Message == expectedWarning));
        Assert.HasCount(1, fixture.Logs.Where(entry => entry.Message.StartsWith("Skipping invalid '--report-azdo-stackframe-filter'", StringComparison.Ordinal)));
        Assert.HasCount(4, fixture.Displays);
        string tfm = ghactions::Microsoft.Testing.Extensions.TargetFrameworkMonikerHelper.GetTargetFrameworkMonikerIncludingPlatform();
        Assert.AreSequenceEqual(
            Enumerable.Repeat($"##vso[task.logissue type=warning]Tests.Final [{tfm}]%0Afinal explanation", 4),
            fixture.Displays.Select(Command));
        Assert.IsTrue(fixture.DisplayAttempts.All(attempt => ReferenceEquals(reporter, attempt.Owner)));
    }

    [TestMethod]
    public async Task Annotation_UserFilterSelectsNextExistingSourceFrame()
    {
        using var fixture = new CiReporterFixture();
        fixture.EnableAzureDevOps();
        fixture.Options[AzureDevOpsCommandLineOptions.AzureDevOpsStackFrameFilter] = ["^Generated\\."];
        fixture.FileSystem.Setup(value => value.ExistFile(It.IsAny<string>())).Returns(true);
        var exception = new StackException(
            "   at Generated.Wrapper() in /_/src/Generated.cs:line 7\n   at User.Tests.Final() in /_/src/User.cs:line 29");
        AzureDevOpsReporter reporter = Annotation(fixture);

        await reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Filtered", new FailedTestNodeStateProperty(exception)), CancellationToken.None);

        string tfm = ghactions::Microsoft.Testing.Extensions.TargetFrameworkMonikerHelper.GetTargetFrameworkMonikerIncludingPlatform();
        Assert.AreEqual(
            $"##vso[task.logissue type=error;sourcepath=src/User.cs;linenumber=29;columnnumber=1]Tests.Filtered [{tfm}]%0Astack failure",
            Command(Assert.ContainsSingle(fixture.Displays)));
        Assert.Contains("Skipping stack frame 'Generated.Wrapper()'", fixture.LogText);
        Assert.DoesNotContain("sourcepath=src/Generated.cs", Command(fixture.Displays[0]));
    }

    [TestMethod]
    public async Task Annotation_CanceledOrIrrelevantInputDoesNotReadConfiguration()
    {
        using var fixture = new CiReporterFixture();
        AzureDevOpsReporter reporter = Annotation(fixture);
        await reporter.ConsumeAsync(null!, Mock.Of<IData>(), CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        OperationCanceledException error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            reporter.ConsumeAsync(null!, CiReporterFixture.Update("Tests.Canceled", new FailedTestNodeStateProperty()), cancellation.Token));

        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        Assert.IsEmpty(fixture.Displays);
        Assert.IsEmpty(fixture.Logs);
        fixture.FileSystem.VerifyNoOtherCalls();
        Assert.IsFalse(await reporter.IsEnabledAsync());
        Assert.AreEqual("AzureDevOpsReport is disabled.", Assert.ContainsSingle(fixture.Logs).Message);
    }

    [TestMethod]
    public void Quarantine_ParsingBoundsDoNotDropValidNeighbors()
    {
        using var fixture = new CiReporterFixture();
        string path = Path.Combine(fixture.Root, "patterns.txt");
        string content = "# ignored\r\n \n" + new string('x', 4097) + "\n" + new string('b', 4096) + "\n"
            + string.Join("\n", Enumerable.Range(0, 10001).Select(index => $"Tests.Item{index}"));
        fixture.PutText(path, content);

        var quarantine = new QuarantineFile(path, fixture.FileSystem.Object, fixture.Logger.Object);

        Assert.IsTrue(quarantine.Matches("Tests.Item0"));
        Assert.IsTrue(quarantine.Matches(new string('b', 4096)));
        Assert.IsTrue(quarantine.Matches("Tests.Item9998"));
        Assert.IsFalse(quarantine.Matches("Tests.Item9999"));
        Assert.IsFalse(quarantine.Matches("Tests.Item10000"));
        Assert.IsFalse(quarantine.Matches(new string('x', 4097)));
        Assert.AreSequenceEqual(
        [
            string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.QuarantinePatternTooLongWarning, 4096),
            string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.QuarantinePatternsCappedWarning, 10000),
        ], fixture.Logs.Select(entry => entry.Message));
    }

    [TestMethod]
    public void Quarantine_GlobsRemainAnchoredLiteralAndCaseSensitive()
    {
        var quarantine = new QuarantineFile(["Tests.Literal[1]", "Tests.One?", "Tests.Prefix*"]);

        Assert.IsFalse(quarantine.Matches(null!));
        Assert.IsFalse(quarantine.Matches(" \t "));
        Assert.IsTrue(quarantine.Matches("  Tests.Literal[1]  "));
        Assert.IsFalse(quarantine.Matches("Tests.Literal1"));
        Assert.IsTrue(quarantine.Matches("Tests.OneX"));
        Assert.IsFalse(quarantine.Matches("Tests.OneXY"));
        Assert.IsTrue(quarantine.Matches("Tests.Prefix"));
        Assert.IsTrue(quarantine.Matches("Tests.Prefix.More"));
        Assert.IsFalse(quarantine.Matches("XTests.Prefix.More"));
        Assert.IsFalse(quarantine.Matches("tests.Prefix"));
    }

    private static ITestSessionLifetimeHandler Group(CiReporterFixture fixture, bool github)
    {
        fixture.EnableAzureDevOps();
        fixture.Options[AzureDevOpsCommandLineOptions.AzureDevOpsGroups] = ["on"];
        fixture.Variables["GITHUB_ACTIONS"] = "true";
        fixture.Options[GitHub.GitHubActionsCommandLineOptions.GitHubActionsOptionName] = [];
        return github
            ? new GitHub.GitHubActionsReporter(new TestCommandLineOptions(fixture.Options), fixture.Environment.Object, fixture.Output.Object, fixture.Module.Object, fixture.LoggerFactory.Object)
            : new AzureDevOpsLogGroupReporter(new TestCommandLineOptions(fixture.Options), fixture.Environment.Object, fixture.Output.Object, fixture.Module.Object, fixture.LoggerFactory.Object);
    }

    private static AzureDevOpsReporter Annotation(CiReporterFixture fixture)
        => new(new TestCommandLineOptions(fixture.Options), fixture.Environment.Object, fixture.FileSystem.Object, fixture.Output.Object, fixture.LoggerFactory.Object, Mock.Of<IAzureDevOpsHistoryService>());

    private static string Command(IOutputDeviceData data)
        => data switch
        {
            AzureDevOpsCommandOutputDeviceData azure => azure.Text,
            GitHubActionsCommandOutputDeviceData github => github.Text,
            _ => throw new AssertFailedException("Expected a workflow command."),
        };

    private static TestNodeStateProperty[] FailureStates()
        =>
        [
            new FailedTestNodeStateProperty("final explanation"),
            new ErrorTestNodeStateProperty("final explanation"),
            new TimeoutTestNodeStateProperty("final explanation"),
#pragma warning disable CS0618, MTP0001
            new CancelledTestNodeStateProperty("final explanation"),
#pragma warning restore CS0618, MTP0001
        ];

    private sealed class StackException(string stack) : Exception("stack failure")
    {
        public override string StackTrace => stack;
    }
}
