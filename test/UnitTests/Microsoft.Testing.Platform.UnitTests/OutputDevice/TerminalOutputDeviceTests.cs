// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.OutputDevice.Terminal;
using Microsoft.Testing.Platform.ServerMode;
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

    [TestMethod]
    [DataRow(true, null)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    [DataRow(false, null)]
    [DataRow(false, true)]
    public async Task MaxFailedTestsCallback_UsesPolicyNegotiatedAfterRoleSetup(bool hasServerDevice, bool? requested)
    {
        List<Func<int, CancellationToken, Task>> callbacks = [];
        var policies = new Mock<IStopPoliciesService>();
        policies.Setup(service => service.RegisterOnMaxFailedTestsCallbackAsync(It.IsAny<Func<int, CancellationToken, Task>>()))
            .Callback<Func<int, CancellationToken, Task>>(callbacks.Add)
            .Returns(Task.CompletedTask);
        StringBuilder output = new();
        var console = new Mock<IConsole>();
        console.Setup(value => value.Write(It.IsAny<string>())).Callback<string>(value => output.Append(value));
        console.Setup(value => value.Write(It.IsAny<StringBuilder>())).Callback<StringBuilder>(value => output.Append(value));
        console.Setup(value => value.WriteLine(It.IsAny<string>())).Callback<string>(value => output.AppendLine(value));
        using TerminalOutputDevice originalDevice = CreateOutputDevice(
            new Dictionary<string, string[]>
            {
                [PlatformCommandLineProvider.ServerOptionKey] = [],
                [TerminalTestReporterCommandLineOptionsProvider.AnsiOption] = ["off"],
                [TerminalTestReporterCommandLineOptionsProvider.ProgressOption] = ["off"],
            },
            policiesService: policies.Object,
            console: console.Object);
        using var serverDevice = new ServerModePerCallOutputDevice(null);
        using var proxy = new ProxyOutputDevice(originalDevice, hasServerDevice ? serverDevice : null, policies.Object);
        await originalDevice.InitializeAsync();
        await proxy.HandleProcessRoleAsync(TestProcessRole.TestHost, CancellationToken.None);
        Assert.HasCount(1, callbacks);
        Assert.AreEqual(hasServerDevice && requested == true, proxy.ConfigureRpcOnlyOutput(requested));

        foreach (Func<int, CancellationToken, Task> callback in callbacks)
        {
            await callback(42, CancellationToken.None);
        }

        string firstOutput = output.ToString();
        Assert.AreEqual(hasServerDevice && requested == true, string.IsNullOrEmpty(firstOutput));
        var messages = (ConcurrentQueue<ServerLogMessage>)typeof(ServerModePerCallOutputDevice)
            .GetField("_messages", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(serverDevice)!;
        Assert.HasCount(hasServerDevice ? 1 : 0, messages);
        if (hasServerDevice)
        {
            Assert.AreEqual(LogLevel.Information, Assert.ContainsSingle(messages).Level);
        }

        proxy.ConfigureRpcOnlyOutput(false);
        foreach (Func<int, CancellationToken, Task> callback in callbacks)
        {
            await callback(42, CancellationToken.None);
        }

        Assert.IsGreaterThan(firstOutput.Length, output.Length);
        Assert.HasCount(hasServerDevice ? 2 : 0, messages);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RpcOnlyOutput_PreservesDebugMirrorExactlyOnceAndSuppressesOnlyConsole(bool? requested)
    {
        StringBuilder output = new();
        var console = new Mock<IConsole>();
        console.Setup(value => value.Write(It.IsAny<string>())).Callback<string>(value => output.Append(value));
        console.Setup(value => value.Write(It.IsAny<StringBuilder>())).Callback<StringBuilder>(value => output.Append(value));
        console.Setup(value => value.WriteLine(It.IsAny<string>())).Callback<string>(value => output.AppendLine(value));
        var logger = new Mock<ILogger>();
        logger.Setup(value => value.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(value => value.CreateLogger(It.IsAny<string>())).Returns(logger.Object);
        using TerminalOutputDevice original = CreateOutputDevice(
            new Dictionary<string, string[]>
            {
                [PlatformCommandLineProvider.ServerOptionKey] = [],
                [TerminalTestReporterCommandLineOptionsProvider.AnsiOption] = ["off"],
                [TerminalTestReporterCommandLineOptionsProvider.ProgressOption] = ["off"],
            },
            console: console.Object,
            loggerFactory: loggerFactory.Object,
            fileLoggerInformation: Mock.Of<IFileLoggerInformation>(info => info.LogFile == new FileInfo("diagnostics.log")));
        using var server = new ServerModePerCallOutputDevice(null);
        using var proxy = new ProxyOutputDevice(original, server, null);
        await original.InitializeAsync();
        Assert.AreEqual(requested == true, proxy.ConfigureRpcOnlyOutput(requested));
        IOutputDeviceData[] messages =
        [
            new TextOutputDeviceData("plain μ"),
            new FormattedTextOutputDeviceData("formatted") { Padding = 2 },
            new SessionMessageOutputDeviceData("session"),
            new ProgressMessageOutputDeviceData("key", "progress"),
            new ProgressMessageOutputDeviceData("key", null),
            new WarningMessageOutputDeviceData("warning"),
            new ErrorMessageOutputDeviceData("error"),
            new ExceptionOutputDeviceData(new InvalidOperationException("exception")),
        ];
        string[] diagnostics = ["plain μ", "formatted", "session", "progress", string.Empty, "warning", "error", "System.InvalidOperationException: exception"];
        foreach (IOutputDeviceData message in messages)
        {
            await proxy.DisplayAsync(Producer, message, CancellationToken.None);
        }

        foreach (string message in diagnostics)
        {
            logger.Verify(value => value.LogAsync(LogLevel.Debug, message, null, It.IsAny<Func<string, Exception?, string>>()), Times.Once);
        }

        await proxy.DisplayBannerAsync("banner", CancellationToken.None);
        await proxy.DisplayBeforeSessionStartAsync(CancellationToken.None);
        await proxy.DisplayAfterSessionEndRunAsync(CancellationToken.None);
        Assert.AreEqual(requested == true, output.Length == 0);
        var queuedMessages = (ConcurrentQueue<ServerLogMessage>)typeof(ServerModePerCallOutputDevice)
            .GetField("_messages", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(server)!;
        Assert.AreSequenceEqual(
            new[] { LogLevel.Information, LogLevel.Information, LogLevel.Information, LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.Error, LogLevel.Debug, LogLevel.Trace, LogLevel.Trace },
            queuedMessages.Select(message => message.Level));
        Assert.AreSequenceEqual(
            new[] { "plain μ", "  formatted", "session", "progress", "warning", "error", "System.InvalidOperationException: exception" },
            queuedMessages.Take(7).Select(message => message.Message));
    }

    [TestMethod]
    [DataRow(null, true)]
    [DataRow("off", false)]
    [DataRow("false", false)]
    public async Task RpcOnlyOutput_AzureDevOpsAgentDeclinesEvenWithAutomaticAnnotationsDisabled(string? annotationSetting, bool automaticAnnotations)
    {
        StringBuilder output = new();
        var console = new Mock<IConsole>();
        console.Setup(value => value.Write(It.IsAny<string>())).Callback<string>(value => output.Append(value));
        console.Setup(value => value.Write(It.IsAny<StringBuilder>())).Callback<StringBuilder>(value => output.Append(value));
        console.Setup(value => value.WriteLine(It.IsAny<string>())).Callback<string>(value => output.AppendLine(value));
        var environment = new Mock<IEnvironment>();
        environment.Setup(value => value.GetEnvironmentVariable("TF_BUILD")).Returns("true");
        environment.Setup(value => value.GetEnvironmentVariable("TESTINGPLATFORM_AZDO_OUTPUT")).Returns(annotationSetting);
        using TerminalOutputDevice original = CreateOutputDevice(
            new Dictionary<string, string[]>
            {
                [PlatformCommandLineProvider.ServerOptionKey] = [],
                [TerminalTestReporterCommandLineOptionsProvider.AnsiOption] = ["off"],
            },
            console: console.Object,
            environmentOverride: environment.Object);
        using var server = new ServerModePerCallOutputDevice(null);
        using var proxy = new ProxyOutputDevice(original, server, null);
        await original.InitializeAsync();
        Assert.IsFalse(proxy.ConfigureRpcOnlyOutput(true));

        await proxy.DisplayAsync(Producer, new WarningMessageOutputDeviceData("warning"), CancellationToken.None);
        await proxy.DisplayAsync(Producer, new TextOutputDeviceData("##vso[results.publish type=JUnit;]report.xml"), CancellationToken.None);

        Assert.Contains("warning", output.ToString());
        Assert.Contains("##vso[results.publish type=JUnit;]report.xml", output.ToString());
        Assert.AreEqual(automaticAnnotations, output.ToString().Contains("##vso[task.logissue type=warning]warning", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, true)]
    public async Task RpcOnlyOutput_NormalConsoleAndMachineReadableDiscoveryDecline(bool serverMode, bool listTestsJson)
    {
        Dictionary<string, string[]> options = [];
        if (serverMode)
        {
            options[PlatformCommandLineProvider.ServerOptionKey] = [];
        }

        if (listTestsJson)
        {
            options[PlatformCommandLineProvider.DiscoverTestsOptionKey] = [PlatformCommandLineProvider.DiscoverTestsJsonArgument];
        }

        using TerminalOutputDevice original = CreateOutputDevice(options);
        using var server = new ServerModePerCallOutputDevice(null);
        using var proxy = new ProxyOutputDevice(original, server, null);
        await original.InitializeAsync();
        Assert.IsFalse(proxy.ConfigureRpcOnlyOutput(true));
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

    private static TerminalOutputDevice CreateOutputDevice(
        Dictionary<string, string[]> options,
        bool isCIEnvironment = false,
        IStopPoliciesService? policiesService = null,
        IConsole? console = null,
        ILoggerFactory? loggerFactory = null,
        IFileLoggerInformation? fileLoggerInformation = null,
        IEnvironment? environmentOverride = null)
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
            console ?? Mock.Of<IConsole>(),
            testApplicationModuleInfo.Object,
            Mock.Of<ITestHostControllerInfo>(),
            Mock.Of<IAsyncMonitor>(),
            Mock.Of<IRuntimeFeature>(),
            environmentOverride ?? environment.Object,
            Mock.Of<IPlatformInformation>(),
            new TestCommandLineOptions(options),
            fileLoggerInformation,
            loggerFactory ?? Mock.Of<ILoggerFactory>(),
            Mock.Of<IClock>(),
            policiesService ?? stopPoliciesService.Object,
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
