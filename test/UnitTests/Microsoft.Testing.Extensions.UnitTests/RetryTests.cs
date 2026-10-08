#pragma warning disable IDE0073 // The file header does not match the required text
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.
#pragma warning restore IDE0073 // The file header does not match the required text

using System.Collections;
using System.IO.Pipes;
using System.Reflection;

using Microsoft.Testing.Extensions.Policy;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHostOrchestrator;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

#pragma warning disable TPEXP // Artifact post-processing is experimental.

[TestClass]
public class RetryTests
{
    private const string ContosoPackageSid = "S-1-15-2-1990679259-4123976751-842158434-3026549936-2944832882-252165955-409282942";

    [TestMethod]
    public void RandomId_NextProducesFivePoolCharactersAndVariesAcrossCalls()
    {
        string[] values = [.. Enumerable.Range(0, 32).Select(_ => RandomId.Next())];

        Assert.IsTrue(values.All(value => value.Length == 5));
        Assert.IsTrue(values.All(value => value.All(character =>
            "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz".IndexOf(character) >= 0)));
        Assert.IsGreaterThan(1, values.Distinct(StringComparer.Ordinal).Count());
    }

    [TestMethod]
    public async Task RetryOrchestrator_ConstructorInitializesServicesAndEnablement()
    {
        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(new TestCommandLineOptions(new()
        {
            [RetryCommandLineOptionsProvider.RetryFailedTestsOptionName] = ["1"],
        }));
        serviceProvider.AddService(Mock.Of<IFileSystem>());

        var orchestrator = new RetryOrchestrator(serviceProvider);

        Assert.IsTrue(await orchestrator.IsEnabledAsync());
        Assert.AreEqual(nameof(RetryOrchestrator), orchestrator.Uid);
    }

    [TestMethod]
    [DataRow(null, null)]
    [DataRow("", "")]
    public void RetryOrchestrator_InitializeEnvironment_StandaloneRunGeneratesLogicalRunId(string? logicalRunId, string? executionId)
    {
        Dictionary<string, string?> values = InitializeRetryEnvironment(logicalRunId, executionId);

        Assert.IsTrue(Guid.TryParse(values["TESTINGPLATFORM_LOGICAL_RUN_ID"], out _));
        Assert.AreEqual(executionId, values["TESTINGPLATFORM_DOTNETTEST_EXECUTIONID"]);
    }

    [TestMethod]
    [DataRow(null, "module-execution", null)]
    [DataRow("", "module-execution", "")]
    [DataRow("logical-run", null, "logical-run")]
    [DataRow("logical-run", "module-execution", "logical-run")]
    public void RetryOrchestrator_InitializeEnvironment_UsesOnlyLogicalRunContext(string? logicalRunId, string? executionId, string? expectedLogicalRunId)
    {
        Dictionary<string, string?> values = InitializeRetryEnvironment(logicalRunId, executionId);

        Assert.AreEqual(expectedLogicalRunId, values["TESTINGPLATFORM_LOGICAL_RUN_ID"]);
        Assert.AreEqual(executionId, values["TESTINGPLATFORM_DOTNETTEST_EXECUTIONID"]);
    }

    [TestMethod]
    public void RetryPipeServer_UsesCompactUniqueNameAndLogsIt()
    {
        ServiceProvider serviceProvider = CreateRetryServiceProvider();
        Mock<ILogger> logger = CreateRecordingLogger();

        using var server = new RetryFailedTestsPipeServer(serviceProvider, [], logger.Object);

        string pattern = Path.DirectorySeparatorChar == '/'
            ? @"(?:^|[\\/])[0-9a-f]{32}$"
            : @"^testingplatform\.pipe\.[0-9a-f]{32}$";
        Assert.MatchesRegex(new Regex(pattern, RegexOptions.CultureInvariant), server.PipeName);
        logger.Verify(
            value => value.Log(
                LogLevel.Trace,
                $"Retry server pipe name: '{server.PipeName}'",
                null,
                LoggingExtensions.Formatter),
            Times.Once);
    }

    [TestMethod]
    public async Task RetryPipeServer_GetListRequestReturnsConfiguredFailedTests()
    {
        ServiceProvider serviceProvider = CreateRetryServiceProvider();
        using var server = new RetryFailedTestsPipeServer(
            serviceProvider,
            ["failed-1", "failed-2"],
            Mock.Of<ILogger>());
        MethodInfo callback = typeof(RetryFailedTestsPipeServer).GetMethod(
            "CallbackAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        var task = (Task)callback.Invoke(server, [new GetListOfFailedTestsRequest()])!;
        await task;
        var response = (GetListOfFailedTestsResponse)task.GetType().GetProperty("Result")!.GetValue(task)!;

        Assert.AreSequenceEqual(["failed-1", "failed-2"], response.FailedTestIds);
    }

    [TestMethod]
    public void RetryPipeServer_RegistersCoreRequestAndResponseSerializers()
    {
        ServiceProvider serviceProvider = CreateRetryServiceProvider();
        using var server = new RetryFailedTestsPipeServer(serviceProvider, [], Mock.Of<ILogger>());
        object innerServer = typeof(RetryFailedTestsPipeServer)
            .GetField("_singleConnectionNamedPipeServer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(server)!;
        FieldInfo? serializerField = null;
        for (Type? type = innerServer.GetType(); type is not null && serializerField is null; type = type.BaseType)
        {
            serializerField = type.GetField("_typeSerializer", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        Assert.IsNotNull(serializerField);
        var serializers = (IDictionary)serializerField.GetValue(innerServer)!;
        Assembly retryAssembly = typeof(RetryOrchestrator).Assembly;
        Type voidResponseType = retryAssembly.GetType(
            "Microsoft.Testing.Platform.IPC.Models.VoidResponse",
            throwOnError: true)!;
        Type getListRequestType = retryAssembly.GetType(
            "Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers.GetListOfFailedTestsRequest",
            throwOnError: true)!;
        Type voidResponseSerializerType = retryAssembly.GetType(
            "Microsoft.Testing.Platform.IPC.Serializers.VoidResponseSerializer",
            throwOnError: true)!;
        Type getListRequestSerializerType = retryAssembly.GetType(
            "Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers.GetListOfFailedTestsRequestSerializer",
            throwOnError: true)!;
        Assert.AreEqual(voidResponseSerializerType, serializers[voidResponseType]!.GetType());
        Assert.AreEqual(getListRequestSerializerType, serializers[getListRequestType]!.GetType());
    }

    [TestMethod]
    public async Task RetryLifecycleCallbacks_MultiplePipeNamesFailWithExactArgumentMessage()
    {
        ServiceProvider serviceProvider = CreateRetryServiceProvider(new TestCommandLineOptions(new()
        {
            [RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName] = ["one", "two"],
        }));
        var lifecycle = new RetryLifecycleCallbacks(serviceProvider);

        ArgumentException exception = await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => lifecycle.BeforeRunAsync(CancellationToken.None));

        Assert.AreEqual("pipeName", exception.ParamName);
        Assert.Contains("Pipe name expected", exception.Message);
    }

    [TestMethod]
    public async Task RetryLifecycleCallbacks_LogsExactPipeNameBeforeCanceledConnect()
    {
        const string PipeName = "missing-retry-pipe";
        Mock<ILogger> logger = CreateRecordingLogger();
        ServiceProvider serviceProvider = CreateRetryServiceProvider(
            new TestCommandLineOptions(new()
            {
                [RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName] = [PipeName],
            }),
            logger.Object);
        var lifecycle = new RetryLifecycleCallbacks(serviceProvider);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => lifecycle.BeforeRunAsync(cancellation.Token));

        logger.Verify(
            value => value.LogAsync(
                LogLevel.Debug,
                $"Connecting to pipe '{PipeName}'",
                null,
                LoggingExtensions.Formatter),
            Times.Once);
    }

    [DataRow(true)]
    [DataRow(false)]
    [TestMethod]
    public async Task LogResponseFileFallbackWarningAsync_OnlyQuotedSuffixLogsWarning(bool quoteIsInDirectPrefix)
    {
        const string GeneratedResponseFilePath = "retry-arguments-1.rsp";
        string[] originalArguments = quoteIsInDirectPrefix
            ? ["quoted\"prefix", "@original.rsp"]
            : ["@original.rsp", "quoted\"suffix"];
        List<string> finalArguments = quoteIsInDirectPrefix
            ? ["quoted\"prefix", $"@{GeneratedResponseFilePath}"]
            : ["quoted\"suffix"];
        var logger = new Mock<ILogger>();
        logger
            .Setup(value => value.LogAsync(
                LogLevel.Warning,
                It.IsAny<string>(),
                null,
                It.IsAny<Func<string, Exception?, string>>()))
            .Returns(Task.CompletedTask);

        await RetryOrchestrator.LogResponseFileFallbackWarningAsync(
            logger.Object,
            originalArguments,
            finalArguments,
            GeneratedResponseFilePath);

        logger.Verify(
            value => value.LogAsync(
                LogLevel.Warning,
                "Retry arguments could not be regenerated in a response file because an argument contains a literal double quote. "
                + "The retry command line may exceed the operating system limit.",
                null,
                It.IsAny<Func<string, Exception?, string>>()),
            quoteIsInDirectPrefix ? Times.Never : Times.Once);
    }

    [TestMethod]
    public async Task LogResponseFileFallbackWarningAsync_WithoutOriginalResponseFile_DoesNotLog()
    {
        Mock<ILogger> logger = CreateRecordingLogger();

        await RetryOrchestrator.LogResponseFileFallbackWarningAsync(
            logger.Object,
            ["test.dll", "--quoted=\"value\""],
            ["test.dll"],
            "retry-arguments-1.rsp");

        logger.Verify(
            value => value.LogAsync(
                It.IsAny<LogLevel>(),
                It.IsAny<string>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<string, Exception?, string>>()),
            Times.Never);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "AppContainer pipe authorization is Windows-only.")]
    public void RetryPipeServer_UsesControllerAuthorizedSecurityIdentities()
    {
        ServiceProvider serviceProvider = new()
        {
            TestHostControllerAuthorizedSecurityIdentities = [ContosoPackageSid],
        };
        serviceProvider.AddService(new Mock<IEnvironment>().Object);
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
        serviceProvider.AddService(loggerFactory.Object);
        serviceProvider.AddService(new SystemTask());
        Mock<ITestApplicationCancellationTokenSource> cancellationTokenSource = new();
        cancellationTokenSource.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        serviceProvider.AddService(cancellationTokenSource.Object);
        serviceProvider.AddService(new TestCommandLineOptions([]));
        serviceProvider.AddService(new Mock<IFileSystem>().Object);

        var orchestrator = new RetryOrchestrator(serviceProvider);

        using var server = new RetryFailedTestsPipeServer(serviceProvider, [], new Mock<ILogger>().Object);

        Assert.IsInstanceOfType<ITestHostControllerConnectionAuthorizationConsumer>(orchestrator);
        Assert.IsTrue(server.PipeName.StartsWith(@"LOCAL\", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task RunAttemptAsync_UsesRegisteredTestHostLauncher()
    {
        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(new Mock<IEnvironment>().Object);
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
        serviceProvider.AddService(loggerFactory.Object);
        serviceProvider.AddService(new SystemTask());
        Mock<IFileSystem> fileSystem = new();
        fileSystem.Setup(value => value.ExistFile(It.IsAny<string>())).Returns(true);
        serviceProvider.AddService(fileSystem.Object);
        Mock<ITestApplicationCancellationTokenSource> cancellationTokenSource = new();
        cancellationTokenSource.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        serviceProvider.AddService(cancellationTokenSource.Object);
        Mock<IProcessHandler> processHandler = new(MockBehavior.Strict);
        serviceProvider.AddService(processHandler.Object);
        var launcher = new ConnectingTestHostLauncher();
        serviceProvider.AddService(launcher);
        Mock<ILogger> logger = CreateRecordingLogger();

        using var server = new RetryFailedTestsPipeServer(serviceProvider, [], new Mock<ILogger>().Object);
        List<string> arguments =
        [
            $"--{RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName}",
            server.PipeName,
        ];

        RetryTestHostRunner.AttemptResult result = await RetryTestHostRunner.RunAttemptAsync(
            serviceProvider,
            Mock.Of<IOutputDeviceDataProducer>(),
            Mock.Of<IOutputDevice>(),
            logger.Object,
            server,
            new ExecutableInfo("testhost.exe", [], string.Empty),
            arguments,
            attemptCount: 1,
            userMaxRetryCount: 2,
            CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.IsFalse(result.ExitedBeforeConnect);
        Assert.AreEqual("testhost.exe", launcher.Context!.FileName);
        Assert.AreSequenceEqual(arguments, launcher.Context.Arguments);
        Assert.IsNull(launcher.Context.WorkingDirectory);
        Assert.MatchesRegex(
            new Regex(@"^testfx-retry-recovered-artifacts-[0-9a-f]{32}\.txt$", RegexOptions.CultureInvariant),
            Path.GetFileName(result.RecoveredArtifactManifestPath));
        logger.Verify(
            value => value.LogAsync(
                LogLevel.Debug,
                "Starting test host process, attempt 1/2",
                null,
                LoggingExtensions.Formatter),
            Times.Once);
        logger.Verify(
            value => value.LogAsync(
                LogLevel.Debug,
                $"Delegating retry test host launch to '{launcher.DisplayName}' (UID: {launcher.Uid})",
                null,
                LoggingExtensions.Formatter),
            Times.Once);
        logger.Verify(
            value => value.LogAsync(
                LogLevel.Debug,
                $"Retry test host launched by '{launcher.Uid}' (Identifier: '{nameof(ConnectedTestHostHandle)}')",
                null,
                LoggingExtensions.Formatter),
            Times.Once);
        logger.Verify(
            value => value.LogAsync(
                LogLevel.Debug,
                "Wait connection from the test host process",
                null,
                LoggingExtensions.Formatter),
            Times.Once);
        processHandler.Verify(x => x.Start(It.IsAny<ProcessStartInfo>()), Times.Never);
        fileSystem.Verify(value => value.DeleteFile(result.RecoveredArtifactManifestPath), Times.Never);
    }

    [DataRow(0, 1, true, 0)]
    [DataRow(1, 0, true, 2)]
    [DataRow(0, 0, false, 1)]
    [TestMethod]
    public async Task RunAttemptAsync_NonAuthoritativeHandle_DerivesExitCodeFromReportedCounts(
        int failedTestResults,
        int passedTestResults,
        bool countsReported,
        int expectedExitCode)
    {
        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(new Mock<IEnvironment>().Object);
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
        serviceProvider.AddService(loggerFactory.Object);
        serviceProvider.AddService(new SystemTask());
        Mock<IFileSystem> fileSystem = new();
        fileSystem.Setup(value => value.ExistFile(It.IsAny<string>())).Returns(true);
        serviceProvider.AddService(fileSystem.Object);
        serviceProvider.AddService(Mock.Of<ITestApplicationCancellationTokenSource>(
            source => source.CancellationToken == CancellationToken.None));
        serviceProvider.AddService(new Mock<IProcessHandler>(MockBehavior.Strict).Object);

        using var server = new RetryFailedTestsPipeServer(serviceProvider, [], Mock.Of<ILogger>());
        serviceProvider.AddService(new ConnectingTestHostLauncher(
            exitCode: 1,
            isExitCodeAuthoritative: false,
            onConnected: () =>
            {
                if (!countsReported)
                {
                    return;
                }

                typeof(RetryFailedTestsPipeServer)
                    .GetProperty(nameof(RetryFailedTestsPipeServer.FailedTestResults))!
                    .SetValue(server, failedTestResults);
                typeof(RetryFailedTestsPipeServer)
                    .GetProperty(nameof(RetryFailedTestsPipeServer.TotalTestRan))!
                    .SetValue(server, failedTestResults + passedTestResults);
                typeof(RetryFailedTestsPipeServer)
                    .GetProperty(nameof(RetryFailedTestsPipeServer.CountsReported))!
                    .SetValue(server, true);
            }));

        List<string> arguments =
        [
            $"--{RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName}",
            server.PipeName,
        ];

        RetryTestHostRunner.AttemptResult result = await RetryTestHostRunner.RunAttemptAsync(
            serviceProvider,
            Mock.Of<IOutputDeviceDataProducer>(),
            Mock.Of<IOutputDevice>(),
            Mock.Of<ILogger>(),
            server,
            new ExecutableInfo("testhost.exe", [], string.Empty),
            arguments,
            attemptCount: 1,
            userMaxRetryCount: 2,
            CancellationToken.None);

        Assert.AreEqual(expectedExitCode, result.ExitCode);
        Assert.IsFalse(result.ExitedBeforeConnect);
    }

    [TestMethod]
    public async Task RunAttemptAsync_AlreadyExitedCustomHandle_DoesNotWaitForPipeTimeout()
    {
        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(new Mock<IEnvironment>().Object);
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
        serviceProvider.AddService(loggerFactory.Object);
        serviceProvider.AddService(new SystemTask());
        Mock<IFileSystem> fileSystem = new();
        fileSystem.Setup(value => value.ExistFile(It.IsAny<string>())).Returns(true);
        serviceProvider.AddService(fileSystem.Object);
        Mock<ITestApplicationCancellationTokenSource> cancellationTokenSource = new();
        cancellationTokenSource.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        serviceProvider.AddService(cancellationTokenSource.Object);
        serviceProvider.AddService(new Mock<IProcessHandler>(MockBehavior.Strict).Object);
        var launcher = new AlreadyExitedTestHostLauncher(exitCode: 7);
        serviceProvider.AddService(launcher);
        Mock<IOutputDevice> outputDevice = new();
        IOutputDeviceData? displayedData = null;
        outputDevice.Setup(x => x.DisplayAsync(
                It.IsAny<IOutputDeviceDataProducer>(),
                It.IsAny<IOutputDeviceData>(),
                It.IsAny<CancellationToken>()))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>(
                (_, data, _) => displayedData = data)
            .Returns(Task.CompletedTask);

        using var server = new RetryFailedTestsPipeServer(serviceProvider, [], new Mock<ILogger>().Object);

        RetryTestHostRunner.AttemptResult result = await RetryTestHostRunner.RunAttemptAsync(
            serviceProvider,
            Mock.Of<IOutputDeviceDataProducer>(),
            outputDevice.Object,
            Mock.Of<ILogger>(),
            server,
            new ExecutableInfo("testhost.exe", [], "working-dir"),
            [],
            attemptCount: 1,
            userMaxRetryCount: 2,
            CancellationToken.None);

        Assert.AreEqual(7, result.ExitCode);
        Assert.AreEqual((ExitCode: 7, ExitedBeforeConnect: true), (result.ExitCode, result.ExitedBeforeConnect));
        Assert.IsNull(launcher.Context!.WorkingDirectory);
        ErrorMessageOutputDeviceData error = Assert.IsInstanceOfType<ErrorMessageOutputDeviceData>(displayedData);
        Assert.Contains("7", error.Message);
        fileSystem.Verify(value => value.DeleteFile(result.RecoveredArtifactManifestPath), Times.Never);
        outputDevice.Verify(
            x => x.DisplayAsync(
                It.IsAny<IOutputDeviceDataProducer>(),
                It.IsAny<IOutputDeviceData>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task LaunchUsingCustomLauncherAsync_ForwardsExplicitProcessWorkingDirectory()
    {
        var launcher = new AlreadyExitedTestHostLauncher(exitCode: 0);
        var processStartInfo = new ProcessStartInfo("testhost.exe")
        {
            WorkingDirectory = "working-dir",
        };
        MethodInfo method = typeof(RetryTestHostRunner).GetMethod(
            "LaunchUsingCustomLauncherAsync",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        var task = (Task<IProcess>)method.Invoke(
            null,
            [
                launcher,
                processStartInfo,
                Array.Empty<string>(),
                CreateRecordingLogger().Object,
                CancellationToken.None,
            ])!;
        using IProcess process = await task;

        Assert.AreEqual("working-dir", launcher.Context!.WorkingDirectory);
    }

    [TestMethod]
    public async Task RunAttemptAsync_WithoutCustomLauncherUsesExactProcessStartInfo()
    {
        ServiceProvider serviceProvider = CreateRetryServiceProvider();
        ProcessStartInfo? capturedStartInfo = null;
        Mock<IProcess> process = new();
        process.SetupGet(value => value.HasExited).Returns(true);
        process.SetupGet(value => value.ExitCode).Returns(7);
        Mock<IProcessHandler> processHandler = new();
        processHandler
            .Setup(value => value.Start(It.IsAny<ProcessStartInfo>()))
            .Callback<ProcessStartInfo>(startInfo => capturedStartInfo = startInfo)
            .Returns(process.Object);
        serviceProvider.AddService(processHandler.Object);
        Mock<IOutputDevice> outputDevice = new();
        outputDevice
            .Setup(value => value.DisplayAsync(
                It.IsAny<IOutputDeviceDataProducer>(),
                It.IsAny<IOutputDeviceData>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        using var server = new RetryFailedTestsPipeServer(serviceProvider, [], Mock.Of<ILogger>());

        RetryTestHostRunner.AttemptResult result = await RetryTestHostRunner.RunAttemptAsync(
            serviceProvider,
            Mock.Of<IOutputDeviceDataProducer>(),
            outputDevice.Object,
            CreateRecordingLogger().Object,
            server,
            new ExecutableInfo("testhost.exe", [], string.Empty),
            ["argument"],
            attemptCount: 3,
            userMaxRetryCount: 4,
            CancellationToken.None);

        Assert.IsTrue(result.ExitedBeforeConnect);
        Assert.IsNotNull(capturedStartInfo);
        Assert.IsFalse(capturedStartInfo.UseShellExecute);
        Assert.AreEqual(
            "3",
            capturedStartInfo.EnvironmentVariables["TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER"]);
        Assert.AreEqual(
            result.RecoveredArtifactManifestPath,
            capturedStartInfo.EnvironmentVariables["TESTINGPLATFORM_RETRY_RECOVERED_ARTIFACT_MANIFEST"]);
        processHandler.Verify(value => value.Start(It.IsAny<ProcessStartInfo>()), Times.Once);
    }

    [TestMethod]
    public async Task RunAttemptAsync_ProcessExitEventWinsBeforeConnection()
    {
        ServiceProvider serviceProvider = CreateRetryServiceProvider();
        var process = new ExitOnStatusProbeProcess(exitCode: 9);
        Mock<IProcessHandler> processHandler = new();
        processHandler.Setup(value => value.Start(It.IsAny<ProcessStartInfo>())).Returns(process);
        serviceProvider.AddService(processHandler.Object);
        Mock<IOutputDevice> outputDevice = new();
        outputDevice
            .Setup(value => value.DisplayAsync(
                It.IsAny<IOutputDeviceDataProducer>(),
                It.IsAny<IOutputDeviceData>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        using var server = new RetryFailedTestsPipeServer(serviceProvider, [], Mock.Of<ILogger>());
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Mock<ILogger> logger = CreateRecordingLogger();

        RetryTestHostRunner.AttemptResult result = await RetryTestHostRunner.RunAttemptAsync(
            serviceProvider,
            Mock.Of<IOutputDeviceDataProducer>(),
            outputDevice.Object,
            logger.Object,
            server,
            new ExecutableInfo("testhost.exe", [], string.Empty),
            [],
            attemptCount: 1,
            userMaxRetryCount: 1,
            cancellation.Token);

        Assert.AreEqual(9, result.ExitCode);
        Assert.IsTrue(result.ExitedBeforeConnect);
        Assert.IsTrue(process.HandlerWasAttached);
        Assert.IsTrue(process.HandlerWasDetached);
        logger.Verify(
            value => value.Log(
                LogLevel.Debug,
                "Test host process exited, PID: ''",
                null,
                LoggingExtensions.Formatter),
            Times.Once);
    }

    [TestMethod]
    public void RetryConnectionRaceHelpers_DistinguishEveryCompletionState()
    {
        Task completedConnection = Task.CompletedTask;
        var pendingConnection = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task completedExit = Task.FromResult(0);

        Assert.IsFalse(InvokeRetryTestHostRunnerPredicate(
            "ShouldWaitForConnectionGracePeriod",
            completedConnection,
            completedConnection));
        Assert.IsTrue(InvokeRetryTestHostRunnerPredicate(
            "ShouldWaitForConnectionGracePeriod",
            completedExit,
            pendingConnection.Task));
        Assert.IsFalse(InvokeRetryTestHostRunnerPredicate(
            "ShouldWaitForConnectionGracePeriod",
            completedExit,
            completedConnection));

        Assert.IsTrue(InvokeRetryTestHostRunnerPredicate(
            "HasConnectionCompleted",
            completedConnection,
            completedConnection));
        Assert.IsTrue(InvokeRetryTestHostRunnerPredicate(
            "HasConnectionCompleted",
            completedExit,
            completedConnection));
        Assert.IsFalse(InvokeRetryTestHostRunnerPredicate(
            "HasConnectionCompleted",
            completedExit,
            pendingConnection.Task));
    }

    [TestMethod]
    public async Task RetryConnectionGracePeriod_WaitsForConnectionAndPropagatesCancellation()
    {
        Task completedExit = Task.FromResult(0);
        Task completedConnection = Task.CompletedTask;
        var pendingConnection = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<Task> gracePeriod = InvokeRetryTestHostRunnerGracePeriodAsync(
            completedExit,
            pendingConnection.Task,
            CancellationToken.None);
        Assert.IsFalse(gracePeriod.IsCompleted);

        pendingConnection.SetResult(true);
        Assert.AreSame(pendingConnection.Task, await gracePeriod);

        Task<Task> noGracePeriod = InvokeRetryTestHostRunnerGracePeriodAsync(
            completedExit,
            completedConnection,
            CancellationToken.None);
        Assert.AreSame(completedExit, await noGracePeriod);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var neverConnects = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => InvokeRetryTestHostRunnerGracePeriodAsync(
                completedExit,
                neverConnects.Task,
                cancellation.Token));
    }

    [TestMethod]
    public async Task RunAttemptAsync_CanceledConnection_TerminatesCustomHandleBeforeDisposal()
    {
        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(new Mock<IEnvironment>().Object);
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
        serviceProvider.AddService(loggerFactory.Object);
        serviceProvider.AddService(new SystemTask());
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(fs => fs.ExistFile(It.IsAny<string>())).Returns(true);
        serviceProvider.AddService(fileSystem.Object);
        Mock<ITestApplicationCancellationTokenSource> applicationCancellation = new();
        applicationCancellation.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        serviceProvider.AddService(applicationCancellation.Object);
        serviceProvider.AddService(new Mock<IProcessHandler>(MockBehavior.Strict).Object);
        var launcher = new TerminatedTestHostLauncher();
        serviceProvider.AddService(launcher);
        using var server = new RetryFailedTestsPipeServer(serviceProvider, [], new Mock<ILogger>().Object);
        using var cancellation = new CancellationTokenSource();
#pragma warning disable VSTHRD103 // CancelAsync is unavailable on .NET Framework.
        cancellation.Cancel();
#pragma warning restore VSTHRD103

        await Assert.ThrowsAsync<OperationCanceledException>(() => RetryTestHostRunner.RunAttemptAsync(
            serviceProvider,
            Mock.Of<IOutputDeviceDataProducer>(),
            Mock.Of<IOutputDevice>(),
            Mock.Of<ILogger>(),
            server,
            new ExecutableInfo("testhost.exe", [], string.Empty),
            [],
            attemptCount: 1,
            userMaxRetryCount: 2,
            cancellation.Token));

        Assert.IsTrue(launcher.Handle.TerminateCalled);
        Assert.IsTrue(launcher.Handle.Disposed);
        fileSystem.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public async Task TerminateAndWaitForExitAsync_KillsThenWaitsForExit()
    {
        Mock<IProcess> process = new(MockBehavior.Strict);
        process.Setup(value => value.Kill());
        process
            .Setup(value => value.WaitForExitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        MethodInfo method = typeof(RetryTestHostRunner).GetMethod(
            "TerminateAndWaitForExitAsync",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        var task = (Task)method.Invoke(null, [process.Object, CreateRecordingLogger().Object])!;
        await task;

        process.Verify(value => value.Kill(), Times.Once);
        process.Verify(value => value.WaitForExitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task RunAttemptAsync_LauncherFailure_DeletesRecoveredArtifactManifest()
    {
        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(Mock.Of<IEnvironment>());
        serviceProvider.AddService(Mock.Of<IProcessHandler>());
        serviceProvider.AddService(new SystemTask());
        serviceProvider.AddService(Mock.Of<ITestApplicationCancellationTokenSource>(
            source => source.CancellationToken == CancellationToken.None));
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(factory => factory.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        serviceProvider.AddService(loggerFactory.Object);
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(fs => fs.ExistFile(It.IsAny<string>())).Returns(true);
        serviceProvider.AddService(fileSystem.Object);
        Mock<ITestHostLauncher> launcher = new();
        string? expectedManifestPath = null;
        launcher.SetupGet(value => value.DisplayName).Returns("launcher");
        launcher.SetupGet(value => value.Uid).Returns("launcher");
        launcher.Setup(value => value.LaunchTestHostAsync(It.IsAny<TestHostLaunchContext>(), It.IsAny<CancellationToken>()))
            .Callback<TestHostLaunchContext, CancellationToken>((context, _) =>
                expectedManifestPath = context.EnvironmentVariables["TESTINGPLATFORM_RETRY_RECOVERED_ARTIFACT_MANIFEST"])
            .ThrowsAsync(new InvalidOperationException("launch failed"));
        serviceProvider.AddService(launcher.Object);
        using var server = new RetryFailedTestsPipeServer(serviceProvider, [], Mock.Of<ILogger>());

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => RetryTestHostRunner.RunAttemptAsync(
            serviceProvider,
            Mock.Of<IOutputDeviceDataProducer>(),
            Mock.Of<IOutputDevice>(),
            Mock.Of<ILogger>(),
            server,
            new ExecutableInfo("testhost.exe", [], string.Empty),
            [],
            attemptCount: 1,
            userMaxRetryCount: 2,
            CancellationToken.None));

        Assert.IsNotNull(expectedManifestPath);
        fileSystem.Verify(fs => fs.DeleteFile(expectedManifestPath), Times.Once);
    }

    [TestMethod]
    public void CollectRecoveredArtifacts_MalformedAndMissingEntries_AreIgnoredAndManifestIsDeleted()
    {
        const string manifestPath = "recovered-artifacts.txt";
        const string missingArtifactPath = "missing.xml";
        string attemptDirectory = Path.GetFullPath("attempt");
        string manifest = $"not-base64\t-{Environment.NewLine}{CreateManifestLine(missingArtifactPath, "microsoft.testing.junit")}";
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(fs => fs.ExistFile(It.IsAny<string>()))
            .Returns<string>(path => path == manifestPath);
        fileSystem.Setup(fs => fs.NewFileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            .Returns(new ReadOnlyMemoryFileStream(manifest));
        List<ArtifactRequest> artifacts = [];
        Mock<ILogger> logger = CreateRecordingLogger();

        InvokeCollectRecoveredArtifacts(fileSystem.Object, manifestPath, attemptDirectory, artifacts, logger.Object);

        Assert.IsEmpty(artifacts);
        fileSystem.Verify(fs => fs.DeleteFile(manifestPath), Times.Once);
        logger.Verify(
            value => value.Log(
                LogLevel.Warning,
                It.Is<string>(message =>
                    message.StartsWith(
                        $"Ignoring malformed recovered retry artifact manifest entry in '{manifestPath}':",
                        StringComparison.Ordinal)),
                null,
                LoggingExtensions.Formatter),
            Times.Once);
    }

    [TestMethod]
    public void CollectRecoveredArtifacts_RecoveredKind_ReplacesPreviouslyPublishedArtifact()
    {
        const string manifestPath = "recovered-artifacts.txt";
        string attemptDirectory = Path.GetFullPath("attempt");
        string recoveredArtifactPath = Path.Combine(attemptDirectory, "recovered.xml");
        const string kind = "microsoft.testing.junit";
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(fs => fs.ExistFile(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(fs => fs.NewFileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            .Returns(new ReadOnlyMemoryFileStream(CreateManifestLine(recoveredArtifactPath, kind)));
        List<ArtifactRequest> artifacts = [new("original.xml", kind)];

        InvokeCollectRecoveredArtifacts(fileSystem.Object, manifestPath, attemptDirectory, artifacts);

        ArtifactRequest artifact = Assert.ContainsSingle(artifacts);
        Assert.AreEqual(recoveredArtifactPath, artifact.Path);
        Assert.AreEqual(kind, artifact.Kind);
        fileSystem.Verify(fs => fs.DeleteFile(manifestPath), Times.Once);
    }

    [TestMethod]
    public void CollectRecoveredArtifacts_NullKind_AddsRecoveredArtifact()
    {
        const string manifestPath = "recovered-artifacts.txt";
        string attemptDirectory = Path.GetFullPath("attempt");
        string recoveredArtifactPath = Path.Combine(attemptDirectory, "recovered.xml");
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(fs => fs.ExistFile(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(fs => fs.NewFileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            .Returns(new ReadOnlyMemoryFileStream(CreateManifestLine(recoveredArtifactPath, kind: null)));
        List<ArtifactRequest> artifacts = [];

        InvokeCollectRecoveredArtifacts(fileSystem.Object, manifestPath, attemptDirectory, artifacts);

        ArtifactRequest artifact = Assert.ContainsSingle(artifacts);
        Assert.AreEqual(recoveredArtifactPath, artifact.Path);
        Assert.IsNull(artifact.Kind);
        fileSystem.Verify(fs => fs.DeleteFile(manifestPath), Times.Once);
    }

    [TestMethod]
    public void RetryArtifactManifest_WriteEntry_RoundTripsNonNullKind()
    {
        const string path = "report\t✓.xml";
        const string kind = "microsoft.testing\tjunit✓";

        string entry = InvokeWriteManifestEntry(path, kind);

        Assert.HasCount(2, entry.Split('\t'));
        (string encodedPath, string encodedKindOrNullSentinel) = SplitManifestEntry(entry);
        Assert.AreEqual(path, DecodeManifestPath(encodedPath));
        Assert.AreEqual(kind, DecodeManifestKind(encodedKindOrNullSentinel));
    }

    [TestMethod]
    public void RetryArtifactManifest_WriteEntry_RoundTripsNullKind()
    {
        const string path = "report.xml";

        string entry = InvokeWriteManifestEntry(path, kind: null);

        Assert.HasCount(2, entry.Split('\t'));
        (string encodedPath, string encodedKindOrNullSentinel) = SplitManifestEntry(entry);
        Assert.AreEqual(path, DecodeManifestPath(encodedPath));
        Assert.AreEqual("-", encodedKindOrNullSentinel);
        Assert.IsNull(DecodeManifestKind(encodedKindOrNullSentinel));
    }

    [TestMethod]
    public void RetryArtifactManifest_WriteEntryWithEncodedKind_RoundTripsEncodedKind()
    {
        const string path = "report.xml";
        const string kind = "microsoft.testing.junit";
        string encodedKind = Convert.ToBase64String(Encoding.UTF8.GetBytes(kind));

        string entry = InvokeWriteManifestEntryWithEncodedKind(path, encodedKind);

        Assert.HasCount(2, entry.Split('\t'));
        (string encodedPath, string encodedKindOrNullSentinel) = SplitManifestEntry(entry);
        Assert.AreEqual(encodedKind, encodedKindOrNullSentinel);
        Assert.AreEqual(path, DecodeManifestPath(encodedPath));
        Assert.AreEqual(kind, DecodeManifestKind(encodedKindOrNullSentinel));
    }

    [TestMethod]
    public void CollectRecoveredArtifacts_OversizedLine_IsRejectedAndManifestIsDeleted()
    {
        const string manifestPath = "recovered-artifacts.txt";
        string attemptDirectory = Path.GetFullPath("attempt");
        Type manifestType = typeof(RetryOrchestrator).Assembly
            .GetType("Microsoft.Testing.Extensions.RetryArtifactManifest")!;
        int maxLineBytes = (int)manifestType
            .GetField("MaxLineLength", BindingFlags.Static | BindingFlags.Public)!
            .GetRawConstantValue()!;
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(fs => fs.ExistFile(manifestPath)).Returns(true);
        fileSystem.Setup(fs => fs.NewFileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            .Returns(new ReadOnlyMemoryFileStream(new string('x', maxLineBytes + 1)));
        List<ArtifactRequest> artifacts = [];

        InvokeCollectRecoveredArtifacts(fileSystem.Object, manifestPath, attemptDirectory, artifacts);

        Assert.IsEmpty(artifacts);
        fileSystem.Verify(fs => fs.DeleteFile(manifestPath), Times.Once);
    }

    [TestMethod]
    public void CollectRecoveredArtifacts_RecordLimit_IsEnforcedAndManifestIsDeleted()
    {
        const string manifestPath = "recovered-artifacts.txt";
        string attemptDirectory = Path.GetFullPath("attempt");
        string recoveredArtifactPath = Path.Combine(attemptDirectory, "recovered.xml");
        Type manifestType = typeof(RetryOrchestrator).Assembly
            .GetType("Microsoft.Testing.Extensions.RetryArtifactManifest")!;
        int maxRecords = (int)manifestType
            .GetField("MaxRecords", BindingFlags.Static | BindingFlags.Public)!
            .GetRawConstantValue()!;
        var manifest = new StringBuilder();
        for (int i = 0; i < maxRecords; i++)
        {
            manifest.AppendLine("malformed");
        }

        manifest.AppendLine(CreateManifestLine(recoveredArtifactPath, "microsoft.testing.junit"));

        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(fs => fs.ExistFile(manifestPath)).Returns(true);
        fileSystem.Setup(fs => fs.ExistFile(recoveredArtifactPath)).Returns(true);
        fileSystem.Setup(fs => fs.NewFileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            .Returns(new ReadOnlyMemoryFileStream(manifest.ToString()));
        List<ArtifactRequest> artifacts = [];
        Mock<ILogger> logger = CreateRecordingLogger();

        InvokeCollectRecoveredArtifacts(fileSystem.Object, manifestPath, attemptDirectory, artifacts, logger.Object);

        Assert.IsEmpty(artifacts);
        fileSystem.Verify(fs => fs.DeleteFile(manifestPath), Times.Once);
        logger.Verify(
            value => value.Log(
                LogLevel.Warning,
                $"Stopped reading recovered retry artifact manifest '{manifestPath}' after the maximum of {maxRecords} records.",
                null,
                LoggingExtensions.Formatter),
            Times.Once);
    }

    [TestMethod]
    public void CollectRecoveredArtifacts_OutsideAttemptDirectory_IsRejected()
    {
        const string manifestPath = "recovered-artifacts.txt";
        string attemptDirectory = Path.GetFullPath("attempt");
        string externalArtifactPath = Path.GetFullPath(Path.Combine("outside", "recovered.xml"));
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(fs => fs.ExistFile(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(fs => fs.NewFileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            .Returns(new ReadOnlyMemoryFileStream(CreateManifestLine(externalArtifactPath, "microsoft.testing.junit")));
        List<ArtifactRequest> artifacts = [];
        Mock<ILogger> logger = CreateRecordingLogger();

        InvokeCollectRecoveredArtifacts(fileSystem.Object, manifestPath, attemptDirectory, artifacts, logger.Object);

        Assert.IsEmpty(artifacts);
        fileSystem.Verify(fs => fs.ExistFile(externalArtifactPath), Times.Never);
        fileSystem.Verify(fs => fs.DeleteFile(manifestPath), Times.Once);
        logger.Verify(
            value => value.Log(
                LogLevel.Warning,
                $"Ignoring recovered retry artifact '{externalArtifactPath}' because it is outside the retry attempt directory '{attemptDirectory}'.",
                null,
                LoggingExtensions.Formatter),
            Times.Once);
    }

    [TestMethod]
    public void CollectRecoveredArtifacts_LimitExceededStopsBeforeLaterValidRecordAndLogsExactWarning()
    {
        const string ManifestPath = "recovered-artifacts.txt";
        string attemptDirectory = Path.GetFullPath("attempt");
        string recoveredArtifactPath = Path.Combine(attemptDirectory, "recovered.xml");
        Type manifestType = GetRetryArtifactManifestType();
        int maxLineLength = (int)manifestType
            .GetField("MaxLineLength", BindingFlags.Static | BindingFlags.Public)!
            .GetRawConstantValue()!;
        string manifest = new string('x', maxLineLength + 1)
            + Environment.NewLine
            + CreateManifestLine(recoveredArtifactPath, "report");
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(fs => fs.ExistFile(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(fs => fs.NewFileStream(ManifestPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            .Returns(new ReadOnlyMemoryFileStream(manifest));
        Mock<ILogger> logger = CreateRecordingLogger();
        List<ArtifactRequest> artifacts = [];

        InvokeCollectRecoveredArtifacts(fileSystem.Object, ManifestPath, attemptDirectory, artifacts, logger.Object);

        Assert.IsEmpty(artifacts);
        logger.Verify(
            value => value.Log(
                LogLevel.Warning,
                $"Stopped reading recovered retry artifact manifest '{ManifestPath}' because it exceeded a configured size limit.",
                null,
                LoggingExtensions.Formatter),
            Times.Once);
    }

    [TestMethod]
    public void CollectRecoveredArtifacts_MalformedRecordLogsOnceAndContinues()
    {
        const string ManifestPath = "recovered-artifacts.txt";
        string attemptDirectory = Path.GetFullPath("attempt");
        string recoveredArtifactPath = Path.Combine(attemptDirectory, "recovered.xml");
        string manifest = $"malformed{Environment.NewLine}{CreateManifestLine(recoveredArtifactPath, "report")}";
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(fs => fs.ExistFile(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(fs => fs.NewFileStream(ManifestPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            .Returns(new ReadOnlyMemoryFileStream(manifest));
        Mock<ILogger> logger = CreateRecordingLogger();
        List<ArtifactRequest> artifacts = [];

        InvokeCollectRecoveredArtifacts(fileSystem.Object, ManifestPath, attemptDirectory, artifacts, logger.Object);

        Assert.AreEqual(recoveredArtifactPath, Assert.ContainsSingle(artifacts).Path);
        logger.Verify(
            value => value.Log(
                LogLevel.Warning,
                $"Ignoring malformed recovered retry artifact manifest entry in '{ManifestPath}'.",
                null,
                LoggingExtensions.Formatter),
            Times.Once);
        logger.Verify(
            value => value.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<string>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<string, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public void CollectRecoveredArtifacts_InvalidBase64LogsTheDecodeFailure()
    {
        const string ManifestPath = "recovered-artifacts.txt";
        string attemptDirectory = Path.GetFullPath("attempt");
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(fs => fs.ExistFile(ManifestPath)).Returns(true);
        fileSystem.Setup(fs => fs.NewFileStream(ManifestPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            .Returns(new ReadOnlyMemoryFileStream("not-base64\t-"));
        List<string> messages = [];
        Mock<ILogger> logger = CreateRecordingLogger();
        logger
            .Setup(value => value.Log(
                LogLevel.Warning,
                It.IsAny<string>(),
                null,
                LoggingExtensions.Formatter))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>(
                (_, message, _, _) => messages.Add(message));

        InvokeCollectRecoveredArtifacts(
            fileSystem.Object,
            ManifestPath,
            attemptDirectory,
            [],
            logger.Object);

        string message = Assert.ContainsSingle(messages);
        Assert.StartsWith(
            $"Ignoring malformed recovered retry artifact manifest entry in '{ManifestPath}': ",
            message);
        Assert.IsGreaterThan(
            $"Ignoring malformed recovered retry artifact manifest entry in '{ManifestPath}': ".Length,
            message.Length);
    }

    [TestMethod]
    public void CollectRecoveredArtifacts_OversizedEntryUsesStrictIndependentLimits()
    {
        MethodInfo isOversized = typeof(RetryOrchestrator).GetMethod(
            "IsRecoveredArtifactEntryOversized",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.IsFalse((bool)isOversized.Invoke(null, [32 * 1024, 1024])!);
        Assert.IsTrue((bool)isOversized.Invoke(null, [(32 * 1024) + 1, null])!);
        Assert.IsTrue((bool)isOversized.Invoke(null, [1, 1025])!);

        MethodInfo tryAdvance = typeof(RetryOrchestrator).GetMethod(
            "TryAdvanceRecoveredArtifactManifestByteCount",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        long byteCount = (16L * 1024 * 1024) - 1;
        object?[] arguments = [byteCount];
        Assert.IsTrue((bool)tryAdvance.Invoke(null, arguments)!);
        byteCount = (long)arguments[0]!;
        Assert.AreEqual(16L * 1024 * 1024, byteCount);
        arguments[0] = byteCount;
        Assert.IsFalse((bool)tryAdvance.Invoke(null, arguments)!);
        byteCount = (long)arguments[0]!;
        Assert.AreEqual(16L * 1024 * 1024, byteCount);
    }

    [TestMethod]
    public void BoundedManifestLineReader_TrimsCarriageReturnAndReturnsExactTerminalStates()
    {
        Type readerType = typeof(RetryOrchestrator).GetNestedType(
            "BoundedManifestLineReader",
            BindingFlags.NonPublic)!;
        MethodInfo readLine = readerType.GetMethod("ReadLine", BindingFlags.Instance | BindingFlags.Public)!;
        object reader = Activator.CreateInstance(
            readerType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [new MemoryStream(Encoding.UTF8.GetBytes("first\r\nsecond"))],
            culture: CultureInfo.InvariantCulture)!;

        AssertReadLine(readLine, reader, "Line", "first");
        AssertReadLine(readLine, reader, "Line", "second");
        AssertReadLine(readLine, reader, "End", string.Empty);

        object emptyLineReader = Activator.CreateInstance(
            readerType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [new MemoryStream([(byte)'\n'])],
            culture: CultureInfo.InvariantCulture)!;
        AssertReadLine(readLine, emptyLineReader, "Line", string.Empty);
    }

    [TestMethod]
    public void RemoveArtifactsOutsideControllerRoots_AppContainerMappings_RejectOutsidePaths()
    {
        string artifactRoot = Path.GetFullPath("controller-results");
        string diagnosticArtifactRoot = Path.GetFullPath("controller-diagnostics");
        var environment = new Mock<IEnvironment>();
        environment
            .Setup(x => x.GetEnvironmentVariable("TESTINGPLATFORM_PACKAGEDAPP_APPCONTAINER_ARTIFACT_ROOTS_CONFIGURED"))
            .Returns("1");
        environment
            .Setup(x => x.GetEnvironmentVariable("TESTINGPLATFORM_ARTIFACT_PATH_DESTINATION_ROOT"))
            .Returns(artifactRoot);
        environment
            .Setup(x => x.GetEnvironmentVariable("TESTINGPLATFORM_DIAGNOSTIC_ARTIFACT_PATH_DESTINATION_ROOT"))
            .Returns(diagnosticArtifactRoot);
        List<ArtifactRequest> artifacts =
        [
            new(Path.Combine(artifactRoot, "test.trx"), "microsoft.testing.trx"),
            new(Path.Combine(diagnosticArtifactRoot, "test.log"), null),
            new(Path.GetFullPath(Path.Combine("outside", "secret.txt")), null),
        ];

        InvokeRemoveArtifactsOutsideControllerRoots(environment.Object, artifacts);

        Assert.HasCount(2, artifacts);
        Assert.IsTrue(artifacts.All(artifact =>
            artifact.Path.StartsWith(artifactRoot, StringComparison.Ordinal)
            || artifact.Path.StartsWith(diagnosticArtifactRoot, StringComparison.Ordinal)));
    }

    [TestMethod]
    public void RemoveArtifactsOutsideControllerRoots_EmptyRootsRejectSingleArtifactAtIndexZero()
    {
        var environment = new Mock<IEnvironment>();
        environment
            .Setup(x => x.GetEnvironmentVariable("TESTINGPLATFORM_PACKAGEDAPP_APPCONTAINER_ARTIFACT_ROOTS_CONFIGURED"))
            .Returns("1");
        environment
            .Setup(x => x.GetEnvironmentVariable("TESTINGPLATFORM_ARTIFACT_PATH_DESTINATION_ROOT"))
            .Returns(string.Empty);
        environment
            .Setup(x => x.GetEnvironmentVariable("TESTINGPLATFORM_DIAGNOSTIC_ARTIFACT_PATH_DESTINATION_ROOT"))
            .Returns(string.Empty);
        List<ArtifactRequest> artifacts = [new(Path.GetFullPath("outside.txt"), null)];
        Mock<ILogger> logger = CreateRecordingLogger();

        InvokeRemoveArtifactsOutsideControllerRoots(environment.Object, artifacts, logger.Object);

        Assert.IsEmpty(artifacts);
        logger.Verify(
            value => value.Log(
                LogLevel.Warning,
                It.Is<string>(message => message.Contains("outside.txt", StringComparison.Ordinal)),
                null,
                LoggingExtensions.Formatter),
            Times.Once);
    }

    [TestMethod]
    public void SnapshotAttemptArtifacts_CopiesExternalArtifactAndPreservesDestination()
    {
        string retryRoot = Path.GetFullPath(Path.Combine("TR", "Retries", "abcde"));
        string attemptDirectory = Path.Combine(retryRoot, "1");
        string externalPath = Path.GetFullPath(Path.Combine("custom", "report.ctrf.json"));
        string expectedSnapshot = Path.Combine(retryRoot, "Artifacts", "1", "0000-report.ctrf.json");
        var fileSystem = new Mock<IFileSystem>();

        IReadOnlyList<RetryAttemptArtifact> captured = RetryArtifactProcessor.SnapshotAttemptArtifacts(
            fileSystem.Object,
            [new ArtifactRequest(externalPath, "microsoft.testing.ctrf")],
            attempt: 1,
            attemptDirectory,
            retryRoot);

        RetryAttemptArtifact artifact = Assert.ContainsSingle(captured);
        Assert.AreEqual(expectedSnapshot, artifact.Path);
        Assert.AreEqual(externalPath, artifact.DestinationPath);
        Assert.AreEqual(1, artifact.Attempt);
        fileSystem.Verify(fs => fs.CreateDirectory(Path.Combine(retryRoot, "Artifacts", "1")), Times.Once);
        fileSystem.Verify(fs => fs.CopyFile(externalPath, expectedSnapshot, overwrite: true), Times.Once);
    }

    [TestMethod]
    public void SnapshotAttemptArtifacts_MultipleExternalArtifactsCreateSnapshotDirectoryOnce()
    {
        string retryRoot = Path.GetFullPath(Path.Combine("TR", "Retries", "abcde"));
        string attemptDirectory = Path.Combine(retryRoot, "1");
        string firstExternal = Path.GetFullPath(Path.Combine("custom", "first.xml"));
        string secondExternal = Path.GetFullPath(Path.Combine("custom", "second.xml"));
        var fileSystem = new Mock<IFileSystem>();

        IReadOnlyList<RetryAttemptArtifact> captured = RetryArtifactProcessor.SnapshotAttemptArtifacts(
            fileSystem.Object,
            [new ArtifactRequest(firstExternal, "first"), new ArtifactRequest(secondExternal, "second")],
            attempt: 1,
            attemptDirectory,
            retryRoot);

        Assert.HasCount(2, captured);
        fileSystem.Verify(
            fs => fs.CreateDirectory(Path.Combine(retryRoot, "Artifacts", "1")),
            Times.Once);
        fileSystem.Verify(
            fs => fs.CopyFile(It.IsAny<string>(), It.IsAny<string>(), overwrite: true),
            Times.Exactly(2));
    }

    [TestMethod]
    public void SnapshotAttemptArtifacts_LeavesAttemptArtifactInPlace()
    {
        string retryRoot = Path.GetFullPath(Path.Combine("TR", "Retries", "abcde"));
        string attemptDirectory = Path.Combine(retryRoot, "1");
        string artifactPath = Path.Combine(attemptDirectory, "report.ctrf.json");
        var fileSystem = new Mock<IFileSystem>();

        RetryAttemptArtifact captured = Assert.ContainsSingle(RetryArtifactProcessor.SnapshotAttemptArtifacts(
            fileSystem.Object,
            [new ArtifactRequest(artifactPath, "microsoft.testing.ctrf")],
            attempt: 1,
            attemptDirectory,
            retryRoot));

        Assert.AreEqual(Path.GetFullPath(artifactPath), captured.Path);
        Assert.IsNull(captured.DestinationPath);
        fileSystem.Verify(fs => fs.CreateDirectory(It.IsAny<string>()), Times.Never);
        fileSystem.Verify(fs => fs.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    public void PublishExternalArtifacts_UsesMergedReplacement()
    {
        string snapshotPath = Path.GetFullPath(Path.Combine("TR", "Retries", "abcde", "Artifacts", "2", "report.ctrf.json"));
        string destinationPath = Path.GetFullPath(Path.Combine("custom", "report.ctrf.json"));
        string replacementPath = Path.GetFullPath(Path.Combine("temp", "merged.ctrf.json"));
        var fileSystem = new Mock<IFileSystem>();
        var artifact = new RetryAttemptArtifact(snapshotPath, "microsoft.testing.ctrf", attempt: 2, destinationPath);

        RetryArtifactProcessor.PublishExternalArtifacts(
            fileSystem.Object,
            [artifact],
            finalAttempt: 2,
            new Dictionary<string, string> { [snapshotPath] = replacementPath });

        fileSystem.Verify(fs => fs.CreateDirectory(Path.GetDirectoryName(destinationPath)!), Times.Once);
        fileSystem.Verify(fs => fs.CopyFile(replacementPath, destinationPath, overwrite: true), Times.Once);
    }

    [TestMethod]
    public void PublishExternalArtifacts_UsesFinalSnapshotWhenNoReplacementExists()
    {
        string finalSnapshotPath = Path.GetFullPath(Path.Combine("TR", "Retries", "abcde", "Artifacts", "2", "report.ctrf.json"));
        string previousSnapshotPath = Path.GetFullPath(Path.Combine("TR", "Retries", "abcde", "Artifacts", "1", "report.ctrf.json"));
        string destinationPath = Path.GetFullPath(Path.Combine("custom", "report.ctrf.json"));
        var fileSystem = new Mock<IFileSystem>();

        RetryArtifactProcessor.PublishExternalArtifacts(
            fileSystem.Object,
            [
                new RetryAttemptArtifact(previousSnapshotPath, "microsoft.testing.ctrf", attempt: 1, destinationPath),
                new RetryAttemptArtifact(finalSnapshotPath, "microsoft.testing.ctrf", attempt: 2, destinationPath),
                new RetryAttemptArtifact("internal.ctrf.json", "microsoft.testing.ctrf", attempt: 2, destinationPath: null),
            ],
            finalAttempt: 2,
            new Dictionary<string, string>());

        fileSystem.Verify(fs => fs.CopyFile(finalSnapshotPath, destinationPath, overwrite: true), Times.Once);
        fileSystem.Verify(fs => fs.CopyFile(previousSnapshotPath, It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        fileSystem.Verify(fs => fs.CopyFile("internal.ctrf.json", It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    public async Task MoveArtifactsAsync_NormalizesRelativePathsBeforeApplyingReplacement()
    {
        string currentAttemptDirectory = Path.Combine("TR", "Retries", "abcde", "2");
        string relativeAttemptFile = Path.Combine(currentAttemptDirectory, "report.xml");
        string replacementFile = Path.GetFullPath(Path.Combine("merged", "report.xml"));
        var fileSystem = new Mock<IFileSystem>();
        fileSystem
            .Setup(fs => fs.GetFiles(currentAttemptDirectory, "*.*", SearchOption.AllDirectories))
            .Returns([relativeAttemptFile]);
        var outputDevice = new Mock<IOutputDevice>();
        var displayCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        IOutputDeviceData? displayedData = null;
        outputDevice
            .Setup(device => device.DisplayAsync(
                It.IsAny<IOutputDeviceDataProducer>(),
                It.IsAny<IOutputDeviceData>(),
                It.IsAny<CancellationToken>()))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>(
                (_, data, _) => displayedData = data)
            .Returns(displayCompletion.Task);
        Mock<ILogger> logger = CreateRecordingLogger();

        Task moveTask = RetrySummaryReporter.MoveArtifactsAsync(
            new Mock<IOutputDeviceDataProducer>().Object,
            outputDevice.Object,
            fileSystem.Object,
            logger.Object,
            currentAttemptDirectory,
            "TR",
            new Dictionary<string, string>
            {
                [Path.GetFullPath(relativeAttemptFile)] = replacementFile,
            },
            CancellationToken.None);
        Assert.IsFalse(moveTask.IsCompleted);

        displayCompletion.SetResult(true);
        await moveTask;

        fileSystem.Verify(
            fs => fs.CopyFile(replacementFile, Path.Combine("TR", "report.xml"), overwrite: true),
            Times.Once);
        fileSystem.Verify(fs => fs.CreateDirectory("TR"), Times.Once);
        logger.Verify(
            value => value.LogAsync(
                LogLevel.Debug,
                $"Copying file '{replacementFile}' to '{Path.Combine("TR", "report.xml")}'",
                null,
                LoggingExtensions.Formatter),
            Times.Once);
        FormattedTextOutputDeviceData published = Assert.IsInstanceOfType<FormattedTextOutputDeviceData>(displayedData);
        SystemConsoleColor color = Assert.IsInstanceOfType<SystemConsoleColor>(published.ForegroundColor);
        Assert.AreEqual(ConsoleColor.DarkGray, color.ConsoleColor);
    }

    [TestMethod]
    public async Task ProcessAsync_ProcessorWithoutRetryMode_IsNotCalled()
    {
        var processor = new TestArtifactPostProcessor(
            supportedModes: [ArtifactPostProcessingMode.TestModules],
            supportedKinds: ["report"],
            supportedExtensions: []);
        ServiceProvider serviceProvider = CreateServiceProvider(processor);

        IReadOnlyDictionary<string, string> replacements = await RetryArtifactProcessor.ProcessAsync(
            serviceProvider,
            Mock.Of<IOutputDeviceDataProducer>(),
            Mock.Of<IOutputDevice>(),
            Mock.Of<ILogger>(),
            [
                new RetryAttemptArtifact("first.report", "report", attempt: 1, destinationPath: null),
                new RetryAttemptArtifact("second.report", "report", attempt: 2, destinationPath: null),
            ],
            attemptCount: 2,
            Path.GetTempPath(),
            CancellationToken.None);

        Assert.IsEmpty(replacements);
        Assert.AreEqual(0, processor.ProcessCallCount);
    }

    [TestMethod]
    public async Task ProcessAsync_SingleAttempt_IsNotProcessed()
    {
        var processor = new TestArtifactPostProcessor(
            supportedModes: [ArtifactPostProcessingMode.RetryAttempts],
            supportedKinds: ["report"],
            supportedExtensions: []);
        ServiceProvider serviceProvider = CreateServiceProvider(processor);

        IReadOnlyDictionary<string, string> replacements = await RetryArtifactProcessor.ProcessAsync(
            serviceProvider,
            Mock.Of<IOutputDeviceDataProducer>(),
            Mock.Of<IOutputDevice>(),
            Mock.Of<ILogger>(),
            [new RetryAttemptArtifact("first.report", "report", attempt: 1, destinationPath: null)],
            attemptCount: 1,
            Path.GetTempPath(),
            CancellationToken.None);

        Assert.IsEmpty(replacements);
        Assert.AreEqual(0, processor.ProcessCallCount);
    }

    [TestMethod]
    public async Task ProcessAsync_CompleteArtifacts_UsesAttemptOrderAndMapsFinalArtifact()
    {
        string outputDirectory = CreateTemporaryDirectory();
        try
        {
            string firstPath = Path.Combine(outputDirectory, "attempt-1.report");
            string finalPath = Path.Combine(outputDirectory, "attempt-2.report");
            string mergedPath = Path.Combine(outputDirectory, "merged.report");
            IReadOnlyList<InputArtifact>? capturedInputs = null;
            ArtifactPostProcessingContext? capturedContext = null;
            CancellationToken capturedCancellationToken = default;
            string? capturedOutputDirectory = null;
            var processor = new TestArtifactPostProcessor(
                supportedModes: [ArtifactPostProcessingMode.RetryAttempts],
                supportedKinds: ["report"],
                supportedExtensions: [],
                (inputs, directory, context, cancellationToken) =>
                {
                    capturedInputs = inputs;
                    capturedOutputDirectory = directory;
                    capturedContext = context;
                    capturedCancellationToken = cancellationToken;
                    File.WriteAllText(mergedPath, "merged");
                    return Task.FromResult<ProcessedArtifact?>(new ProcessedArtifact(
                        mergedPath,
                        "report",
                        "Merged report",
                        description: null));
                });
            ServiceProvider serviceProvider = CreateServiceProvider(processor);
            var cancellationToken = new CancellationToken(canceled: false);
            var runSummary = new ArtifactPostProcessingRunSummary(
                totalTests: 3,
                passedTests: 3,
                failedTests: 0,
                skippedTests: 0,
                duration: TimeSpan.FromSeconds(1),
                exitCode: 0,
                testModuleCount: 1);

            IReadOnlyDictionary<string, string> replacements = await RetryArtifactProcessor.ProcessAsync(
                serviceProvider,
                Mock.Of<IOutputDeviceDataProducer>(),
                Mock.Of<IOutputDevice>(),
                Mock.Of<ILogger>(),
                [
                    new RetryAttemptArtifact(finalPath, "report", attempt: 2, destinationPath: null),
                    new RetryAttemptArtifact(firstPath, "report", attempt: 1, destinationPath: null),
                ],
                attemptCount: 2,
                runSummary,
                outputDirectory,
                cancellationToken);

            Assert.HasCount(1, replacements);
            Assert.AreEqual(Path.GetFullPath(mergedPath), replacements[finalPath]);
            Assert.IsNotNull(capturedInputs);
            Assert.AreSequenceEqual([firstPath, finalPath], capturedInputs.Select(input => input.Path));
            Assert.AreSequenceEqual(["report", "report"], capturedInputs.Select(input => input.Kind));
            Assert.AreSequenceEqual(["1", "2"], capturedInputs.Select(input => input.ExecutionId));
            Assert.IsTrue(capturedInputs.All(input =>
                input.ProducingTestModule is null
                && input.TargetFramework is null
                && input.Architecture is null));
            Assert.AreEqual(outputDirectory, capturedOutputDirectory);
            Assert.IsNotNull(capturedContext);
            Assert.AreEqual(ArtifactPostProcessingMode.RetryAttempts, capturedContext.Mode);
            Assert.AreEqual(ArtifactPostProcessingTruncationReason.None, capturedContext.TruncationReason);
            Assert.AreSame(runSummary, capturedContext.RunSummary);
            Assert.AreEqual(cancellationToken, capturedCancellationToken);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [DataRow("report", "attempt.report", true)]
    [DataRow(null, "attempt.REPORT", true)]
    [DataRow(null, "attempt.txt", false)]
    [DataRow("other", "attempt.report", false)]
    [DataRow("REPORT", "attempt.report", false)]
    [TestMethod]
    public async Task ProcessAsync_MatchesProducerKindOrUntaggedExtension(
        string? artifactKind,
        string fileName,
        bool shouldProcess)
    {
        string outputDirectory = CreateTemporaryDirectory();
        try
        {
            string mergedPath = Path.Combine(outputDirectory, "merged.report");
            var processor = new TestArtifactPostProcessor(
                supportedModes: [ArtifactPostProcessingMode.RetryAttempts],
                supportedKinds: ["report"],
                supportedExtensions: [".report"],
                (_, _, _, _) =>
                {
                    File.WriteAllText(mergedPath, "merged");
                    return Task.FromResult<ProcessedArtifact?>(new ProcessedArtifact(
                        mergedPath,
                        "report",
                        "Merged report",
                        description: null));
                });
            ServiceProvider serviceProvider = CreateServiceProvider(processor);
            string firstPath = Path.Combine(outputDirectory, "1", fileName);
            string finalPath = Path.Combine(outputDirectory, "2", fileName);

            IReadOnlyDictionary<string, string> replacements = await RetryArtifactProcessor.ProcessAsync(
                serviceProvider,
                Mock.Of<IOutputDeviceDataProducer>(),
                Mock.Of<IOutputDevice>(),
                Mock.Of<ILogger>(),
                [
                    new RetryAttemptArtifact(firstPath, artifactKind, attempt: 1, destinationPath: null),
                    new RetryAttemptArtifact(finalPath, artifactKind, attempt: 2, destinationPath: null),
                ],
                attemptCount: 2,
                outputDirectory,
                CancellationToken.None);

            Assert.AreEqual(shouldProcess ? 1 : 0, processor.ProcessCallCount);
            Assert.AreEqual(shouldProcess, replacements.ContainsKey(finalPath));
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_ReplacementLookupUsesPlatformPathComparison()
    {
        string outputDirectory = CreateTemporaryDirectory();
        try
        {
            string finalPath = Path.Combine(outputDirectory, "Attempt-2.report");
            string mergedPath = Path.Combine(outputDirectory, "merged.report");
            var processor = new TestArtifactPostProcessor(
                supportedModes: [ArtifactPostProcessingMode.RetryAttempts],
                supportedKinds: ["report"],
                supportedExtensions: [],
                (_, _, _, _) =>
                {
                    File.WriteAllText(mergedPath, "merged");
                    return Task.FromResult<ProcessedArtifact?>(new ProcessedArtifact(
                        mergedPath,
                        "report",
                        "Merged report",
                        description: null));
                });

            IReadOnlyDictionary<string, string> replacements = await RetryArtifactProcessor.ProcessAsync(
                CreateServiceProvider(processor),
                Mock.Of<IOutputDeviceDataProducer>(),
                Mock.Of<IOutputDevice>(),
                Mock.Of<ILogger>(),
                [
                    new RetryAttemptArtifact(Path.Combine(outputDirectory, "attempt-1.report"), "report", attempt: 1, destinationPath: null),
                    new RetryAttemptArtifact(finalPath, "report", attempt: 2, destinationPath: null),
                ],
                attemptCount: 2,
                outputDirectory,
                CancellationToken.None);

            Assert.AreEqual(
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
                replacements.ContainsKey(finalPath.ToUpperInvariant()));
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_IncompleteOrDuplicateAttemptArtifacts_AreNotProcessed()
    {
        var processor = new TestArtifactPostProcessor(
            supportedModes: [ArtifactPostProcessingMode.RetryAttempts],
            supportedKinds: ["report"],
            supportedExtensions: []);
        ServiceProvider serviceProvider = CreateServiceProvider(processor);

        IReadOnlyDictionary<string, string> missingAttemptReplacements = await RetryArtifactProcessor.ProcessAsync(
            serviceProvider,
            Mock.Of<IOutputDeviceDataProducer>(),
            Mock.Of<IOutputDevice>(),
            Mock.Of<ILogger>(),
            [new RetryAttemptArtifact("attempt-1.report", "report", attempt: 1, destinationPath: null)],
            attemptCount: 2,
            Path.GetTempPath(),
            CancellationToken.None);
        IReadOnlyDictionary<string, string> duplicateAttemptReplacements = await RetryArtifactProcessor.ProcessAsync(
            serviceProvider,
            Mock.Of<IOutputDeviceDataProducer>(),
            Mock.Of<IOutputDevice>(),
            Mock.Of<ILogger>(),
            [
                new RetryAttemptArtifact("attempt-1-a.report", "report", attempt: 1, destinationPath: null),
                new RetryAttemptArtifact("attempt-1-b.report", "report", attempt: 1, destinationPath: null),
                new RetryAttemptArtifact("attempt-2.report", "report", attempt: 2, destinationPath: null),
            ],
            attemptCount: 2,
            Path.GetTempPath(),
            CancellationToken.None);

        Assert.IsEmpty(missingAttemptReplacements);
        Assert.IsEmpty(duplicateAttemptReplacements);
        Assert.AreEqual(0, processor.ProcessCallCount);
    }

    [TestMethod]
    public async Task ProcessAsync_ProcessorDeclinesMerge_ReturnsNoReplacement()
    {
        var processor = new TestArtifactPostProcessor(
            supportedModes: [ArtifactPostProcessingMode.RetryAttempts],
            supportedKinds: ["report"],
            supportedExtensions: []);
        ServiceProvider serviceProvider = CreateServiceProvider(processor);

        IReadOnlyDictionary<string, string> replacements = await RetryArtifactProcessor.ProcessAsync(
            serviceProvider,
            Mock.Of<IOutputDeviceDataProducer>(),
            Mock.Of<IOutputDevice>(),
            Mock.Of<ILogger>(),
            [
                new RetryAttemptArtifact("attempt-1.report", "report", attempt: 1, destinationPath: null),
                new RetryAttemptArtifact("attempt-2.report", "report", attempt: 2, destinationPath: null),
            ],
            attemptCount: 2,
            Path.GetTempPath(),
            CancellationToken.None);

        Assert.IsEmpty(replacements);
        Assert.AreEqual(1, processor.ProcessCallCount);
    }

    [TestMethod]
    public async Task ProcessAsync_ProcessorDeclinesMerge_DoesNotWarn()
    {
        var processor = new TestArtifactPostProcessor(
            supportedModes: [ArtifactPostProcessingMode.RetryAttempts],
            supportedKinds: ["report"],
            supportedExtensions: []);
        Mock<ILogger> logger = new();
        Mock<IOutputDevice> outputDevice = new();

        IReadOnlyDictionary<string, string> replacements = await RetryArtifactProcessor.ProcessAsync(
            CreateServiceProvider(processor),
            Mock.Of<IOutputDeviceDataProducer>(),
            outputDevice.Object,
            logger.Object,
            [
                new RetryAttemptArtifact("attempt-1.report", "report", attempt: 1, destinationPath: null),
                new RetryAttemptArtifact("attempt-2.report", "report", attempt: 2, destinationPath: null),
            ],
            attemptCount: 2,
            Path.GetTempPath(),
            CancellationToken.None);

        Assert.IsEmpty(replacements);
        logger.Verify(
            value => value.LogAsync(
                It.IsAny<LogLevel>(),
                It.IsAny<string>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<string, Exception?, string>>()),
            Times.Never);
        outputDevice.Verify(
            value => value.DisplayAsync(
                It.IsAny<IOutputDeviceDataProducer>(),
                It.IsAny<IOutputDeviceData>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ProcessAsync_ArtifactIsConsumedByOnlyTheFirstMatchingProcessor()
    {
        string outputDirectory = CreateTemporaryDirectory();
        try
        {
            string mergedPath = Path.Combine(outputDirectory, "merged.report");
            var first = new TestArtifactPostProcessor(
                supportedModes: [ArtifactPostProcessingMode.RetryAttempts],
                supportedKinds: ["report"],
                supportedExtensions: [],
                (_, _, _, _) =>
                {
                    File.WriteAllText(mergedPath, "merged");
                    return Task.FromResult<ProcessedArtifact?>(new ProcessedArtifact(
                        mergedPath,
                        "report",
                        "Merged",
                        description: null));
                },
                uid: "first");
            var second = new TestArtifactPostProcessor(
                supportedModes: [ArtifactPostProcessingMode.RetryAttempts],
                supportedKinds: ["report"],
                supportedExtensions: [],
                uid: "second");

            IReadOnlyDictionary<string, string> replacements = await RetryArtifactProcessor.ProcessAsync(
                CreateServiceProvider(first, second),
                Mock.Of<IOutputDeviceDataProducer>(),
                Mock.Of<IOutputDevice>(),
                Mock.Of<ILogger>(),
                [
                    new RetryAttemptArtifact("attempt-1.report", "report", attempt: 1, destinationPath: null),
                    new RetryAttemptArtifact("attempt-2.report", "report", attempt: 2, destinationPath: null),
                ],
                attemptCount: 2,
                outputDirectory,
                CancellationToken.None);

            Assert.HasCount(1, replacements);
            Assert.AreEqual(1, first.ProcessCallCount);
            Assert.AreEqual(0, second.ProcessCallCount);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_InvalidOutput_WarnsAndContinuesWithOtherKinds()
    {
        string outputDirectory = CreateTemporaryDirectory();
        try
        {
            string validMergedPath = Path.Combine(outputDirectory, "valid-merged.report");
            var invalidProcessor = new TestArtifactPostProcessor(
                uid: "invalid-processor",
                supportedModes: [ArtifactPostProcessingMode.RetryAttempts],
                supportedKinds: ["invalid"],
                supportedExtensions: [],
                processAsync: (_, _, _, _) => Task.FromResult<ProcessedArtifact?>(new ProcessedArtifact(
                    Path.Combine(outputDirectory, "missing.report"),
                    "invalid",
                    "Missing report",
                    description: null)));
            var validProcessor = new TestArtifactPostProcessor(
                uid: "valid-processor",
                supportedModes: [ArtifactPostProcessingMode.RetryAttempts],
                supportedKinds: ["valid"],
                supportedExtensions: [],
                processAsync: (_, _, _, _) =>
                {
                    File.WriteAllText(validMergedPath, "merged");
                    return Task.FromResult<ProcessedArtifact?>(new ProcessedArtifact(
                        validMergedPath,
                        "valid",
                        "Merged report",
                        description: null));
                });
            ServiceProvider serviceProvider = CreateServiceProvider(invalidProcessor, validProcessor);
            var logger = new Mock<ILogger>();
            var outputDevice = new Mock<IOutputDevice>();
            var displayed = new List<IOutputDeviceData>();
            outputDevice
                .Setup(device => device.DisplayAsync(
                    It.IsAny<IOutputDeviceDataProducer>(),
                    It.IsAny<IOutputDeviceData>(),
                    It.IsAny<CancellationToken>()))
                .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>(
                    (_, data, _) => displayed.Add(data))
                .Returns(Task.CompletedTask);
            string validFinalPath = Path.Combine(outputDirectory, "valid-2.report");

            IReadOnlyDictionary<string, string> replacements = await RetryArtifactProcessor.ProcessAsync(
                serviceProvider,
                Mock.Of<IOutputDeviceDataProducer>(),
                outputDevice.Object,
                logger.Object,
                [
                    new RetryAttemptArtifact("invalid-1.report", "invalid", attempt: 1, destinationPath: null),
                    new RetryAttemptArtifact("invalid-2.report", "invalid", attempt: 2, destinationPath: null),
                    new RetryAttemptArtifact("valid-1.report", "valid", attempt: 1, destinationPath: null),
                    new RetryAttemptArtifact(validFinalPath, "valid", attempt: 2, destinationPath: null),
                ],
                attemptCount: 2,
                outputDirectory,
                CancellationToken.None);

            Assert.HasCount(1, replacements);
            Assert.AreEqual(Path.GetFullPath(validMergedPath), replacements[validFinalPath]);
            WarningMessageOutputDeviceData warning = Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(
                Assert.ContainsSingle(displayed));
            Assert.Contains("invalid-processor", warning.Message);
            logger.Verify(
                logger => logger.LogAsync(
                    LogLevel.Warning,
                    It.Is<string>(message =>
                        message.Contains("invalid-processor", StringComparison.Ordinal)
                        && message.Contains(nameof(InvalidOperationException), StringComparison.Ordinal)),
                    null,
                    It.IsAny<Func<string, Exception?, string>>()),
                Times.Once);
            Assert.AreEqual(1, invalidProcessor.ProcessCallCount);
            Assert.AreEqual(1, validProcessor.ProcessCallCount);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_CanceledProcessor_PropagatesWithoutWarning()
    {
        var cancellationToken = new CancellationToken(canceled: true);
        var processor = new TestArtifactPostProcessor(
            supportedModes: [ArtifactPostProcessingMode.RetryAttempts],
            supportedKinds: ["report"],
            supportedExtensions: [],
            (_, _, _, _) => throw new OperationCanceledException(cancellationToken));
        ServiceProvider serviceProvider = CreateServiceProvider(processor);
        var logger = new Mock<ILogger>();
        var outputDevice = new Mock<IOutputDevice>();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => RetryArtifactProcessor.ProcessAsync(
            serviceProvider,
            Mock.Of<IOutputDeviceDataProducer>(),
            outputDevice.Object,
            logger.Object,
            [
                new RetryAttemptArtifact("attempt-1.report", "report", attempt: 1, destinationPath: null),
                new RetryAttemptArtifact("attempt-2.report", "report", attempt: 2, destinationPath: null),
            ],
            attemptCount: 2,
            Path.GetTempPath(),
            cancellationToken));

        logger.Verify(
            logger => logger.LogAsync(
                It.IsAny<LogLevel>(),
                It.IsAny<string>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<string, Exception?, string>>()),
            Times.Never);
        outputDevice.Verify(
            device => device.DisplayAsync(
                It.IsAny<IOutputDeviceDataProducer>(),
                It.IsAny<IOutputDeviceData>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ProcessAsync_UnrelatedCancellation_WarnsWithoutPropagating()
    {
        const string ProcessorUid = "canceling-processor";
        const string ExceptionMessage = "The processor canceled its own operation.";
        var processor = new TestArtifactPostProcessor(
            uid: ProcessorUid,
            supportedModes: [ArtifactPostProcessingMode.RetryAttempts],
            supportedKinds: ["report"],
            supportedExtensions: [],
            processAsync: (_, _, _, _) => throw new OperationCanceledException(ExceptionMessage));
        var logger = new Mock<ILogger>();
        var outputDevice = new Mock<IOutputDevice>();
        var producer = new Mock<IOutputDeviceDataProducer>();
        WarningMessageOutputDeviceData? displayedWarning = null;
        outputDevice
            .Setup(device => device.DisplayAsync(
                producer.Object,
                It.IsAny<WarningMessageOutputDeviceData>(),
                CancellationToken.None))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>(
                (_, data, _) => displayedWarning = (WarningMessageOutputDeviceData)data)
            .Returns(Task.CompletedTask);

        IReadOnlyDictionary<string, string> replacements = await RetryArtifactProcessor.ProcessAsync(
            CreateServiceProvider(processor),
            producer.Object,
            outputDevice.Object,
            logger.Object,
            [
                new RetryAttemptArtifact("attempt-1.report", "report", attempt: 1, destinationPath: null),
                new RetryAttemptArtifact("attempt-2.report", "report", attempt: 2, destinationPath: null),
            ],
            attemptCount: 2,
            Path.GetTempPath(),
            CancellationToken.None);

        Assert.IsEmpty(replacements);
        Assert.IsNotNull(displayedWarning);
        Assert.AreEqual(
            string.Format(
                CultureInfo.CurrentCulture,
                Policy.Resources.ExtensionResources.RetryArtifactPostProcessorFailed,
                ProcessorUid,
                ExceptionMessage),
            displayedWarning.Message);
        logger.Verify(
            logger => logger.LogAsync(
                LogLevel.Warning,
                It.Is<string>(message =>
                    message.Contains(ProcessorUid, StringComparison.Ordinal)
                    && message.Contains(nameof(OperationCanceledException), StringComparison.Ordinal)
                    && message.Contains(ExceptionMessage, StringComparison.Ordinal)),
                null,
                It.IsAny<Func<string, Exception?, string>>()),
            Times.Once);
        outputDevice.Verify(
            device => device.DisplayAsync(
                producer.Object,
                It.IsAny<WarningMessageOutputDeviceData>(),
                CancellationToken.None),
            Times.Once);
    }

    [TestMethod]
    public void GetCommandLineOptions_PublicRetryOptions_AreExtensionOptions()
    {
        var provider = new RetryCommandLineOptionsProvider();

        CommandLineOption[] publicOptions = [.. provider.GetCommandLineOptions().Where(option => !option.IsHidden)];
        Assert.AreSequenceEqual(
            [
                RetryCommandLineOptionsProvider.RetryFailedTestsOptionName,
                RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName,
                RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName,
                RetryCommandLineOptionsProvider.RetryFailedTestsDelayOptionName,
            ],
            publicOptions.Select(option => option.Name));
        foreach (CommandLineOption option in publicOptions)
        {
            Assert.IsFalse(option.IsBuiltIn, option.Name);
        }

        Assert.AreEqual("RetryCommandLineOptionsProvider", provider.Uid);
    }

    [TestMethod]
    public void GetCommandLineOptions_InternalRetryOption_RemainsBuiltIn()
    {
        var provider = new RetryCommandLineOptionsProvider();
        CommandLineOption option = provider.GetCommandLineOptions().Single(x => x.Name == RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName);

        Assert.IsTrue(option.IsHidden);
        Assert.IsTrue(option.IsBuiltIn);
    }

    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsOptionName, "32")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsOptionName, "0")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, "32")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, "0")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, "100")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, "32")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, "0")]
    [TestMethod]
    public async Task IsValid_If_CorrectInteger_Is_Provided_For_RetryOptions(string optionName, string retries)
    {
        var provider = new RetryCommandLineOptionsProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == optionName);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [retries]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsOptionName, "invalid")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsOptionName, "32.32")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsOptionName, "-1")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, "invalid")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, "32.32")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, "-1")]
    [TestMethod]
    public async Task IsInvalid_If_IncorrectInteger_Or_NegativeValue_Is_Provided_For_RetryOptions(string optionName, string retries)
    {
        var provider = new RetryCommandLineOptionsProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == optionName);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [retries]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetryFailedTestsOptionNonNegativeIntegerArgumentErrorMessage, optionName), validateOptionsResult.ErrorMessage);
    }

    [DataRow("invalid")]
    [DataRow("32.32")]
    [DataRow("-1")]
    [DataRow("101")]
    [TestMethod]
    public async Task IsInvalid_If_IncorrectInteger_Or_OutOfRangeValue_Is_Provided_For_MaxPercentageOption(string retries)
    {
        var provider = new RetryCommandLineOptionsProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [retries]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetryFailedTestsMaxPercentageOptionIntegerBetween0And100ArgumentErrorMessage, RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName), validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task IsInvalid_When_MaxPercentage_MaxTests_BothProvided()
    {
        var provider = new RetryCommandLineOptionsProvider();
        var options = new Dictionary<string, string[]>
        {
            { RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, [] },
            { RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, [] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetryFailedTestsPercentageAndCountCannotBeMixedErrorMessage, RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName), validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task IsInvalid_When_MaxPercentage_Provided_But_TestOption_Missing()
    {
        var provider = new RetryCommandLineOptionsProvider();
        var options = new Dictionary<string, string[]>
        {
            { RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, [] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetryFailedTestsOptionIsMissingErrorMessage, RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, RetryCommandLineOptionsProvider.RetryFailedTestsOptionName), validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task IsInvalid_When_MaxTests_Provided_But_TestOption_Missing()
    {
        var provider = new RetryCommandLineOptionsProvider();
        var options = new Dictionary<string, string[]>
        {
            { RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, [] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetryFailedTestsOptionIsMissingErrorMessage, RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, RetryCommandLineOptionsProvider.RetryFailedTestsOptionName), validateOptionsResult.ErrorMessage);
    }

    [DataRow(true, false)]
    [DataRow(false, true)]
    [TestMethod]
    public async Task IsValid_When_TestOption_Provided_With_Either_MaxPercentage_MaxTests_Provided(bool isMaxPercentageSet, bool isMaxTestsSet)
    {
        var provider = new RetryCommandLineOptionsProvider();
        var options = new Dictionary<string, string[]>
        {
            { RetryCommandLineOptionsProvider.RetryFailedTestsOptionName, [] },
        };
        if (isMaxPercentageSet)
        {
            options.Add(RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, []);
        }

        if (isMaxTestsSet)
        {
            options.Add(RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, []);
        }

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [DataRow("0")]
    [DataRow("0s")]
    [DataRow("200")]
    [DataRow("1s")]
    [DataRow("2.5m")]
    [DataRow("1h")]
    [DataRow("2147483647ms")]
    [TestMethod]
    public async Task IsValid_If_CorrectTimeSpan_Is_Provided_For_DelayOption(string delay)
    {
        var provider = new RetryCommandLineOptionsProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == RetryCommandLineOptionsProvider.RetryFailedTestsDelayOptionName);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [delay]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [DataRow("invalid")]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("25d")]
    [TestMethod]
    public async Task IsInvalid_If_InvalidTimeSpan_Is_Provided_For_DelayOption(string delay)
    {
        var provider = new RetryCommandLineOptionsProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == RetryCommandLineOptionsProvider.RetryFailedTestsDelayOptionName);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [delay]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(Policy.Resources.ExtensionResources.RetryFailedTestsDelayOptionInvalidArgument, validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task IsInvalid_When_DelayOption_Provided_But_RetryOption_Missing()
    {
        var provider = new RetryCommandLineOptionsProvider();
        var options = new Dictionary<string, string[]>
        {
            { RetryCommandLineOptionsProvider.RetryFailedTestsDelayOptionName, ["1s"] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, Policy.Resources.ExtensionResources.RetryFailedTestsOptionIsMissingErrorMessage, RetryCommandLineOptionsProvider.RetryFailedTestsDelayOptionName, RetryCommandLineOptionsProvider.RetryFailedTestsOptionName), validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task IsValid_When_DelayOption_Provided_With_RetryOption()
    {
        var provider = new RetryCommandLineOptionsProvider();
        var options = new Dictionary<string, string[]>
        {
            { RetryCommandLineOptionsProvider.RetryFailedTestsOptionName, ["3"] },
            { RetryCommandLineOptionsProvider.RetryFailedTestsDelayOptionName, ["1s"] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    private static ServiceProvider CreateServiceProvider(params IArtifactPostProcessor[] processors)
    {
        ServiceProvider serviceProvider = new();
        serviceProvider.AddServices(processors);
        return serviceProvider;
    }

    private static ServiceProvider CreateRetryServiceProvider(
        TestCommandLineOptions? commandLineOptions = null,
        ILogger? logger = null)
    {
        ServiceProvider serviceProvider = new();
        serviceProvider.AddService(commandLineOptions ?? new TestCommandLineOptions([]));
        serviceProvider.AddService(Mock.Of<IEnvironment>());
        serviceProvider.AddService(new SystemTask());
        serviceProvider.AddService(Mock.Of<IFileSystem>());
        serviceProvider.AddService(Mock.Of<ITestApplicationCancellationTokenSource>(
            source => source.CancellationToken == CancellationToken.None));
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(factory => factory.CreateLogger(It.IsAny<string>())).Returns(logger ?? Mock.Of<ILogger>());
        serviceProvider.AddService(loggerFactory.Object);
        return serviceProvider;
    }

    private static Dictionary<string, string?> InitializeRetryEnvironment(string? logicalRunId, string? executionId)
    {
        ServiceProvider serviceProvider = CreateRetryServiceProvider();
        var environment = Mock.Get(serviceProvider.GetEnvironment());
        var values = new Dictionary<string, string?>
        {
            ["TESTINGPLATFORM_LOGICAL_RUN_ID"] = logicalRunId,
            ["TESTINGPLATFORM_DOTNETTEST_EXECUTIONID"] = executionId,
        };
        environment.Setup(value => value.GetEnvironmentVariable(It.IsAny<string>()))
            .Returns((string name) => values.TryGetValue(name, out string? value) ? value : null);
        environment.Setup(value => value.SetEnvironmentVariable(It.IsAny<string>(), It.IsAny<string?>()))
            .Callback((string name, string? value) => values[name] = value);
        var orchestrator = new RetryOrchestrator(serviceProvider);

        typeof(RetryOrchestrator).GetMethod("InitializeEnvironment", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(orchestrator, null);

        return values;
    }

    private static string CreateManifestLine(string path, string? kind)
        => InvokeWriteManifestEntry(path, kind);

    private static string InvokeWriteManifestEntry(string path, string? kind)
        => (string)GetRetryArtifactManifestType()
            .GetMethod("WriteEntry", BindingFlags.Static | BindingFlags.Public)!
            .Invoke(null, [path, kind])!;

    private static string InvokeWriteManifestEntryWithEncodedKind(string path, string encodedKindOrNullSentinel)
        => (string)GetRetryArtifactManifestType()
            .GetMethod("WriteEntryWithEncodedKind", BindingFlags.Static | BindingFlags.Public)!
            .Invoke(null, [path, encodedKindOrNullSentinel])!;

    private static (string EncodedPath, string EncodedKindOrNullSentinel) SplitManifestEntry(string entry)
    {
        object?[] arguments = [entry, null, null];
        bool wasSplit = (bool)GetRetryArtifactManifestType()
            .GetMethod("TrySplitEntry", BindingFlags.Static | BindingFlags.Public)!
            .Invoke(null, arguments)!;

        Assert.IsTrue(wasSplit);
        return ((string)arguments[1]!, (string)arguments[2]!);
    }

    private static string DecodeManifestPath(string encodedPath)
        => (string)GetRetryArtifactManifestType()
            .GetMethod("DecodePath", BindingFlags.Static | BindingFlags.Public)!
            .Invoke(null, [encodedPath])!;

    private static string? DecodeManifestKind(string encodedKindOrNullSentinel)
        => (string?)GetRetryArtifactManifestType()
            .GetMethod("DecodeKind", BindingFlags.Static | BindingFlags.Public)!
            .Invoke(null, [encodedKindOrNullSentinel]);

    private static Type GetRetryArtifactManifestType()
        => typeof(RetryOrchestrator).Assembly
            .GetType("Microsoft.Testing.Extensions.RetryArtifactManifest")!;

    private static void InvokeCollectRecoveredArtifacts(
        IFileSystem fileSystem,
        string manifestPath,
        string attemptDirectory,
        List<ArtifactRequest> artifacts,
        ILogger? logger = null)
        => typeof(RetryOrchestrator)
            .GetMethod("CollectRecoveredArtifacts", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [fileSystem, manifestPath, attemptDirectory, artifacts, logger ?? Mock.Of<ILogger>()]);

    private static void InvokeRemoveArtifactsOutsideControllerRoots(
        IEnvironment environment,
        List<ArtifactRequest> artifacts,
        ILogger? logger = null)
        => typeof(RetryOrchestrator)
            .GetMethod("RemoveArtifactsOutsideControllerRoots", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [environment, artifacts, logger ?? Mock.Of<ILogger>()]);

    private static Mock<ILogger> CreateRecordingLogger()
    {
        Mock<ILogger> logger = new();
        logger
            .Setup(value => value.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<string>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<string, Exception?, string>>()));
        logger
            .Setup(value => value.LogAsync(
                It.IsAny<LogLevel>(),
                It.IsAny<string>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<string, Exception?, string>>()))
            .Returns(Task.CompletedTask);
        return logger;
    }

    private static bool InvokeRetryTestHostRunnerPredicate(
        string methodName,
        Task completedTask,
        Task waitForConnectionTask)
        => (bool)typeof(RetryTestHostRunner)
            .GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [completedTask, waitForConnectionTask])!;

    private static Task<Task> InvokeRetryTestHostRunnerGracePeriodAsync(
        Task completedTask,
        Task waitForConnectionTask,
        CancellationToken cancellationToken)
        => (Task<Task>)typeof(RetryTestHostRunner)
            .GetMethod(
                "WaitForConnectionGracePeriodAsync",
                BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [completedTask, waitForConnectionTask, cancellationToken])!;

    private static void AssertReadLine(MethodInfo readLine, object reader, string expectedResult, string expectedLine)
    {
        object?[] arguments = [null];
        object result = readLine.Invoke(reader, arguments)!;

        Assert.AreEqual(expectedResult, result.ToString());
        Assert.AreEqual(expectedLine, arguments[0]);
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"retry-artifact-processor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class ReadOnlyMemoryFileStream(string content) : IFileStream
    {
        private readonly MemoryStream _stream = new(Encoding.UTF8.GetBytes(content));

        Stream IFileStream.Stream => _stream;

        string IFileStream.Name => string.Empty;

        void IDisposable.Dispose() => _stream.Dispose();

#if NETCOREAPP
        ValueTask IAsyncDisposable.DisposeAsync() => _stream.DisposeAsync();
#endif
    }

    private sealed class ExitOnStatusProbeProcess(int exitCode) : IProcess
    {
        private EventHandler? _exited;

        public event EventHandler Exited
        {
            add
            {
                HandlerWasAttached = true;
                _exited += value;
            }

            remove
            {
                HandlerWasDetached = true;
                _exited -= value;
            }
        }

        public bool HandlerWasAttached { get; private set; }

        public bool HandlerWasDetached { get; private set; }

        public int Id => 42;

        public string Name => nameof(ExitOnStatusProbeProcess);

        public int ExitCode => exitCode;

        public bool HasExited
        {
            get
            {
                _exited?.Invoke(this, EventArgs.Empty);
                return false;
            }
        }

        public IMainModule? MainModule => null;

        public DateTime StartTime => default;

        public Task WaitForExitAsync() => Task.CompletedTask;

        public Task WaitForExitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void WaitForExit()
        {
        }

        public void Kill()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class TestArtifactPostProcessor : IArtifactPostProcessor
    {
        private readonly Func<
            IReadOnlyList<InputArtifact>,
            string,
            ArtifactPostProcessingContext,
            CancellationToken,
            Task<ProcessedArtifact?>> _processAsync;

        public TestArtifactPostProcessor(
            IReadOnlyList<ArtifactPostProcessingMode> supportedModes,
            IReadOnlyList<string> supportedKinds,
            IReadOnlyList<string> supportedExtensions,
            Func<
                IReadOnlyList<InputArtifact>,
                string,
                ArtifactPostProcessingContext,
                CancellationToken,
                Task<ProcessedArtifact?>>? processAsync = null,
            string uid = "test-processor")
        {
            SupportedModes = supportedModes;
            SupportedKinds = supportedKinds;
            SupportedFileExtensionsFallback = supportedExtensions;
            Uid = uid;
            _processAsync = processAsync ?? ((_, _, _, _) => Task.FromResult<ProcessedArtifact?>(null));
        }

        public string Uid { get; }

        public string Version => "1.0.0";

        public string DisplayName => Uid;

        public string Description => Uid;

        public IReadOnlyList<ArtifactPostProcessingMode> SupportedModes { get; }

        public bool SupportsTruncatedRuns => false;

        public IReadOnlyList<string> SupportedKinds { get; }

        public IReadOnlyList<string> SupportedFileExtensionsFallback { get; }

        public int ProcessCallCount { get; private set; }

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task<ProcessedArtifact?> ProcessAsync(
            IReadOnlyList<InputArtifact> inputs,
            string outputDirectory,
            ArtifactPostProcessingContext context,
            CancellationToken cancellationToken)
        {
            ProcessCallCount++;
            return _processAsync(inputs, outputDirectory, context, cancellationToken);
        }
    }

    private sealed class ConnectingTestHostLauncher(
        int exitCode = 0,
        bool isExitCodeAuthoritative = true,
        Action? onConnected = null) : ITestHostLauncher
    {
        public TestHostLaunchContext? Context { get; private set; }

        public string Uid => nameof(ConnectingTestHostLauncher);

        public string Version => "1.0.0";

        public string DisplayName => nameof(ConnectingTestHostLauncher);

        public string Description => nameof(ConnectingTestHostLauncher);

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public async Task<ITestHostHandle> LaunchTestHostAsync(TestHostLaunchContext context, CancellationToken cancellationToken)
        {
            Context = context;
            int pipeNameIndex = context.Arguments.ToList().IndexOf($"--{RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName}") + 1;
            var pipeClient = new NamedPipeClientStream(".", context.Arguments[pipeNameIndex], PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipeClient.ConnectAsync(5_000, cancellationToken);
            return new ConnectedTestHostHandle(
                pipeClient,
                new GetListOfFailedTestsRequestSerializer().Id,
                exitCode,
                isExitCodeAuthoritative,
                onConnected);
        }
    }

    private sealed class ConnectedTestHostHandle(
        NamedPipeClientStream pipeClient,
        int requestSerializerId,
        int exitCode,
        bool isExitCodeAuthoritative,
        Action? onConnected) : ITestHostHandle, ITestHostHandleExitCodePolicy
    {
        private static readonly TimeSpan RetryPipeRoundTripTimeout = TimeSpan.FromSeconds(30);

        private readonly NamedPipeClientStream _pipeClient = pipeClient;
        private readonly Task _exitTask = CompleteRunAsync(pipeClient, requestSerializerId, onConnected);

        public string Identifier => nameof(ConnectedTestHostHandle);

        public int ExitCode => exitCode;

        public bool HasExited => _exitTask.IsCompleted;

        public bool IsExitCodeAuthoritative => isExitCodeAuthoritative;

        public Task WaitForExitAsync(CancellationToken cancellationToken)
            => _exitTask.WithCancellationAsync(cancellationToken);

        public void Terminate()
        {
        }

        public void Dispose() => _pipeClient.Dispose();

        private static async Task CompleteRunAsync(
            NamedPipeClientStream pipeClient,
            int requestSerializerId,
            Action? onConnected)
        {
            await Task.Yield();

            using var timeout = new CancellationTokenSource(RetryPipeRoundTripTimeout);

            // Complete a retry-protocol round trip before reporting exit so the server has accepted the connection.
            byte[] request = new byte[2 * sizeof(int)];
            BitConverter.GetBytes(sizeof(int)).CopyTo(request, 0);
            BitConverter.GetBytes(requestSerializerId).CopyTo(request, sizeof(int));
            await pipeClient.WriteAsync(request, 0, request.Length, timeout.Token);
            await pipeClient.FlushAsync(timeout.Token);

            byte[] responseSizeBuffer = new byte[sizeof(int)];
            await ReadExactlyAsync(pipeClient, responseSizeBuffer, timeout.Token);
            int responseSize = BitConverter.ToInt32(responseSizeBuffer, 0);
            if (responseSize < sizeof(int))
            {
                throw new InvalidDataException($"Invalid retry pipe response size: {responseSize}.");
            }

            await ReadExactlyAsync(pipeClient, new byte[responseSize], timeout.Token);
            onConnected?.Invoke();
        }

        private static async Task ReadExactlyAsync(
            NamedPipeClientStream pipeClient,
            byte[] buffer,
            CancellationToken cancellationToken)
        {
            int bytesRead = 0;
            while (bytesRead < buffer.Length)
            {
                int read = await pipeClient.ReadAsync(buffer, bytesRead, buffer.Length - bytesRead, cancellationToken)
                    .WithCancellationAsync(cancellationToken);
                if (read == 0)
                {
                    throw new EndOfStreamException("The retry pipe closed before the response was complete.");
                }

                bytesRead += read;
            }
        }
    }

    private sealed class AlreadyExitedTestHostLauncher(int exitCode) : ITestHostLauncher
    {
        public TestHostLaunchContext? Context { get; private set; }

        public string Uid => nameof(AlreadyExitedTestHostLauncher);

        public string Version => "1.0.0";

        public string DisplayName => nameof(AlreadyExitedTestHostLauncher);

        public string Description => nameof(AlreadyExitedTestHostLauncher);

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task<ITestHostHandle> LaunchTestHostAsync(TestHostLaunchContext context, CancellationToken cancellationToken)
        {
            Context = context;
            return Task.FromResult<ITestHostHandle>(new AlreadyExitedTestHostHandle(exitCode));
        }
    }

    private sealed class AlreadyExitedTestHostHandle(int exitCode) : ITestHostHandle
    {
        public string Identifier => nameof(AlreadyExitedTestHostHandle);

        public int ExitCode => exitCode;

        public bool HasExited => true;

        public Task WaitForExitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Terminate()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class TerminatedTestHostLauncher : ITestHostLauncher
    {
        public TerminatedTestHostHandle Handle { get; } = new();

        public string Uid => nameof(TerminatedTestHostLauncher);

        public string Version => "1.0.0";

        public string DisplayName => Uid;

        public string Description => Uid;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task<ITestHostHandle> LaunchTestHostAsync(TestHostLaunchContext context, CancellationToken cancellationToken)
            => Task.FromResult<ITestHostHandle>(Handle);
    }

    private sealed class TerminatedTestHostHandle : ITestHostHandle
    {
        private readonly TaskCompletionSource<bool> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool TerminateCalled { get; private set; }

        public bool Disposed { get; private set; }

        public string Identifier => nameof(TerminatedTestHostHandle);

        public int ExitCode => 1;

        public bool HasExited => _exited.Task.IsCompleted;

        public Task WaitForExitAsync(CancellationToken cancellationToken) => _exited.Task.WithCancellationAsync(cancellationToken);

        public void Terminate()
        {
            TerminateCalled = true;
            _exited.TrySetResult(true);
        }

        public void Dispose() => Disposed = true;
    }
}
