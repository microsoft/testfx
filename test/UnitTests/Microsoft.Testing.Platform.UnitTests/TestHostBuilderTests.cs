// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.ExceptionServices;

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHostControllers;
using Microsoft.Testing.Platform.UnitTests.Helpers;

using Moq;

using BackCompat = Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using PublicApi = Microsoft.Testing.Platform.TestHostOrchestrator;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class TestHostBuilderTests
{
    [TestMethod]
    public async Task ControllerPreLaunch_StartsLifetimeHandlersBeforePublishingEnvironment()
    {
        string endpoint = "unqualified";
        string? publishedEndpoint = null;
        Mock<ITestHostProcessLifetimeHandler> handler = new();
        handler.Setup(x => x.BeforeTestHostProcessStartAsync(It.IsAny<CancellationToken>()))
            .Callback(() => endpoint = @"LOCAL\qualified")
            .Returns(Task.CompletedTask);
        Mock<ITestHostEnvironmentVariableProvider> provider = new();
        provider.Setup(x => x.UpdateAsync(It.IsAny<IEnvironmentVariables>()))
            .Callback(() => publishedEndpoint = endpoint)
            .Returns(Task.CompletedTask);

        await TestHostControllersTestHost.ApplyControllerExtensionPreLaunchAsync(
            [handler.Object],
            [provider.Object],
            new EnvironmentVariables(new Mock<ILoggerFactory>().Object),
            CancellationToken.None);

        Assert.AreEqual(@"LOCAL\qualified", publishedEndpoint);
    }

    [TestMethod]
    public async Task ControllerPreLaunch_CooperativeShutdownTimeoutUsesFinalProviderValue()
    {
        Mock<ITestHostEnvironmentVariableProvider> provider = new();
        provider.SetupGet(x => x.Uid).Returns("provider");
        provider.SetupGet(x => x.DisplayName).Returns("provider");
        provider.Setup(x => x.UpdateAsync(It.IsAny<IEnvironmentVariables>()))
            .Callback<IEnvironmentVariables>(environmentVariables => environmentVariables.SetVariable(new(
                EnvironmentVariableConstants.TESTINGPLATFORM_MESSAGEBUS_CANCELED_SHUTDOWN_TIMEOUT_SECONDS,
                "60",
                isSecret: false,
                isLocked: false)))
            .Returns(Task.CompletedTask);
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new NopLogger());
        var environmentVariables = new EnvironmentVariables(loggerFactory.Object);

        await TestHostControllersTestHost.ApplyControllerExtensionPreLaunchAsync(
            [],
            [provider.Object],
            environmentVariables,
            CancellationToken.None);

        Assert.AreEqual(
            TimeSpan.FromSeconds(75),
            TestHostControllersTestHost.GetTestHostCooperativeShutdownTimeout(environmentVariables));
        Assert.AreEqual(
            TimeSpan.FromSeconds(60),
            ShutdownTimeouts.GetCanceledConsumerCompletion("60"));
    }

    // Mutates a real process-global environment variable (via SystemEnvironment, not a mock) under the
    // assembly's method-level parallelism. No other test in this assembly reads or writes the same
    // PID-qualified TESTINGPLATFORM_TESTHOSTCONTROLLER_PIPENAME_<pid> key (the other tests in this class use
    // mocked ITestHostEnvironmentVariableProvider/IEnvironmentVariables, never the real Environment), so a
    // method-level lock is sufficient without serializing the rest of the class.
    [TestMethod]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    public async Task ConnectToTestHostProcessMonitorIfAvailableAsync_MissingPipeName_ReportsPidQualifiedEnvironmentVariable()
    {
        const int testHostControllerPid = 123456789;
        string pipeEnvironmentVariable = $"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_PIPENAME}_{testHostControllerPid}";
        SystemEnvironment environment = new();
        string? previousPipeName = environment.GetEnvironmentVariable(pipeEnvironmentVariable);
        environment.SetEnvironmentVariable(pipeEnvironmentVariable, null);

        try
        {
            MethodInfo method = typeof(TestHostBuilder).GetMethod("ConnectToTestHostProcessMonitorIfAvailableAsync", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("Could not find TestHostBuilder.ConnectToTestHostProcessMonitorIfAvailableAsync.");
            TestHostControllerInfo testHostControllerInfo = new(new CommandLineParseResult(
                null,
                [new CommandLineParseOption(PlatformCommandLineProvider.TestHostControllerPIDOptionKey, [testHostControllerPid.ToString(CultureInfo.InvariantCulture)])],
                []));
            CurrentTestApplicationModuleInfo testApplicationModuleInfo = new(environment, new SystemProcessHandler());
            AggregatedConfiguration configuration = new([], testApplicationModuleInfo, new SystemFileSystem(), environment, new(null, [], []));

            InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => ConnectToTestHostProcessMonitorIfAvailableAsync(method, testHostControllerInfo, configuration, environment));

            Assert.AreEqual($"Unexpected null pipe name from environment variable '{pipeEnvironmentVariable}'", exception.Message);
        }
        finally
        {
            environment.SetEnvironmentVariable(pipeEnvironmentVariable, previousPipeName);
        }
    }

    [TestMethod]
    public void ShouldSkipTestHostControllersHost_RetryChild_RetainsControllerComposition()
    {
        TestHostControllerInfo controllerInfo = CreateTestHostControllerInfo(testHostControllerPid: null);
        var commandLineOptions = new TestCommandLineOptions(new()
        {
            ["internal-retry-pipename"] = ["retry-pipe"],
        });

        Assert.IsFalse(TestHostBuilder.ShouldSkipTestHostControllersHost(
            controllerInfo,
            commandLineOptions,
            Mock.Of<IEnvironment>()));
    }

    [TestMethod]
    public void ShouldSkipTestHostControllersHost_PackagedRetryChildWithSkipMarker_SkipsControllerComposition()
    {
        TestHostControllerInfo controllerInfo = CreateTestHostControllerInfo(testHostControllerPid: null);
        var commandLineOptions = new TestCommandLineOptions(new()
        {
            ["internal-retry-pipename"] = ["retry-pipe"],
        });
        Mock<IEnvironment> environment = new();
        environment
            .Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_SKIPEXTENSION))
            .Returns("1");

        Assert.IsTrue(TestHostBuilder.ShouldSkipTestHostControllersHost(
            controllerInfo,
            commandLineOptions,
            environment.Object));
    }

    [TestMethod]
    public void ShouldSkipTestHostControllersHost_ControllerChildWithSkipMarker_SkipsControllerComposition()
    {
        const int ControllerPid = 42;
        TestHostControllerInfo controllerInfo = CreateTestHostControllerInfo(ControllerPid);
        Mock<IEnvironment> environment = new();
        environment
            .Setup(x => x.GetEnvironmentVariable(
                $"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_SKIPEXTENSION}_{ControllerPid}"))
            .Returns("1");

        Assert.IsTrue(TestHostBuilder.ShouldSkipTestHostControllersHost(
            controllerInfo,
            new TestCommandLineOptions([]),
            environment.Object));
    }

    [TestMethod]
    public void ShouldSkipTestHostControllersHost_ControllerChildWithoutSkipMarker_RetainsControllerComposition()
    {
        TestHostControllerInfo controllerInfo = CreateTestHostControllerInfo(testHostControllerPid: 42);

        Assert.IsFalse(TestHostBuilder.ShouldSkipTestHostControllersHost(
            controllerInfo,
            new TestCommandLineOptions([]),
            Mock.Of<IEnvironment>()));
    }

    [TestMethod]
    public void ValidateTestHostOrchestratorMiddlewareConfiguration_WithNoOrchestrator_ThrowsWithCount()
    {
        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => TestHostBuilder.ValidateTestHostOrchestratorMiddlewareConfiguration(orchestratorCount: 0, middlewareCount: 1));

        Assert.Contains("0", exception.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void ValidateTestHostOrchestratorMiddlewareConfiguration_WithMultipleOrchestrators_ThrowsWithCount()
    {
        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => TestHostBuilder.ValidateTestHostOrchestratorMiddlewareConfiguration(orchestratorCount: 2, middlewareCount: 1));

        Assert.Contains("2", exception.Message, StringComparison.Ordinal);
    }

    [DataRow(0)]
    [DataRow(2)]
    [TestMethod]
    public async Task BuilderRegistrationWithInvalidOrchestratorCount_ThrowsWithCount(int orchestratorCount)
    {
        var builder = new TestApplicationBuilder(
            new ApplicationLoggingState(LogLevel.None, new CommandLineParseResult(null, [], [])),
            DateTimeOffset.UtcNow,
            new TestApplicationOptions(),
            Mock.Of<IUnhandledExceptionsHandler>(),
            ["--no-banner"]);
        builder.RegisterTestFramework(
            _ => Mock.Of<ITestFrameworkCapabilities>(),
            (_, _) => Mock.Of<ITestFramework>());
        var orchestratorManager = (PublicApi.TestHostOrchestratorManager)builder.TestHostOrchestrator;
        for (int i = 0; i < orchestratorCount; i++)
        {
            int orchestratorIndex = i;
            orchestratorManager.AddTestHostOrchestrator(
                _ =>
                {
                    Mock<BackCompat.ITestHostExecutionOrchestrator> orchestrator = new();
                    orchestrator.SetupGet(x => x.Uid).Returns($"orchestrator-{orchestratorIndex}");
                    orchestrator.Setup(x => x.IsEnabledAsync()).ReturnsAsync(true);
                    return orchestrator.Object;
                });
        }

        ((PublicApi.ITestHostExecutionOrchestratorMiddlewareManager)orchestratorManager)
            .AddTestHostExecutionOrchestratorMiddleware(
                _ =>
                {
                    Mock<BackCompat.ITestHostExecutionOrchestratorMiddleware> middleware = new();
                    middleware.SetupGet(x => x.Uid).Returns("middleware");
                    middleware.Setup(x => x.IsEnabledAsync()).ReturnsAsync(true);
                    return middleware.Object;
                });

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => builder.BuildAsync());

        Assert.Contains($"Found {orchestratorCount} orchestrator(s) instead.", exception.Message, StringComparison.Ordinal);
    }

    private static TestHostControllerInfo CreateTestHostControllerInfo(int? testHostControllerPid)
        => new(new CommandLineParseResult(
            null,
            testHostControllerPid.HasValue
                ? [new CommandLineParseOption(
                    PlatformCommandLineProvider.TestHostControllerPIDOptionKey,
                    [testHostControllerPid.Value.ToString(CultureInfo.InvariantCulture)])]
                : [],
            []));

    private static async Task ConnectToTestHostProcessMonitorIfAvailableAsync(
        MethodInfo method,
        TestHostControllerInfo testHostControllerInfo,
        AggregatedConfiguration configuration,
        SystemEnvironment environment)
    {
        using CTRLPlusCCancellationTokenSource cancellationTokenSource = new();
        Task connectTask;
        try
        {
            connectTask = (Task?)method.Invoke(null, [cancellationTokenSource, new NopLogger(), testHostControllerInfo, configuration, environment])
                ?? throw new InvalidOperationException("TestHostBuilder.ConnectToTestHostProcessMonitorIfAvailableAsync returned null.");
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }

        await connectTask;
    }
}
