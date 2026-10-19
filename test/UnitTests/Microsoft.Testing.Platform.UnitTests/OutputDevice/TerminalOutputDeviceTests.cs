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
using Microsoft.Testing.Platform.Resources;
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
    public TestContext TestContext { get; set; } = null!;

    private static readonly IOutputDeviceDataProducer Producer = Mock.Of<IOutputDeviceDataProducer>(
        producer => producer.Uid == "producer");

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
    [DataRow(null, null, 31)]
    [DataRow("8", null, 39)]
    [DataRow(null, "8", 39)]
    [DataRow("2; 8;9", null, 39)]
    [DataRow("18", null, 31)]
    [DataRow("8", "2", 31)]
    [DataRow("2", "8", 39)]
    [DataRow("8", "", 39)]
    public async Task ActualSummary_ZeroTests_HonorsIgnorePolicy(string? commandLineIgnore, string? environmentIgnore, int expectedColor)
    {
        Dictionary<string, string[]> options = new()
        {
            [TerminalTestReporterCommandLineOptionsProvider.AnsiOption] = ["on"],
        };
        if (commandLineIgnore is not null)
        {
            options[PlatformCommandLineProvider.IgnoreExitCodeOptionKey] = [commandLineIgnore];
        }

        using RecordingDeviceFixture fixture = new(
            options,
            environmentVariables: new() { [EnvironmentVariableConstants.TESTINGPLATFORM_EXITCODE_IGNORE] = environmentIgnore });
        await fixture.Device.InitializeAsync();
        await fixture.Device.HandleProcessRoleAsync(TestProcessRole.TestHost, TestContext.CancellationToken);
        await fixture.Device.DisplayBeforeSessionStartAsync(TestContext.CancellationToken);
        await fixture.Device.DisplayAfterSessionEndRunAsync(TestContext.CancellationToken);

        string output = fixture.Output.ToString();
        Assert.Contains($"\u001B[{expectedColor}mTest run summary: Zero tests ran", output);
        Assert.AreEqual(1, CountOccurrences(output, "Test run summary: Zero tests ran"));
        Assert.DoesNotContain("Test run summary: Passed!", output);
    }

    [TestMethod]
    [DataRow(false, 31)]
    [DataRow(true, 39)]
    public async Task ActualSummary_StrictAllSkipped_HonorsZeroTestsOptOut(bool ignoreZeroTests, int expectedColor)
    {
        Dictionary<string, string[]> options = new()
        {
            [TerminalTestReporterCommandLineOptionsProvider.AnsiOption] = ["on"],
            [PlatformCommandLineProvider.ZeroTestsPolicyOptionKey] = ["strict"],
        };
        if (ignoreZeroTests)
        {
            options[PlatformCommandLineProvider.IgnoreExitCodeOptionKey] = ["8"];
        }

        using RecordingDeviceFixture fixture = new(options);
        await fixture.Device.InitializeAsync();
        await fixture.Device.HandleProcessRoleAsync(TestProcessRole.TestHost, TestContext.CancellationToken);
        await fixture.Device.DisplayBeforeSessionStartAsync(TestContext.CancellationToken);
        await fixture.Device.ConsumeAsync(Mock.Of<IDataProducer>(), new TestNodeUpdateMessage(default, new TestNode
        {
            Uid = "skipped",
            DisplayName = "skipped test",
            Properties = new PropertyBag(new SkippedTestNodeStateProperty("skip reason")),
        }), TestContext.CancellationToken);
        await fixture.Device.DisplayAfterSessionEndRunAsync(TestContext.CancellationToken);

        Assert.Contains($"\u001B[{expectedColor}mTest run summary: Zero tests ran", fixture.Output.ToString());
    }

    [TestMethod]
    [DataRow("failed")]
    [DataRow("minimum")]
    [DataRow("cancelled")]
    public async Task ActualSummary_IgnoredZeroTests_DoesNotHideOtherFailures(string scenario)
    {
        Dictionary<string, string[]> options = new()
        {
            [TerminalTestReporterCommandLineOptionsProvider.AnsiOption] = ["on"],
            [PlatformCommandLineProvider.IgnoreExitCodeOptionKey] = ["8"],
        };
        if (scenario == "minimum")
        {
            options[PlatformCommandLineProvider.MinimumExpectedTestsOptionKey] = ["1"];
        }

        using RecordingDeviceFixture fixture = new(options, wasCancelled: scenario == "cancelled");
        await fixture.Device.InitializeAsync();
        await fixture.Device.HandleProcessRoleAsync(TestProcessRole.TestHost, TestContext.CancellationToken);
        await fixture.Device.DisplayBeforeSessionStartAsync(TestContext.CancellationToken);
        if (scenario == "failed")
        {
            await fixture.Device.ConsumeAsync(Mock.Of<IDataProducer>(), new TestNodeUpdateMessage(default, new TestNode
            {
                Uid = "failed",
                DisplayName = "failed test",
                Properties = new PropertyBag(new FailedTestNodeStateProperty("failure reason")),
            }), TestContext.CancellationToken);
        }

        await fixture.Device.DisplayAfterSessionEndRunAsync(TestContext.CancellationToken);

        string expectedVerdict = scenario switch
        {
            "failed" => "Failed!",
            "minimum" => string.Format(CultureInfo.CurrentCulture, TerminalResources.MinimumExpectedTestsPolicyViolation, 0, 1),
            _ => PlatformResources.Aborted,
        };
        Assert.Contains($"\u001B[31mTest run summary: {expectedVerdict}", fixture.Output.ToString());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ActualSummary_IgnoredZeroTests_DoesNotHideMinimumAfterSkipping(bool strict)
    {
        using RecordingDeviceFixture fixture = new(new()
        {
            [TerminalTestReporterCommandLineOptionsProvider.AnsiOption] = ["on"],
            [PlatformCommandLineProvider.IgnoreExitCodeOptionKey] = ["8"],
            [PlatformCommandLineProvider.MinimumExpectedTestsOptionKey] = ["1"],
            [PlatformCommandLineProvider.ZeroTestsPolicyOptionKey] = [strict ? "strict" : "allow-skipped"],
        });
        await fixture.Device.InitializeAsync();
        await fixture.Device.HandleProcessRoleAsync(TestProcessRole.TestHost, TestContext.CancellationToken);
        await fixture.Device.DisplayBeforeSessionStartAsync(TestContext.CancellationToken);
        await fixture.Device.ConsumeAsync(Mock.Of<IDataProducer>(), new TestNodeUpdateMessage(default, new TestNode
        {
            Uid = "skipped",
            DisplayName = "skipped test",
            Properties = new PropertyBag(new SkippedTestNodeStateProperty("skip reason")),
        }), TestContext.CancellationToken);
        await fixture.Device.DisplayAfterSessionEndRunAsync(TestContext.CancellationToken);

        string expectedVerdict = string.Format(CultureInfo.CurrentCulture, TerminalResources.MinimumExpectedTestsPolicyViolation, 0, 1);
        Assert.Contains($"\u001B[31mTest run summary: {expectedVerdict}", fixture.Output.ToString());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ActualSummary_IgnoredZeroTests_LateSessionFailureStaysRed(bool ignoreViaEnvironment)
    {
        Dictionary<string, string[]> options = new()
        {
            [TerminalTestReporterCommandLineOptionsProvider.AnsiOption] = ["on"],
        };
        if (!ignoreViaEnvironment)
        {
            options[PlatformCommandLineProvider.IgnoreExitCodeOptionKey] = ["8"];
        }

        using RecordingDeviceFixture fixture = new(
            options,
            environmentVariables: new()
            {
                [EnvironmentVariableConstants.TESTINGPLATFORM_EXITCODE_IGNORE] = ignoreViaEnvironment ? "8" : null,
            });
        await fixture.Device.InitializeAsync();
        await fixture.Device.HandleProcessRoleAsync(TestProcessRole.TestHost, TestContext.CancellationToken);
        await fixture.Device.DisplayBeforeSessionStartAsync(TestContext.CancellationToken);
        Assert.IsFalse(fixture.ApplicationResult.HasTestAdapterTestSessionFailure);

        await fixture.ApplicationResult.SetTestAdapterTestSessionFailureAsync("session failed", TestContext.CancellationToken);
        await fixture.Device.DisplayAfterSessionEndRunAsync(TestContext.CancellationToken);

        Assert.AreEqual((int)ExitCode.TestAdapterTestSessionFailure, fixture.ApplicationResult.GetProcessExitCode());
        Assert.Contains("\u001B[31mTest run summary: Failed!", fixture.Output.ToString());
        Assert.DoesNotContain("Test run summary: Passed!", fixture.Output.ToString());
    }

    private sealed class RecordingDeviceFixture : IDisposable
    {
        public RecordingDeviceFixture(
            Dictionary<string, string[]> extraOptions,
            Dictionary<string, string?>? environmentVariables = null,
            bool wasCancelled = false)
        {
            var console = new Mock<IConsole>();
            console.SetupGet(value => value.IsOutputRedirected).Returns(true);
            console.Setup(value => value.Write(It.IsAny<string>())).Callback<string>(text => Output.Append(text));
            console.Setup(value => value.Write(It.IsAny<StringBuilder>())).Callback<StringBuilder>(text => Output.Append(text));
            console.Setup(value => value.Write(It.IsAny<char>())).Callback<char>(text => Output.Append(text));
            console.Setup(value => value.WriteLine(It.IsAny<string>())).Callback<string>(text => Output.AppendLine(text));
            console.Setup(value => value.WriteLine()).Callback(() => Output.AppendLine());
            var module = new Mock<ITestApplicationModuleInfo>();
            module.Setup(value => value.GetDisplayName()).Returns("fixture.dll");
            var environment = new Mock<IEnvironment>();
            environment.Setup(value => value.GetEnvironmentVariable(It.IsAny<string>()))
                .Returns<string>(name => environmentVariables is not null && environmentVariables.TryGetValue(name, out string? value) ? value : null);
            var loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup(value => value.CreateLogger(It.IsAny<string>())).Returns(new NopLogger());
            var policies = new Mock<IStopPoliciesService>();
            policies.Setup(value => value.RegisterOnAbortCallbackAsync(It.IsAny<Func<Task>>())).Returns(Task.CompletedTask);
            var cancellation = new Mock<ITestApplicationCancellationTokenSource>();
            cancellation.SetupGet(value => value.CancellationToken).Returns(new CancellationToken(wasCancelled));
            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var optionValues = new Dictionary<string, string[]>(extraOptions)
            {
                [TerminalTestReporterCommandLineOptionsProvider.ProgressOption] = ["off"],
            };
            var options = new TestCommandLineOptions(optionValues);
            var coverage = new TestCoverageResult();
            var services = new ServiceProvider();
            services.AddServices(
            [
                console.Object,
                module.Object,
                Mock.Of<ITestHostControllerInfo>(),
                new SystemMonitorAsyncFactory(),
                Mock.Of<IRuntimeFeature>(),
                environment.Object,
                Mock.Of<IPlatformInformation>(),
                options,
                loggerFactory.Object,
                clock.Object,
                policies.Object,
                cancellation.Object,
                coverage,
            ]);
            Device = Assert.IsInstanceOfType<TerminalOutputDevice>(PlatformOutputDeviceManager.GetDefaultTerminalOutputDevice(services));
            ApplicationResult = new TestApplicationResult(new ProxyOutputDevice(Device, serverModeOutputDevice: null), options, environment.Object, policies.Object, otelService: null, coverage);
            services.AddService(ApplicationResult);
        }

        public StringBuilder Output { get; } = new();

        public TerminalOutputDevice Device { get; }

        public TestApplicationResult ApplicationResult { get; }

        public void Dispose()
        {
            Device.Dispose();
            ApplicationResult.Dispose();
        }
    }

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
