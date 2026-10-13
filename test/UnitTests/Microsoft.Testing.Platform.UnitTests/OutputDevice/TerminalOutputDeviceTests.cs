// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.OutputDevice.Terminal;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHostControllers;
using Microsoft.Testing.Platform.UnitTests.Helpers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.OutputDevice;

[TestClass]
[ResourceLock(WellKnownResources.Console)]
[UnsupportedOSPlatform("browser")]
public sealed class TerminalOutputDeviceTests
{
    private static readonly IOutputDeviceDataProducer Producer = Mock.Of<IOutputDeviceDataProducer>(
        producer => producer.Uid == "producer");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ActualDisplay_AllMessageKindsPreservePayloadAndOptionalAzureAnnotations(bool azure)
    {
        using RecordingDeviceFixture fixture = new(azure);
        await fixture.Device.InitializeAsync();
        InvalidOperationException exception = new("exception sentinel");

        await fixture.Device.DisplayAsync(Producer, new SessionMessageOutputDeviceData("session sentinel"), TestContext.CancellationToken);
        await fixture.Device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("operation", "progress sentinel"), TestContext.CancellationToken);
        await fixture.Device.DisplayAsync(Producer, new FormattedTextOutputDeviceData("formatted sentinel") { Padding = 4 }, TestContext.CancellationToken);
        await fixture.Device.DisplayAsync(Producer, new TextOutputDeviceData("plain sentinel"), TestContext.CancellationToken);
        await fixture.Device.DisplayAsync(Producer, new WarningMessageOutputDeviceData("warning sentinel"), TestContext.CancellationToken);
        await fixture.Device.DisplayAsync(Producer, new ErrorMessageOutputDeviceData("error sentinel"), TestContext.CancellationToken);
        await fixture.Device.DisplayAsync(Producer, new ExceptionOutputDeviceData(exception), TestContext.CancellationToken);

        string output = fixture.Output.ToString();
        Assert.Contains("session sentinel", output);
        Assert.Contains("    formatted sentinel", output);
        Assert.Contains("plain sentinel", output);
        Assert.Contains("warning sentinel", output);
        Assert.Contains("error sentinel", output);
        Assert.Contains("exception sentinel", output);
        Assert.AreEqual(azure ? 3 : 0, CountOccurrences(output, "##vso[task.logissue"));
        if (azure)
        {
            Assert.Contains("type=warning", output);
            Assert.AreEqual(2, CountOccurrences(output, "type=error"));
        }

        Assert.DoesNotContain("\u001B[", output, "Explicit ANSI off must avoid emitting terminal escape sequences.");
    }

    [TestMethod]
    [DataRow("passed", 0, 1, 0)]
    [DataRow("failed", 1, 0, 0)]
    [DataRow("error", 1, 0, 0)]
    [DataRow("timeout", 1, 0, 0)]
    [DataRow("cancelled", 1, 0, 0)]
    [DataRow("skipped", 0, 0, 1)]
    public async Task ActualConsume_AllTerminalOutcomePartitionsReachActualReporterAndSummary(string outcome, int failures, int passed, int skipped)
    {
        using RecordingDeviceFixture fixture = new();
        await fixture.Device.InitializeAsync();
        await fixture.Device.HandleProcessRoleAsync(TestProcessRole.TestHost, TestContext.CancellationToken);
        await fixture.Device.DisplayBeforeSessionStartAsync(TestContext.CancellationToken);
        InvalidOperationException exception = new("terminal failure sentinel");
#pragma warning disable CS0618, MTP0001 // Verify retained canceled-state compatibility output.
        TestNodeStateProperty state = outcome switch
        {
            "passed" => PassedTestNodeStateProperty.CachedInstance,
            "failed" => new FailedTestNodeStateProperty(exception, "explanation sentinel"),
            "error" => new ErrorTestNodeStateProperty(exception, "explanation sentinel"),
            "timeout" => new TimeoutTestNodeStateProperty(exception, "explanation sentinel"),
            "cancelled" => new CancelledTestNodeStateProperty(exception, "explanation sentinel"),
            _ => new SkippedTestNodeStateProperty("skip sentinel"),
        };
#pragma warning restore CS0618, MTP0001
        TestNodeUpdateMessage update = new(default, new TestNode
        {
            Uid = "id",
            DisplayName = "observed terminal test",
            Properties = new PropertyBag(state),
        });
        update.TestNode.Properties.Add(new StandardOutputProperty("stdout sentinel"));
        update.TestNode.Properties.Add(new StandardErrorProperty("stderr sentinel"));
        DateTimeOffset start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        update.TestNode.Properties.Add(new TimingProperty(new TimingInfo(start, start.AddMilliseconds(10), TimeSpan.FromMilliseconds(10))));

        await fixture.Device.ConsumeAsync(Mock.Of<IDataProducer>(), update, TestContext.CancellationToken);
        await fixture.Device.DisplayAfterSessionEndRunAsync(TestContext.CancellationToken);

        string output = fixture.Output.ToString();
        Assert.Contains("observed terminal test", output);
        Assert.Contains("stdout sentinel", output);
        Assert.Contains("stderr sentinel", output);
        Assert.Contains("total: 1", output);
        Assert.Contains("failed: " + failures, output);
        Assert.Contains("succeeded: " + passed, output);
        Assert.Contains("skipped: " + skipped, output);
        if (failures > 0)
        {
            Assert.Contains("explanation sentinel", output);
            Assert.DoesNotContain("terminal failure sentinel", output, "Explicit explanation owns the top-level error text.");
            Assert.Contains("Failed!", output);
        }
        else
        {
            Assert.Contains("Passed!", output);
            Assert.DoesNotContain("terminal failure sentinel", output);
        }
    }

    [TestMethod]
    public async Task ActualConsume_AbsentExplanationUsesExceptionMessageAndPreservesInnerChain()
    {
        using RecordingDeviceFixture fixture = new();
        await fixture.Device.InitializeAsync();
        await fixture.Device.HandleProcessRoleAsync(TestProcessRole.TestHost, TestContext.CancellationToken);
        await fixture.Device.DisplayBeforeSessionStartAsync(TestContext.CancellationToken);
        InvalidOperationException exception = new("outer sentinel", new ArgumentException("inner sentinel"));
        TestNodeUpdateMessage update = new(default, new TestNode
        {
            Uid = "exception",
            DisplayName = "exception-only",
            Properties = new PropertyBag(new FailedTestNodeStateProperty(exception)),
        });

        await fixture.Device.ConsumeAsync(Mock.Of<IDataProducer>(), update, TestContext.CancellationToken);
        await fixture.Device.DisplayAfterSessionEndRunAsync(TestContext.CancellationToken);

        string output = fixture.Output.ToString();
        Assert.Contains("outer sentinel", output);
        Assert.Contains("inner sentinel", output);
        Assert.Contains("total: 1", output);
        Assert.Contains("failed: 1", output);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public async Task ActualConsume_AssertionPropertyOwnsBothDiffHalvesAndDuplicatePropertiesSuppressLegacyFallback(int propertyCount)
    {
        using RecordingDeviceFixture fixture = new();
        await fixture.Device.InitializeAsync();
        await fixture.Device.HandleProcessRoleAsync(TestProcessRole.TestHost, TestContext.CancellationToken);
        await fixture.Device.DisplayBeforeSessionStartAsync(TestContext.CancellationToken);
        InvalidOperationException exception = new("assert failure");
        exception.Data["assert.expected"] = "LEGACY_EXPECTED";
        exception.Data["assert.actual"] = "LEGACY_ACTUAL";
        TestNodeUpdateMessage update = new(default, new TestNode
        {
            Uid = "assertion",
            DisplayName = "assertion test",
            Properties = new PropertyBag(new FailedTestNodeStateProperty(exception)),
        });
        if (propertyCount > 0)
        {
            update.TestNode.Properties.Add(new AssertionFailureProperty("PROPERTY_EXPECTED", "PROPERTY_ACTUAL"));
        }

        if (propertyCount > 1)
        {
            update.TestNode.Properties.Add(new AssertionFailureProperty("SECOND_EXPECTED", "SECOND_ACTUAL"));
        }

        await fixture.Device.ConsumeAsync(Mock.Of<IDataProducer>(), update, TestContext.CancellationToken);

        string output = fixture.Output.ToString();
        if (propertyCount == 0)
        {
            Assert.Contains("LEGACY_EXPECTED", output);
            Assert.Contains("LEGACY_ACTUAL", output);
        }
        else if (propertyCount == 1)
        {
            Assert.Contains("PROPERTY_EXPECTED", output);
            Assert.Contains("PROPERTY_ACTUAL", output);
        }
        else
        {
            Assert.DoesNotContain("PROPERTY_EXPECTED", output);
            Assert.DoesNotContain("PROPERTY_ACTUAL", output);
            Assert.DoesNotContain("SECOND_EXPECTED", output);
            Assert.DoesNotContain("SECOND_ACTUAL", output);
        }

        if (propertyCount > 0)
        {
            Assert.DoesNotContain("LEGACY_EXPECTED", output);
            Assert.DoesNotContain("LEGACY_ACTUAL", output);
        }

        Assert.Contains("assertion test", output);
        Assert.Contains("assert failure", output);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ActualConsume_ServerAndJsonDiscoveryModesCannotRenderTerminalResults(bool server)
    {
        var options = new Dictionary<string, string[]>
        {
            [PlatformCommandLineProvider.DiscoverTestsOptionKey] = [PlatformCommandLineProvider.DiscoverTestsJsonArgument],
        };
        if (server)
        {
            options[PlatformCommandLineProvider.ServerOptionKey] = ["dotnettestcli"];
        }

        using RecordingDeviceFixture fixture = new(extraOptions: options);
        await fixture.Device.InitializeAsync();
        await fixture.Device.HandleProcessRoleAsync(TestProcessRole.TestHost, TestContext.CancellationToken);
        await fixture.Device.DisplayBeforeSessionStartAsync(TestContext.CancellationToken);
        TestNodeUpdateMessage discovery = new(default, new TestNode
        {
            Uid = "discovered",
            DisplayName = "discovery sentinel",
            Properties = new PropertyBag(DiscoveredTestNodeStateProperty.CachedInstance),
        });
        TestNodeUpdateMessage failure = new(default, new TestNode
        {
            Uid = "failed",
            DisplayName = "failure must not appear",
            Properties = new PropertyBag(new FailedTestNodeStateProperty("must not render")),
        });
        await fixture.Device.ConsumeAsync(Mock.Of<IDataProducer>(), discovery, TestContext.CancellationToken);
        await fixture.Device.ConsumeAsync(Mock.Of<IDataProducer>(), failure, TestContext.CancellationToken);
        Assert.AreEqual(string.Empty, fixture.Output.ToString());

        await fixture.Device.DisplayAfterSessionEndRunAsync(TestContext.CancellationToken);

        string output = fixture.Output.ToString();
        if (server)
        {
            Assert.AreEqual(string.Empty, output, "Server mode delegates even discovery JSON rendering to the SDK.");
        }
        else
        {
            Assert.Contains("discovery sentinel", output);
            Assert.Contains("discovered", output);
            Assert.DoesNotContain("failure must not appear", output);
            Assert.DoesNotContain("must not render", output);
            Assert.DoesNotContain("Test run summary", output);
        }
    }

    [TestMethod]
    public async Task ActualConsume_EmptyCompletionDoesNotInventPassAndArtifactsKeepActualPaths()
    {
        using RecordingDeviceFixture fixture = new();
        await fixture.Device.InitializeAsync();
        await fixture.Device.HandleProcessRoleAsync(TestProcessRole.TestHost, TestContext.CancellationToken);
        await fixture.Device.DisplayBeforeSessionStartAsync(TestContext.CancellationToken);
        string nodeArtifact = Path.Combine(Path.GetTempPath(), "node-not-opened.trx");
        string sessionArtifact = Path.Combine(Path.GetTempPath(), "session-not-opened.trx");
        string runArtifact = Path.Combine(Path.GetTempPath(), "run-not-opened.trx");
        TestNodeUpdateMessage update = new(default, new TestNode
        {
            Uid = "empty",
            DisplayName = "empty result",
            Properties = new PropertyBag(TestNodeExecutionCompletedProperty.CachedInstance, new FileArtifactProperty(new FileInfo(nodeArtifact), "node")),
        });

        await fixture.Device.ConsumeAsync(Mock.Of<IDataProducer>(), update, TestContext.CancellationToken);
        await fixture.Device.ConsumeAsync(Mock.Of<IDataProducer>(), new SessionFileArtifact(new Microsoft.Testing.Platform.TestHost.SessionUid("session"), new FileInfo(sessionArtifact), "session"), TestContext.CancellationToken);
        await fixture.Device.ConsumeAsync(Mock.Of<IDataProducer>(), new FileArtifact(new FileInfo(runArtifact), "run"), TestContext.CancellationToken);
        await fixture.Device.DisplayAfterSessionEndRunAsync(TestContext.CancellationToken);

        string output = fixture.Output.ToString();
        Assert.Contains(nodeArtifact, output);
        Assert.Contains(sessionArtifact, output);
        Assert.Contains(runArtifact, output);
        Assert.Contains("Zero tests ran", output);
        Assert.Contains("total: 0", output);
        Assert.DoesNotContain("passed empty result", output);
    }

    [TestMethod]
    public async Task ActualConsume_CancellationPreventsOutcomeAndArtifactEmission()
    {
        using RecordingDeviceFixture fixture = new();
        await fixture.Device.InitializeAsync();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        FileArtifact artifact = new(new FileInfo(Path.Combine(Path.GetTempPath(), "not-opened.trx")), "artifact");

        OperationCanceledException error = await Assert.ThrowsAsync<OperationCanceledException>(
            () => fixture.Device.ConsumeAsync(Mock.Of<IDataProducer>(), artifact, cancellation.Token));

        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        Assert.AreEqual(string.Empty, fixture.Output.ToString());
    }

    private sealed class RecordingDeviceFixture : IDisposable
    {
        public RecordingDeviceFixture(bool azure = false, Dictionary<string, string[]>? extraOptions = null)
        {
            var console = new Mock<IConsole>();
            console.SetupGet(value => value.IsOutputRedirected).Returns(true);
            console.Setup(value => value.Write(It.IsAny<string>())).Callback<string>(value => Output.Append(value));
            console.Setup(value => value.Write(It.IsAny<StringBuilder>())).Callback<StringBuilder>(value => Output.Append(value));
            console.Setup(value => value.Write(It.IsAny<char>())).Callback<char>(value => Output.Append(value));
            console.Setup(value => value.WriteLine(It.IsAny<string>())).Callback<string>(value => Output.AppendLine(value));
            console.Setup(value => value.WriteLine()).Callback(() => Output.AppendLine());
            var module = new Mock<ITestApplicationModuleInfo>();
            module.Setup(value => value.GetDisplayName()).Returns("fixture.dll");
            var environment = new Mock<IEnvironment>();
            environment.Setup(value => value.GetEnvironmentVariable(It.IsAny<string>()))
                .Returns<string>(name => azure && name == "TF_BUILD" ? "true" : null);
            var monitor = new Mock<IAsyncMonitor>();
            monitor.Setup(value => value.LockAsync(It.IsAny<TimeSpan>())).ReturnsAsync(Mock.Of<IDisposable>());
            var loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup(value => value.CreateLogger(It.IsAny<string>())).Returns(new NopLogger());
            var policies = new Mock<IStopPoliciesService>();
            policies.Setup(value => value.RegisterOnAbortCallbackAsync(It.IsAny<Func<Task>>())).Returns(Task.CompletedTask);
            var cancellation = new Mock<ITestApplicationCancellationTokenSource>();
            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var optionValues = new Dictionary<string, string[]>
            {
                [TerminalTestReporterCommandLineOptionsProvider.AnsiOption] = ["off"],
                [TerminalTestReporterCommandLineOptionsProvider.ProgressOption] = ["off"],
                [TerminalTestReporterCommandLineOptionsProvider.ShowStdoutOption] = ["always"],
                [TerminalTestReporterCommandLineOptionsProvider.ShowStderrOption] = ["always"],
                [TerminalTestReporterCommandLineOptionsProvider.ShowTestResultsOption] = ["all"],
            };
            if (extraOptions is not null)
            {
                foreach (KeyValuePair<string, string[]> option in extraOptions)
                {
                    optionValues[option.Key] = option.Value;
                }
            }

            var options = new TestCommandLineOptions(optionValues);
            Device = new TerminalOutputDevice(console.Object, module.Object, Mock.Of<ITestHostControllerInfo>(), monitor.Object,
                Mock.Of<IRuntimeFeature>(), environment.Object, Mock.Of<IPlatformInformation>(), options, null,
                loggerFactory.Object, clock.Object, policies.Object, cancellation.Object, new TestCoverageResult());
        }

        public StringBuilder Output { get; } = new();

        public TerminalOutputDevice Device { get; }

        public void Dispose() => Device.Dispose();
    }

    [TestMethod]
    public async Task DisplayAsync_ListTestsJsonInAzureDevOps_WritesPlainErrorOnlyToStandardError()
        => await AssertListTestsJsonStandardErrorAsync(new ErrorMessageOutputDeviceData("boom"), "boom");

    [TestMethod]
    public async Task DisplayAsync_ListTestsJsonInAzureDevOps_WritesPlainExceptionOnlyToStandardError()
    {
        var exception = new InvalidOperationException("boom");

        await AssertListTestsJsonStandardErrorAsync(new ExceptionOutputDeviceData(exception), exception.Message);
    }

    [TestMethod]
    public async Task DisplayAsync_ListTestsJson_WritesSessionMessageToStandardError()
        => await AssertListTestsJsonStandardErrorAsync(new SessionMessageOutputDeviceData("Restoring assets"), "Restoring assets");

    [TestMethod]
    public async Task DisplayAsync_ListTestsJson_WritesOnlyChangedProgressMessagesToStandardError()
    {
        string standardError = await CaptureListTestsJsonStandardErrorAsync(async outputDevice =>
        {
            await outputDevice.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);
            await outputDevice.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);
            await outputDevice.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restored"), CancellationToken.None);
        });

        Assert.AreEqual(1, CountOccurrences(standardError, "Restoring"));
        Assert.AreEqual(1, CountOccurrences(standardError, "Restored"));
    }

    [TestMethod]
    public async Task DisplayAsync_ListTestsJson_ProgressMessageAfterRemovalWritesSameValueAgain()
    {
        string standardError = await CaptureListTestsJsonStandardErrorAsync(async outputDevice =>
        {
            await outputDevice.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);
            await outputDevice.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", null), CancellationToken.None);
            await outputDevice.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);
        });

        Assert.AreEqual(2, CountOccurrences(standardError, "Restoring"));
    }

    [TestMethod]
    public async Task DisplayAsync_ListTestsJson_HotReloadCycleResetsProgressMessageDeduplication()
    {
        string standardError = await CaptureListTestsJsonStandardErrorAsync(
            async outputDevice =>
            {
                await outputDevice.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);
                await outputDevice.DisplayAfterHotReloadSessionEndAsync(CancellationToken.None);
                await outputDevice.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);
            },
            isHotReloadEnabled: true);

        Assert.AreEqual(2, CountOccurrences(standardError, "Restoring"));
    }

    private static async Task AssertListTestsJsonStandardErrorAsync(IOutputDeviceData data, string expectedMessage)
    {
        string standardError = await CaptureListTestsJsonStandardErrorAsync(
            outputDevice => outputDevice.DisplayAsync(Producer, data, CancellationToken.None));

        Assert.Contains(expectedMessage, standardError);
        // Replace "##" so the assertion failure (if it fires) does not itself contain a literal
        // ##vso command that an Azure DevOps agent watching the test output could mistakenly act on.
        Assert.IsFalse(standardError.Contains("##vso[task.logissue", StringComparison.Ordinal), standardError.Replace("##", "[hash][hash]"));
    }

    private static async Task<string> CaptureListTestsJsonStandardErrorAsync(
        Func<TerminalOutputDevice, Task> action,
        bool isHotReloadEnabled = false)
    {
        TextWriter originalError = Console.Error;
        using var errorWriter = new StringWriter(CultureInfo.InvariantCulture);
        Console.SetError(errorWriter);

        try
        {
            using TerminalOutputDevice outputDevice = CreateListTestsJsonAzureDevOpsOutputDevice(isHotReloadEnabled);
            await outputDevice.InitializeAsync();

            await action(outputDevice);

            return errorWriter.ToString();
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    private static int CountOccurrences(string text, string value)
        => (text.Length - text.Replace(value, string.Empty).Length) / value.Length;

    [TestMethod]
    public async Task InitializeAsync_NoProgressLegacyFlag_WritesDeprecationWarningToStandardError()
    {
        string standardError = await InitializeAndCaptureStandardErrorAsync(new Dictionary<string, string[]>
        {
            [TerminalTestReporterCommandLineOptionsProvider.NoProgressOption] = [],
        });

        Assert.Contains("--no-progress is deprecated", standardError);
    }

    [TestMethod]
    public async Task InitializeAsync_NoProgressLegacyFlagInCI_DoesNotWriteDeprecationWarning()
    {
        // In CI the warning is invisible noise and build infrastructure (e.g. the Arcade SDK test runner) passes
        // --no-progress unconditionally, so emitting it to stderr would surface as a build error.
        string standardError = await InitializeAndCaptureStandardErrorAsync(
            new Dictionary<string, string[]>
            {
                [TerminalTestReporterCommandLineOptionsProvider.NoProgressOption] = [],
            },
            isCIEnvironment: true);

        Assert.IsFalse(standardError.Contains("--no-progress is deprecated", StringComparison.Ordinal), standardError);
    }

    [TestMethod]
    public async Task InitializeAsync_ProgressOff_DoesNotWriteDeprecationWarning()
    {
        string standardError = await InitializeAndCaptureStandardErrorAsync(new Dictionary<string, string[]>
        {
            [TerminalTestReporterCommandLineOptionsProvider.ProgressOption] = ["off"],
        });

        Assert.IsFalse(standardError.Contains("--no-progress is deprecated", StringComparison.Ordinal), standardError);
    }

    [TestMethod]
    public async Task InitializeAsync_ProgressOption_TakesPrecedenceOverLegacyNoProgress_NoDeprecationWarning()
    {
        // When --progress is provided it wins over the legacy --no-progress alias, so no warning is emitted.
        string standardError = await InitializeAndCaptureStandardErrorAsync(new Dictionary<string, string[]>
        {
            [TerminalTestReporterCommandLineOptionsProvider.ProgressOption] = ["off"],
            [TerminalTestReporterCommandLineOptionsProvider.NoProgressOption] = [],
        });

        Assert.IsFalse(standardError.Contains("--no-progress is deprecated", StringComparison.Ordinal), standardError);
    }

    private static async Task<string> InitializeAndCaptureStandardErrorAsync(Dictionary<string, string[]> options, bool isCIEnvironment = false)
    {
        TextWriter originalError = Console.Error;
        using var errorWriter = new StringWriter(CultureInfo.InvariantCulture);
        Console.SetError(errorWriter);
        ResetNoProgressDeprecationWarning();

        try
        {
            using TerminalOutputDevice outputDevice = CreateOutputDevice(options, isCIEnvironment);
            await outputDevice.InitializeAsync();

            return errorWriter.ToString();
        }
        finally
        {
            Console.SetError(originalError);
            ResetNoProgressDeprecationWarning();
        }
    }

    private static void ResetNoProgressDeprecationWarning()
        => typeof(TerminalOutputDevice)
            .GetField("s_noProgressDeprecationWarningEmitted", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, 0);

    private static TerminalOutputDevice CreateOutputDevice(Dictionary<string, string[]> options, bool isCIEnvironment = false)
    {
        var testApplicationModuleInfo = new Mock<ITestApplicationModuleInfo>();
        testApplicationModuleInfo.Setup(x => x.GetDisplayName()).Returns("testhost");

        var environment = new Mock<IEnvironment>();
        environment.Setup(x => x.GetEnvironmentVariable(It.IsAny<string>()))
            .Returns<string>(name => isCIEnvironment && name == "TF_BUILD" ? "true" : null);

        var stopPoliciesService = new Mock<IStopPoliciesService>();
        stopPoliciesService.Setup(x => x.RegisterOnAbortCallbackAsync(It.IsAny<Func<Task>>()))
            .Returns(Task.CompletedTask);

        var testApplicationCancellationTokenSource = new Mock<ITestApplicationCancellationTokenSource>();
        testApplicationCancellationTokenSource.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);

        return new TerminalOutputDevice(
            Mock.Of<IConsole>(),
            testApplicationModuleInfo.Object,
            Mock.Of<ITestHostControllerInfo>(),
            Mock.Of<IAsyncMonitor>(),
            Mock.Of<IRuntimeFeature>(),
            environment.Object,
            Mock.Of<IPlatformInformation>(),
            new TestCommandLineOptions(options),
            fileLoggerInformation: null,
            Mock.Of<ILoggerFactory>(),
            Mock.Of<IClock>(),
            stopPoliciesService.Object,
            testApplicationCancellationTokenSource.Object,
            new TestCoverageResult());
    }

    private static TerminalOutputDevice CreateListTestsJsonAzureDevOpsOutputDevice(bool isHotReloadEnabled = false)
    {
        var testApplicationModuleInfo = new Mock<ITestApplicationModuleInfo>();
        testApplicationModuleInfo.Setup(x => x.GetDisplayName()).Returns("testhost");

        var environment = new Mock<IEnvironment>();
        environment.Setup(x => x.GetEnvironmentVariable(It.IsAny<string>()))
            .Returns<string>(name => name == "TF_BUILD" ? "true" : null);

        var stopPoliciesService = new Mock<IStopPoliciesService>();
        stopPoliciesService.Setup(x => x.RegisterOnAbortCallbackAsync(It.IsAny<Func<Task>>()))
            .Returns(Task.CompletedTask);

        var testApplicationCancellationTokenSource = new Mock<ITestApplicationCancellationTokenSource>();
        testApplicationCancellationTokenSource.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);

        var runtimeFeature = new Mock<IRuntimeFeature>();
        runtimeFeature.SetupGet(x => x.IsHotReloadEnabled).Returns(isHotReloadEnabled);

        var asyncMonitor = new Mock<IAsyncMonitor>();
        asyncMonitor
            .Setup(x => x.LockAsync(It.IsAny<TimeSpan>()))
            .ReturnsAsync(Mock.Of<IDisposable>());

        return new TerminalOutputDevice(
            Mock.Of<IConsole>(),
            testApplicationModuleInfo.Object,
            Mock.Of<ITestHostControllerInfo>(),
            asyncMonitor.Object,
            runtimeFeature.Object,
            environment.Object,
            Mock.Of<IPlatformInformation>(),
            new TestCommandLineOptions(new Dictionary<string, string[]>
            {
                [PlatformCommandLineProvider.DiscoverTestsOptionKey] = [PlatformCommandLineProvider.DiscoverTestsJsonArgument],
            }),
            fileLoggerInformation: null,
            Mock.Of<ILoggerFactory>(),
            Mock.Of<IClock>(),
            stopPoliciesService.Object,
            testApplicationCancellationTokenSource.Object,
            new TestCoverageResult());
    }
}
