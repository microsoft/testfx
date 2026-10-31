// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostControllers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class TestApplicationBuilderTests
{
    private const string ContosoPackageSid = "S-1-15-2-1990679259-4123976751-842158434-3026549936-2944832882-252165955-409282942";

    private readonly ServiceProvider _serviceProvider = new();

    public TestApplicationBuilderTests()
    {
        CurrentTestApplicationModuleInfo testApplicationModuleInfo = new(new SystemEnvironment(), new SystemProcessHandler());
        AggregatedConfiguration configuration = new([], testApplicationModuleInfo, new SystemFileSystem(), new SystemEnvironment(), new(null, [], []));
        configuration.SetCurrentWorkingDirectory(string.Empty);
        configuration.SetCurrentWorkingDirectory(string.Empty);
        _serviceProvider.AddService(configuration);
    }

    [TestMethod]
    [DataRow(true, false, "capabilitiesFactory", DisplayName = "Null capabilities factory")]
    [DataRow(false, true, "frameworkFactory", DisplayName = "Null framework factory")]
    [DataRow(true, true, "frameworkFactory", DisplayName = "Both factories null")]
    public void RegisterTestFramework_InvalidFactories_DoNotPoisonSubsequentRegistration(bool nullCapabilities, bool nullFramework, string expectedParameter)
    {
        TestApplicationBuilder builder = CreateRegistrationBuilder();
        bool factoryInvoked = false;
        Func<IServiceProvider, ITestFrameworkCapabilities> capabilities = _ =>
        {
            factoryInvoked = true;
            return Mock.Of<ITestFrameworkCapabilities>();
        };
        Func<ITestFrameworkCapabilities, IServiceProvider, ITestFramework> framework = (_, _) =>
        {
            factoryInvoked = true;
            return Mock.Of<ITestFramework>();
        };

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => builder.RegisterTestFramework(nullCapabilities ? null! : capabilities, nullFramework ? null! : framework));
        ITestApplicationBuilder registered = builder.RegisterTestFramework(capabilities, framework);

        Assert.AreEqual(expectedParameter, exception.ParamName);
        Assert.AreSame(builder, registered);
        Assert.IsFalse(factoryInvoked, "Registration is lazy, even after rejected invalid input.");
        Assert.ThrowsExactly<InvalidOperationException>(() => builder.RegisterTestFramework(capabilities, framework));
    }

    [TestMethod]
    public async Task BuildAsync_WithoutRegisteredFramework_RejectsBeforeBuildingHost()
    {
        TestApplicationBuilder builder = CreateRegistrationBuilder();

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(builder.BuildAsync);

        Assert.Contains("test framework", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("registered", exception.Message, StringComparison.OrdinalIgnoreCase);
        await ((IAsyncCleanableExtension)builder).CleanupAsync();
    }

    private static TestApplicationBuilder CreateRegistrationBuilder()
        => new(
            new ApplicationLoggingState(LogLevel.None, CommandLineParseResult.Empty),
            DateTimeOffset.MinValue,
            new TestApplicationOptions(),
            Mock.Of<IUnhandledExceptionsHandler>(),
            []);

    [TestMethod]
    [DataRow(false, DisplayName = "Prepared ordinary process")]
    [DataRow(true, DisplayName = "Prepared custom launcher")]
    public async Task ControllerHost_RunAsync_PreparesEnvironmentAndCompletesRealIpcLifecycleWithoutLaunchingAProcess(bool customLauncher)
    {
        ControllerHostFixture fixture = new(customLauncher: customLauncher);
        try
        {
            Task<int> run = fixture.RunAsync();
            await run.TimeoutAfterAsync(ControllerHostFixture.OperationLimit);
            Assert.AreEqual(0, await run);
            Assert.AreSequenceEqual(
                new[] { "before-process", "update-environment", "validate-environment", "launch", "process-started", "process-exited", "output-finalized", "run-completed" },
                fixture.Events.Where(value => !value.StartsWith("dispose-", StringComparison.Ordinal)));
            Assert.AreEqual(606, fixture.StartedInformation!.PID);
            Assert.ThrowsExactly<InvalidOperationException>(() => fixture.StartedInformation.ExitCode);
            Assert.AreEqual(606, fixture.ExitedInformation!.PID);
            Assert.AreEqual(0, fixture.ExitedInformation.ExitCode);
            Assert.IsTrue(fixture.ExitedInformation.HasExitedGracefully);
            Assert.AreEqual(0, fixture.CompletionExitCode);
            SessionFileArtifact artifact = Assert.ContainsSingle(fixture.CompletionArtifacts);
            Assert.AreEqual("child summary", artifact.DisplayName);
            Assert.AreEqual("child artifact", artifact.Description);
            Assert.AreEqual("microsoft.testing.trx", artifact.Kind);
            Assert.AreEqual("controller-session", artifact.SessionUid.Value);
            Assert.AreSequenceEqual(
                new[] { "dispose-lifetime", "dispose-environment" },
                fixture.Events.Where(value => value is "dispose-lifetime" or "dispose-environment"));
            fixture.Environment.Verify(x => x.Exit(It.IsAny<int>()), Times.Never);
            fixture.Environment.Verify(x => x.FailFast(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
            if (customLauncher)
            {
                Assert.IsNotNull(fixture.LaunchContext);
                Assert.AreEqual("fake-host", fixture.LaunchContext.FileName);
                Assert.AreEqual("controller-workspace", fixture.LaunchContext.WorkingDirectory);
                Assert.AreSequenceEqual(new[] { "--filter", "name with spaces", "--internal-testhostcontroller-pid", "42" }, fixture.LaunchContext.Arguments);
                Assert.AreEqual("false", fixture.LaunchContext.EnvironmentVariables["TF_BUILD"]);
                Assert.AreEqual("provider-value", fixture.LaunchContext.EnvironmentVariables["CUSTOM"]);
                Assert.AreEqual("03:04:05.000", fixture.LaunchContext.EnvironmentVariables["TESTINGPLATFORM_TESTHOSTCONTROLLER_TESTHOSTPROCESSSTARTTIME_42"]);
                fixture.ProcessHandler.Verify(x => x.Start(It.IsAny<ProcessStartInfo>()), Times.Never);
                fixture.Handle.Verify(x => x.Dispose(), Times.Once);
            }
            else
            {
                Assert.IsNotNull(fixture.StartInfo);
                Assert.AreEqual("fake-host", fixture.StartInfo.FileName);
                Assert.AreEqual(string.Empty, fixture.StartInfo.WorkingDirectory);
                Assert.IsFalse(fixture.StartInfo.UseShellExecute);
                Assert.AreEqual("false", fixture.StartInfo.EnvironmentVariables["TF_BUILD"]);
                Assert.AreEqual("provider-value", fixture.StartInfo.EnvironmentVariables["CUSTOM"]);
#if NETCOREAPP
                Assert.AreSequenceEqual(new[] { "--filter", "name with spaces", "--internal-testhostcontroller-pid", "42" }, fixture.StartInfo.ArgumentList);
#else
                Assert.AreEqual("--filter \"name with spaces\" --internal-testhostcontroller-pid 42", fixture.StartInfo.Arguments);
#endif
                fixture.Process.Verify(x => x.Dispose(), Times.Once);
            }
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task ControllerHost_RunAsync_EnvironmentValidationFailurePreventsLaunchAndStillDisposesExtensions()
    {
        ControllerHostFixture fixture = new(validEnvironment: false);
        try
        {
            Task<int> run = fixture.RunAsync();
            await run.TimeoutAfterAsync(ControllerHostFixture.OperationLimit);
            Assert.AreEqual(4, await run);
            fixture.ProcessHandler.Verify(x => x.Start(It.IsAny<ProcessStartInfo>()), Times.Never);
            fixture.Launcher.Verify(x => x.LaunchTestHostAsync(It.IsAny<TestHostLaunchContext>(), It.IsAny<CancellationToken>()), Times.Never);
            string error = Assert.IsInstanceOfType<ErrorMessageOutputDeviceData>(Assert.ContainsSingle(fixture.OutputMessages)).Message;
            Assert.Contains("provider rejected", error);
            Assert.Contains("environment", error);
            Assert.AreSequenceEqual(
                new[] { "before-process", "update-environment", "validate-environment", "dispose-lifetime", "dispose-environment" },
                fixture.Events);
            Assert.IsNull(fixture.CompletionExitCode);
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    [TestMethod]
    [DataRow(90L, 0, DisplayName = "Passing controller-owned coverage")]
    [DataRow(79L, 14, DisplayName = "Failing controller-owned coverage")]
    public async Task ControllerHost_RunAsync_ControllerCoverageIsCollectedAfterChildExitAndSetsFinalVerdict(long coveredLines, int expectedExit)
    {
        ControllerHostFixture fixture = new(ownsCoverage: true, lineThreshold: "80", coveredLines: coveredLines);
        try
        {
            Task<int> run = fixture.RunAsync();
            await run.TimeoutAfterAsync(ControllerHostFixture.OperationLimit);
            Assert.AreEqual(expectedExit, await run);
            Assert.AreEqual(expectedExit, fixture.CompletionExitCode);
            Assert.AreEqual("1", fixture.StartInfo!.EnvironmentVariables["TESTINGPLATFORM_TESTHOSTCONTROLLER_COVERAGEPOLICY_42"]);
            TestCoverageResult coverage = fixture.Services.GetRequiredService<TestCoverageResult>();
            Assert.AreEqual(coveredLines < 80, coverage.HasThresholdFailure);
            if (expectedExit == 14)
            {
                Assert.Contains("80", Assert.IsInstanceOfType<ErrorMessageOutputDeviceData>(Assert.ContainsSingle(fixture.OutputMessages)).Message);
            }
            else
            {
                Assert.IsEmpty(fixture.OutputMessages);
            }
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Authoritative process exit mismatch")]
    [DataRow(true, DisplayName = "Nonauthoritative custom handle exit")]
    public async Task ControllerHost_RunAsync_ExitAuthorityAndCompletionHandshakeDeterminePublicVerdict(bool nonauthoritative)
    {
        ControllerHostFixture fixture = new(
            customLauncher: nonauthoritative, osExitCode: 17, reportedExitCode: 0, authoritativeExitCode: !nonauthoritative);
        try
        {
            Task<int> run = fixture.RunAsync();
            await run.TimeoutAfterAsync(ControllerHostFixture.OperationLimit);
            Assert.AreEqual(nonauthoritative ? 0 : 7, await run);
            Assert.AreEqual(nonauthoritative ? 0 : 7, fixture.CompletionExitCode);
            Assert.IsTrue(fixture.ExitedInformation!.HasExitedGracefully);
            if (nonauthoritative)
            {
                Assert.IsEmpty(fixture.OutputMessages);
            }
            else
            {
                Assert.Contains("17", Assert.IsInstanceOfType<ErrorMessageOutputDeviceData>(Assert.ContainsSingle(fixture.OutputMessages)).Message);
            }
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task ControllerHost_RunAsync_MissingCompletionMessageRejectsZeroOsExitAndReportsNonGracefulVerdict()
    {
        ControllerHostFixture fixture = new(sendCompletion: false);
        try
        {
            Task<int> run = fixture.RunAsync();
            await run.TimeoutAfterAsync(ControllerHostFixture.OperationLimit);
            Assert.AreEqual(7, await run);
            Assert.IsFalse(fixture.ExitedInformation!.HasExitedGracefully);
            Assert.AreEqual(7, fixture.CompletionExitCode);
            Assert.HasCount(1, fixture.OutputMessages);
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task ControllerHost_RunAsync_TelemetryRecordsTheActualControllerVerdictAndExtensionIdentity()
    {
        ControllerHostFixture fixture = new(telemetry: true, osExitCode: 2, reportedExitCode: 2);
        try
        {
            Task<int> run = fixture.RunAsync();
            await run.TimeoutAfterAsync(ControllerHostFixture.OperationLimit);
            Assert.AreEqual(2, await run);
            (string name, IDictionary<string, object> values) = Assert.ContainsSingle(fixture.TelemetryEvents);
            Assert.AreEqual(TelemetryEvents.TestHostControllersTestHostExitEventName, name);
            Assert.AreEqual("2", values[TelemetryProperties.HostProperties.ExitCodePropertyName]);
            Assert.AreEqual("true", values[TelemetryProperties.HostProperties.HasExitedGracefullyPropertyName]);
            string extensions = (string)values[TelemetryProperties.HostProperties.ExtensionsPropertyName];
            Assert.Contains("1.2.3", extensions);
            Assert.AreEqual(2, fixture.CompletionExitCode);
            Assert.IsEmpty(fixture.OutputMessages);
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task ControllerRegistration_InitializationFailureStopsBeforeRegisteringOrBuildingLaterProviders()
    {
        TestHostControllersManager manager = new();
        InvalidOperationException expectedFailure = new("controller initialization failed");
        Mock<ITestHostEnvironmentVariableProvider> broken = new();
        broken.SetupGet(x => x.Uid).Returns("broken");
        broken.Setup(x => x.IsEnabledAsync()).ReturnsAsync(true);
        broken.As<IAsyncInitializableExtension>().Setup(x => x.InitializeAsync()).ThrowsAsync(expectedFailure);
        bool laterProviderBuilt = false;
        bool observerBuilt = false;
        manager.AddEnvironmentVariableProvider(_ => broken.Object);
        manager.AddEnvironmentVariableProvider(_ =>
        {
            laterProviderBuilt = true;
            return new TestHostEnvironmentVariableProvider("later");
        });
        manager.AddRunCompletionHandler(_ =>
        {
            observerBuilt = true;
            return Mock.Of<ITestHostControllerRunCompletionHandler>();
        });

        InvalidOperationException actualFailure = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => manager.BuildAsync(_serviceProvider));

        Assert.AreSame(expectedFailure, actualFailure);
        Assert.IsFalse(laterProviderBuilt);
        Assert.IsFalse(observerBuilt);
        Assert.IsEmpty(_serviceProvider.GetServicesInternal(typeof(ITestHostEnvironmentVariableProvider)));
    }

    [TestMethod]
    public async Task ControllerRegistration_DisabledCompositeConsumerDoesNotInitializeOrIntroduceAController()
    {
        TestHostControllersManager manager = new();
        Mock<IDataConsumer> consumer = new();
        consumer.SetupGet(x => x.Uid).Returns("disabled-consumer");
        consumer.Setup(x => x.IsEnabledAsync()).ReturnsAsync(false);
        Mock<IAsyncInitializableExtension> initialization = consumer.As<IAsyncInitializableExtension>();
        initialization.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        manager.AddDataConsumer(new CompositeExtensionFactory<IDataConsumer>(() => consumer.Object));
        bool observerBuilt = false;
        manager.AddRunCompletionHandler(_ =>
        {
            observerBuilt = true;
            return Mock.Of<ITestHostControllerRunCompletionHandler>();
        });

        TestHostControllerConfiguration configuration = await manager.BuildAsync(_serviceProvider);

        Assert.IsFalse(configuration.RequireProcessRestart);
        Assert.IsEmpty(configuration.DataConsumer);
        Assert.IsEmpty(configuration.RunCompletionHandlers);
        Assert.IsFalse(observerBuilt);
        initialization.Verify(x => x.InitializeAsync(), Times.Never);
    }

    [TestMethod]
    public async Task TestApplicationLifecycleCallbacks_DuplicatedId_ShouldFail()
    {
        TestHostManager testHostManager = new();
        testHostManager.AddTestHostApplicationLifetime(_ => new ApplicationLifecycleCallbacks("duplicatedId"));
        testHostManager.AddTestHostApplicationLifetime(_ => new ApplicationLifecycleCallbacks("duplicatedId"));
        InvalidOperationException invalidOperationException = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => testHostManager.BuildTestApplicationLifecycleCallbackAsync(_serviceProvider));
        Assert.IsTrue(invalidOperationException.Message.Contains("duplicatedId") && invalidOperationException.Message.Contains(typeof(ApplicationLifecycleCallbacks).ToString()));
    }

    [TestMethod]
    public async Task DataConsumer_DuplicatedId_ShouldFail()
    {
        TestHostManager testHostManager = new();
        testHostManager.AddDataConsumer(_ => new Consumer("duplicatedId"));
        testHostManager.AddDataConsumer(_ => new Consumer("duplicatedId"));
        InvalidOperationException invalidOperationException = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => testHostManager.BuildDataConsumersAsync(_serviceProvider, []));
        Assert.IsTrue(invalidOperationException.Message.Contains("duplicatedId") && invalidOperationException.Message.Contains(typeof(Consumer).ToString()));
    }

    [TestMethod]
    public async Task DataConsumer_DuplicatedIdWithCompositeFactory_ShouldFail()
    {
        TestHostManager testHostManager = new();
        CompositeExtensionFactory<Consumer> compositeExtensionFactory = new(() => new Consumer("duplicatedId"));
        testHostManager.AddDataConsumer(_ => new Consumer("duplicatedId"));
        testHostManager.AddDataConsumer(compositeExtensionFactory);
        InvalidOperationException invalidOperationException = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => testHostManager.BuildDataConsumersAsync(_serviceProvider, []));
        Assert.IsTrue(invalidOperationException.Message.Contains("duplicatedId") && invalidOperationException.Message.Contains(typeof(Consumer).ToString()));
    }

    [TestMethod]
    public async Task TestSessionLifetimeHandle_DuplicatedId_ShouldFail()
    {
        TestHostManager testHostManager = new();
        testHostManager.AddTestSessionLifetimeHandler(_ => new TestSessionLifetimeHandler("duplicatedId"));
        testHostManager.AddTestSessionLifetimeHandler(_ => new TestSessionLifetimeHandler("duplicatedId"));
        InvalidOperationException invalidOperationException = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => testHostManager.BuildTestSessionLifetimeHandleAsync(_serviceProvider, []));
        Assert.IsTrue(invalidOperationException.Message.Contains("duplicatedId") && invalidOperationException.Message.Contains(typeof(TestSessionLifetimeHandler).ToString()));
    }

    [TestMethod]
    public async Task TestSessionLifetimeHandle_DuplicatedIdWithCompositeFactory_ShouldFail()
    {
        TestHostManager testHostManager = new();
        CompositeExtensionFactory<TestSessionLifetimeHandler> compositeExtensionFactory = new(() => new TestSessionLifetimeHandler("duplicatedId"));
        testHostManager.AddTestSessionLifetimeHandler(_ => new TestSessionLifetimeHandler("duplicatedId"));
        testHostManager.AddTestSessionLifetimeHandler(compositeExtensionFactory);
        InvalidOperationException invalidOperationException = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => testHostManager.BuildTestSessionLifetimeHandleAsync(_serviceProvider, []));
        Assert.IsTrue(invalidOperationException.Message.Contains("duplicatedId") && invalidOperationException.Message.Contains(typeof(TestSessionLifetimeHandler).ToString()));
    }

    [DataRow(true)]
    [DataRow(false)]
    [TestMethod]
    public async Task TestHost_ComposeFactory_ShouldSucceed(bool withParameter)
    {
        TestHostManager testHostManager = new();
        CompositeExtensionFactory<TestSessionLifetimeHandlerPlusConsumer> compositeExtensionFactory =
            withParameter
            ? new(sp => new TestSessionLifetimeHandlerPlusConsumer(sp))
            : new(() => new TestSessionLifetimeHandlerPlusConsumer());
        testHostManager.AddTestSessionLifetimeHandler(compositeExtensionFactory);
        testHostManager.AddDataConsumer(compositeExtensionFactory);
        List<ICompositeExtensionFactory> compositeExtensions = [];
        IDataConsumer[] consumers = [.. (await testHostManager.BuildDataConsumersAsync(_serviceProvider, compositeExtensions)).Select(x => (IDataConsumer)x.Consumer)];
        ITestSessionLifetimeHandler[] sessionLifetimeHandle = [.. (await testHostManager.BuildTestSessionLifetimeHandleAsync(_serviceProvider, compositeExtensions)).Select(x => (ITestSessionLifetimeHandler)x.TestSessionLifetimeHandler)];
        Assert.HasCount(1, consumers);
        Assert.HasCount(1, sessionLifetimeHandle);
        Assert.AreEqual(compositeExtensions[0].GetInstance(), consumers[0]);
        Assert.AreEqual(compositeExtensions[0].GetInstance(), sessionLifetimeHandle[0]);
    }

    [TestMethod]
    public async Task TestHostControllerEnvironmentVariableProvider_DuplicatedId_ShouldFail()
    {
        TestHostControllersManager testHostControllerManager = new();
        testHostControllerManager.AddEnvironmentVariableProvider(_ => new TestHostEnvironmentVariableProvider("duplicatedId"));
        testHostControllerManager.AddEnvironmentVariableProvider(_ => new TestHostEnvironmentVariableProvider("duplicatedId"));
        InvalidOperationException invalidOperationException = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => testHostControllerManager.BuildAsync(_serviceProvider));
        Assert.IsTrue(invalidOperationException.Message.Contains("duplicatedId") && invalidOperationException.Message.Contains(typeof(TestHostEnvironmentVariableProvider).ToString()));
    }

    [TestMethod]
    public async Task TestHostControllerEnvironmentVariableProvider_DuplicatedIdWithCompositeFactory_ShouldFail()
    {
        TestHostControllersManager testHostControllerManager = new();
        CompositeExtensionFactory<TestHostEnvironmentVariableProvider> compositeExtensionFactory = new(() => new TestHostEnvironmentVariableProvider("duplicatedId"));
        testHostControllerManager.AddEnvironmentVariableProvider(_ => new TestHostEnvironmentVariableProvider("duplicatedId"));
        testHostControllerManager.AddEnvironmentVariableProvider(compositeExtensionFactory);
        InvalidOperationException invalidOperationException = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => testHostControllerManager.BuildAsync(_serviceProvider));
        Assert.IsTrue(invalidOperationException.Message.Contains("duplicatedId") && invalidOperationException.Message.Contains(typeof(TestHostEnvironmentVariableProvider).ToString()));
    }

    [TestMethod]
    public async Task TestHostControllerEnvironmentVariableProvider_InsertFirst_ShouldPreserveOrdering()
    {
        TestHostControllersManager testHostControllerManager = new();
        testHostControllerManager.AddEnvironmentVariableProvider(_ => new TestHostEnvironmentVariableProvider("second"));
        testHostControllerManager.AddEnvironmentVariableProviderFirst(_ => new TestHostEnvironmentVariableProvider("first"));

        TestHostControllerConfiguration configuration = await testHostControllerManager.BuildAsync(_serviceProvider);

        Assert.AreEqual("first", configuration.EnvironmentVariableProviders[0].Uid);
        Assert.AreEqual("second", configuration.EnvironmentVariableProviders[1].Uid);
    }

    [TestMethod]
    public async Task TestHostControllerProcessLifetimeHandler_DuplicatedId_ShouldFail()
    {
        TestHostControllersManager testHostControllerManager = new();
        testHostControllerManager.AddProcessLifetimeHandler(_ => new TestHostProcessLifetimeHandler("duplicatedId"));
        testHostControllerManager.AddProcessLifetimeHandler(_ => new TestHostProcessLifetimeHandler("duplicatedId"));
        InvalidOperationException invalidOperationException = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => testHostControllerManager.BuildAsync(_serviceProvider));
        Assert.IsTrue(invalidOperationException.Message.Contains("duplicatedId") && invalidOperationException.Message.Contains(typeof(TestHostProcessLifetimeHandler).ToString()));
    }

    [TestMethod]
    public async Task TestHostControllerProcessLifetimeHandler_DuplicatedIdWithCompositeFactory_ShouldFail()
    {
        TestHostControllersManager testHostControllerManager = new();
        CompositeExtensionFactory<TestHostProcessLifetimeHandler> compositeExtensionFactory = new(() => new TestHostProcessLifetimeHandler("duplicatedId"));
        testHostControllerManager.AddProcessLifetimeHandler(_ => new TestHostProcessLifetimeHandler("duplicatedId"));
        testHostControllerManager.AddProcessLifetimeHandler(compositeExtensionFactory);
        InvalidOperationException invalidOperationException = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => testHostControllerManager.BuildAsync(_serviceProvider));
        Assert.IsTrue(invalidOperationException.Message.Contains("duplicatedId") && invalidOperationException.Message.Contains(typeof(TestHostProcessLifetimeHandler).ToString()));
    }

    [TestMethod]
    public async Task TestHostControllerProcessLifetimeHandler_FinalizationTokenStartsUncanceled()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        bool? wasCanceled = null;

        bool finalized = await TryRunControllerExtensionAsync(
            token =>
            {
                wasCanceled = token.IsCancellationRequested;
                return Task.CompletedTask;
            },
            cancellationTokenSource.Token);

        Assert.IsTrue(finalized);
        Assert.IsFalse(wasCanceled);
    }

    [TestMethod]
    public async Task TestHostControllerProcessLifetimeHandler_FinalizationIsBounded()
    {
        using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(2));
        using ManualResetEventSlim releaseFinalization = new(initialState: false);
        try
        {
            bool finalized = await TryRunControllerExtensionAsync(
                _ =>
                {
                    releaseFinalization.Wait(CancellationToken.None);
                    return Task.CompletedTask;
                },
                cancellationTokenSource.Token);

            Assert.IsFalse(finalized);
        }
        finally
        {
            releaseFinalization.Set();
        }
    }

    [TestMethod]
    public async Task TestHostControllerProcessTermination_WaitIsBounded()
    {
        Mock<IProcess> process = new();
        TaskCompletionSource<bool> neverExits = new(TaskCreationOptions.RunContinuationsAsynchronously);
        process.Setup(x => x.WaitForExitAsync(It.IsAny<CancellationToken>())).Returns(neverExits.Task);
        var stopwatch = Stopwatch.StartNew();

        bool exited = await WaitForExitAfterTerminationAsync(process.Object, TimeSpan.FromMilliseconds(100), new NopLogger());

        Assert.IsFalse(exited);
        Assert.IsLessThan(5, stopwatch.Elapsed.TotalSeconds);
    }

    [TestMethod]
    public async Task TestHostControllerConnectionTimeout_IsHandledWithoutThrowing()
    {
        bool timeoutHandled = false;

        bool connected = await TestHostControllersTestHost.WaitForTestHostControllerConnectionAsync(
            token => Task.Delay(Timeout.InfiniteTimeSpan, token),
            timeoutSeconds: 0.01,
            CancellationToken.None,
            () =>
            {
                timeoutHandled = true;
                return Task.CompletedTask;
            });

        Assert.IsFalse(connected);
        Assert.IsTrue(timeoutHandled);
    }

    [TestMethod]
    public async Task TestHostControllerConnectionTimeout_PreservesApplicationCancellation()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => TestHostControllersTestHost.WaitForTestHostControllerConnectionAsync(
                token => Task.Delay(Timeout.InfiniteTimeSpan, token),
                timeoutSeconds: 1,
                cancellationTokenSource.Token,
                () => throw new AssertFailedException("The connection timeout handler should not run for application cancellation.")));
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task ControllerProcessEarlyExitPreservesActualDiagnosticAndDoesNotStartWaitOrTermination(bool pidUnavailable, bool telemetry)
    {
        var process = new Mock<IProcess>();
        process.SetupGet(value => value.HasExited).Returns(true);
        process.SetupGet(value => value.ExitCode).Returns(17);
        if (pidUnavailable)
        {
            process.SetupGet(value => value.Id).Throws(new InvalidOperationException("No process associated."));
        }
        else
        {
            process.SetupGet(value => value.Id).Returns(37);
        }

        var launcher = new Mock<IProcessHandler>();
        launcher.Setup(value => value.Start(It.IsAny<ProcessStartInfo>())).Returns(process.Object);
        ServiceProvider services = new();
        using var cancellation = new CTRLPlusCCancellationTokenSource(environment: Mock.Of<IEnvironment>());
        services.AddService(cancellation);
        services.AddService(new SystemTask());
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(value => value.CreateLogger(It.IsAny<string>())).Returns(new NopLogger());
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTimeOffset(2026, 1, 1, 3, 4, 5, TimeSpan.Zero).AddMilliseconds(123));
        using var host = new TestHostControllersTestHost(
            new([], [], [], testHostLauncher: null, requireProcessRestart: true),
            services,
            passiveNode: null,
            Mock.Of<IEnvironment>(),
            loggerFactory.Object,
            clock.Object);
        var output = new Mock<IPlatformOutputDevice>();
        var messages = new List<IOutputDeviceData>();
        var tokens = new List<CancellationToken>();
        output.Setup(value => value.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>((_, message, token) =>
            {
                messages.Add(message);
                tokens.Add(token);
            }).Returns(Task.CompletedTask);
        var telemetryInfo = new Mock<ITelemetryInformation>();
        telemetryInfo.SetupGet(value => value.IsEnabled).Returns(telemetry);
        ProcessStartInfo startInfo = new("not-launched");

        (int exit, TestHostProcessInformation info, string? extensions) = await RunTestHostProcessAsync(
            host, startInfo, [], 42, launcher.Object, Mock.Of<IConfiguration>(),
            testHostControllerIpc: null!, testHostControllerCancellationServer: null!,
            new ProxyOutputDevice(output.Object, serverModeOutputDevice: null), telemetryInfo.Object, CancellationToken.None);

        Assert.AreEqual(1, exit);
        Assert.AreEqual(pidUnavailable ? 0 : 37, info.PID);
        Assert.AreEqual(1, info.ExitCode);
        Assert.IsFalse(info.HasExitedGracefully);
        Assert.AreEqual(telemetry ? "[]" : null, extensions);
        Assert.AreEqual(
            "The test host process did not connect to the controller's named pipe after '0' seconds. The test host process exited with code '17'.",
            Assert.IsInstanceOfType<ErrorMessageOutputDeviceData>(Assert.ContainsSingle(messages)).Message);
        Assert.AreEqual(CancellationToken.None, Assert.ContainsSingle(tokens));
        Assert.AreEqual("03:04:05.123", startInfo.EnvironmentVariables[EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_TESTHOSTPROCESSSTARTTIME + "_42"]);
        process.Verify(value => value.Kill(), Times.Never);
        process.Verify(value => value.WaitForExitAsync(It.IsAny<CancellationToken>()), Times.Never);
        process.Verify(value => value.Dispose(), Times.Once);
        launcher.Verify(value => value.Start(startInfo), Times.Once);
    }

    [TestMethod]
    public void TestHostControllerProcessExit_DisposedCancellationTokenSourceIsIgnored()
    {
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Dispose();

        TestHostControllersTestHost.TryCancelTestHostExitCancellationTokenSource(cancellationTokenSource, new NopLogger());
    }

    [TestMethod]
    public async Task TestHostControllerConnectionTimeout_TerminatesCustomHandleDefersDisposalAndReturnsFailure()
    {
        TaskCompletionSource<bool> exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<ITestHostHandle> handle = new();
        handle.SetupGet(x => x.Identifier).Returns("custom-host");
        handle.SetupGet(x => x.HasExited).Returns(() => exited.Task.IsCompleted);
        handle.SetupSequence(x => x.WaitForExitAsync(It.IsAny<CancellationToken>()))
            .Returns(exited.Task)
            .ThrowsAsync(new IOException("remote wait failed"))
            .Returns(exited.Task);
        handle.Setup(x => x.Dispose()).Callback(() => disposed.TrySetResult(true));

        Mock<ITestHostLauncher> launcher = new();
        launcher.SetupGet(x => x.Uid).Returns("custom-launcher");
        launcher.SetupGet(x => x.DisplayName).Returns("Custom launcher");
        launcher.Setup(x => x.LaunchTestHostAsync(It.IsAny<TestHostLaunchContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(handle.Object);

        ServiceProvider serviceProvider = new();
        using var cancellationTokenSource = new CTRLPlusCCancellationTokenSource();
        serviceProvider.AddService(cancellationTokenSource);
        var task = new SystemTask();
        serviceProvider.AddService(task);
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new NopLogger());
        Mock<IClock> clock = new();
        clock.SetupGet(x => x.UtcNow).Returns(DateTimeOffset.UtcNow);
        using var host = new TestHostControllersTestHost(
            new([], [], [], launcher.Object, requireProcessRestart: true),
            serviceProvider,
            passiveNode: null,
            new SystemEnvironment(),
            loggerFactory.Object,
            clock.Object);
        Mock<IConfiguration> configuration = new();
        configuration
            .SetupGet(x => x[PlatformConfigurationConstants.PlatformTestHostControllersManagerSingleConnectionNamedPipeServerWaitConnectionTimeoutSeconds])
            .Returns("0.01");
        var outputDevice = new ProxyOutputDevice(new NopPlatformOutputDevice(), serverModeOutputDevice: null);
        var telemetryInformation = new TelemetryInformation(isEnabled: false, version: "test");
        using IDisposable testHostControllerIpc = CreateTestHostControllerIpc(host, CancellationToken.None);
        using var cancellationServer = new TestHostControllerCancellationServer(
            authorizedSecurityIdentities: null,
            new SystemEnvironment(),
            loggerFactory.Object,
            task);

        (int exitCode, TestHostProcessInformation processInformation, _) = await RunTestHostProcessAsync(
            host,
            new ProcessStartInfo("custom-test-host"),
            [],
            currentPid: 42,
            Mock.Of<IProcessHandler>(),
            configuration.Object,
            testHostControllerIpc,
            cancellationServer,
            outputDevice,
            telemetryInformation,
            CancellationToken.None);

        Assert.AreEqual((int)ExitCode.GenericFailure, exitCode);
        Assert.AreEqual((int)ExitCode.GenericFailure, processInformation.ExitCode);
        Assert.IsFalse(processInformation.HasExitedGracefully);
        handle.Verify(x => x.Terminate(), Times.Once);
        handle.Verify(x => x.Dispose(), Times.Never);

        exited.SetResult(true);
        await disposed.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
        handle.Verify(x => x.Dispose(), Times.Once);
    }

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates.
    [TestMethod]
    public void ControllerRegistration_NullOrdinaryFactoriesAreRejectedWithExactParameterNames()
    {
        TestHostControllersManager manager = new();
        Assert.AreEqual("environmentVariableProviderFactory", Assert.ThrowsExactly<ArgumentNullException>(
            () => manager.AddEnvironmentVariableProvider(null!)).ParamName);
        Assert.AreEqual("environmentVariableProviderFactory", Assert.ThrowsExactly<ArgumentNullException>(
            () => manager.AddEnvironmentVariableProviderFirst(null!)).ParamName);
        Assert.AreEqual("lifetimeHandler", Assert.ThrowsExactly<ArgumentNullException>(
            () => manager.AddProcessLifetimeHandler(null!)).ParamName);
        Assert.AreEqual("testHostLauncherFactory", Assert.ThrowsExactly<ArgumentNullException>(
            () => manager.AddTestHostLauncher(null!)).ParamName);
    }

    [TestMethod]
    public void TestHostControllerProcessExit_NonAuthoritativeAbortedExitCodePreservesSuccessfulIpcResult()
    {
        Mock<ITestHostHandle> handle = new();
        handle.SetupGet(x => x.ExitCode).Returns((int)ExitCode.TestSessionAborted);
        handle.Setup(x => x.WaitForExitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        handle.As<ITestHostHandleExitCodePolicy>().SetupGet(x => x.IsExitCodeAuthoritative).Returns(false);
        using var adapter = new TestHostHandleToProcessAdapter(handle.Object);

        (bool testExecutionCanceled, int reportedTestHostExitCode) = TestHostControllersTestHost.ResolveTestHostExitState(
            applicationCancellationRequested: false,
            testHostUnfilteredExitCodeReceived: (int)ExitCode.Success,
            testHostExitCodeReceived: (int)ExitCode.Success,
            adapter.ExitCode,
            adapter.IsExitCodeAuthoritative);

        Assert.IsFalse(testExecutionCanceled);
        Assert.AreEqual((int)ExitCode.Success, reportedTestHostExitCode);
    }
#pragma warning restore TPEXP

    [TestMethod]
    public void TestHostControllerConnectionFailureMessage_ReportsProcessState()
    {
        Mock<IProcess> process = new();
        process.SetupGet(x => x.HasExited).Returns(true);
        process.SetupGet(x => x.ExitCode).Returns(134);

        string message = TestHostControllersTestHost.CreateTestHostControllerConnectionFailureMessage(
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(5),
            process.Object);

        Assert.Contains("after '3' seconds", message);
        Assert.Contains("timeout was '5' seconds", message);
        Assert.Contains("exited with code '134'", message);
    }

    [TestMethod]
    public void TestHostControllerConnectionFailureMessage_ReportsRunningProcess()
    {
        Mock<IProcess> process = new();
        process.SetupGet(x => x.HasExited).Returns(false);

        string message = TestHostControllersTestHost.CreateTestHostControllerConnectionFailureMessage(
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(5),
            process.Object);

        Assert.Contains("after '3' seconds", message);
        Assert.Contains("timeout was '5' seconds", message);
        Assert.Contains("still running", message);
    }

    [TestMethod]
    public async Task TestHostControllerProcessTermination_FaultedWaitAdvancesToTermination()
    {
        Mock<IProcess> process = new();
        process.Setup(x => x.WaitForExitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("remote wait failed"));
        bool cancellationRequested = false;

        await TestHostControllersTestHost.HandleCanceledTestHostAsync(
            process.Object,
            () => cancellationRequested = true,
            new NopLogger(),
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(50));

        Assert.IsTrue(cancellationRequested);
        process.Verify(x => x.Kill(), Times.Once);
    }

    [TestMethod]
    public async Task TestHostControllerProcessTermination_UnresponsiveCustomHandleIsTerminatedAndDeferred()
    {
        TaskCompletionSource<bool> exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<ITestHostHandle> handle = new();
        handle.SetupGet(x => x.HasExited).Returns(() => exited.Task.IsCompleted);
        handle.Setup(x => x.WaitForExitAsync(It.IsAny<CancellationToken>())).Returns(exited.Task);
        handle.Setup(x => x.Dispose()).Callback(() => disposed.TrySetResult(true));
        var adapter = new TestHostHandleToProcessAdapter(handle.Object);
        bool cancellationRequested = false;

        await TestHostControllersTestHost.HandleCanceledTestHostAsync(
            adapter,
            () => cancellationRequested = true,
            new NopLogger(),
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(50));

        Assert.IsTrue(cancellationRequested);
        handle.Verify(x => x.Terminate(), Times.Once);

        adapter.Dispose();
        handle.Verify(x => x.Dispose(), Times.Never);

        exited.SetResult(true);
        await disposed.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
        handle.Verify(x => x.Dispose(), Times.Once);
    }

    [TestMethod]
    public async Task TestHostControllerConnectionFailure_UnresponsiveCustomHandleIsTerminatedAndDeferred()
    {
        TaskCompletionSource<bool> exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<ITestHostHandle> handle = new();
        handle.SetupGet(x => x.HasExited).Returns(() => exited.Task.IsCompleted);
        handle.Setup(x => x.WaitForExitAsync(It.IsAny<CancellationToken>())).Returns(exited.Task);
        handle.Setup(x => x.Dispose()).Callback(() => disposed.TrySetResult(true));
        var adapter = new TestHostHandleToProcessAdapter(handle.Object);

        await TestHostControllersTestHost.TerminateTestHostAfterConnectionFailureAsync(
            adapter,
            new NopLogger(),
            TimeSpan.FromMilliseconds(50));

        handle.Verify(x => x.Terminate(), Times.Once);

        adapter.Dispose();
        handle.Verify(x => x.Dispose(), Times.Never);

        exited.SetResult(true);
        await disposed.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
        handle.Verify(x => x.Dispose(), Times.Once);
    }

    [TestMethod]
    public async Task TestHostControllerProcessTermination_CancellationDuringStartupUsesTeardownPath()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        cancellationTokenSource.Cancel();
        Mock<IProcess> process = new();
        TaskCompletionSource<bool> neverExits = new(TaskCreationOptions.RunContinuationsAsynchronously);
        process.Setup(x => x.WaitForExitAsync(It.IsAny<CancellationToken>())).Returns(neverExits.Task);
        bool cancellationRequested = false;

        await TestHostControllersTestHost.RunWithCancellationTeardownAsync(
            () => Task.FromCanceled(cancellationTokenSource.Token),
            cancellationTokenSource.Token,
            process.Object,
            () => cancellationRequested = true,
            new NopLogger(),
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(50));

        Assert.IsTrue(cancellationRequested);
        process.Verify(x => x.Kill(), Times.Once);
    }

    [TestMethod]
    public async Task TestHostControlledHost_LateCooperativeCancellationPreservesCompletionToken()
    {
        using CancellationTokenSource applicationCancellationTokenSource = new();
        using CancellationTokenSource completionCancellationTokenSource = new();
        TaskCompletionSource<bool> cancellationObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration completionRegistration =
            completionCancellationTokenSource.Token.Register(() => cancellationObserved.TrySetResult(true));
        bool cooperativeCancellation = false;
        using CancellationTokenRegistration registration = TestHostControlledHost.RegisterCompletionCancellationTransition(
            applicationCancellationTokenSource.Token,
            () => cooperativeCancellation,
            completionCancellationTokenSource,
            TimeSpan.FromMilliseconds(20));

        cooperativeCancellation = true;
        applicationCancellationTokenSource.Cancel();

        Assert.IsFalse(completionCancellationTokenSource.IsCancellationRequested);
        await cancellationObserved.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
        Assert.IsTrue(completionCancellationTokenSource.IsCancellationRequested);
    }

    [TestMethod]
    public void TestHostControlledHost_LateNonCooperativeCancellationCancelsCompletionToken()
    {
        using CancellationTokenSource applicationCancellationTokenSource = new();
        using CancellationTokenSource completionCancellationTokenSource = new();
        using CancellationTokenRegistration registration = TestHostControlledHost.RegisterCompletionCancellationTransition(
            applicationCancellationTokenSource.Token,
            () => false,
            completionCancellationTokenSource,
            TimeoutHelper.DefaultHangTimeSpanTimeout);

        applicationCancellationTokenSource.Cancel();

        Assert.IsTrue(completionCancellationTokenSource.IsCancellationRequested);
    }

    [TestMethod]
    public void TestHostControllerProcessTermination_CooperativeBudgetUsesConfiguredCanceledConsumerBudget()
    {
        Mock<IEnvironment> environment = new();
        environment
            .Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_MESSAGEBUS_CANCELED_SHUTDOWN_TIMEOUT_SECONDS))
            .Returns("60");
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new NopLogger());
        using var host = new TestHostControllersTestHost(
            new([], [], [], testHostLauncher: null, requireProcessRestart: false),
            new(),
            passiveNode: null,
            environment.Object,
            loggerFactory.Object,
            Mock.Of<IClock>());

        Assert.AreEqual(TimeSpan.FromSeconds(75), host.TestHostCooperativeShutdownTimeout);
    }

    [TestMethod]
    public async Task TestHostControllerProcessTermination_CustomHandleDisposalWaitsForExit()
    {
        TaskCompletionSource<bool> exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<ITestHostHandle> handle = new();
        handle.SetupGet(x => x.HasExited).Returns(() => exited.Task.IsCompleted);
        handle.Setup(x => x.WaitForExitAsync(It.IsAny<CancellationToken>())).Returns(exited.Task);
        handle.Setup(x => x.Dispose()).Callback(() => disposed.TrySetResult(true));
        var adapter = new TestHostHandleToProcessAdapter(handle.Object);

        adapter.DeferDisposalUntilExit();
        adapter.Dispose();

        handle.Verify(x => x.Dispose(), Times.Never);

        exited.SetResult(true);
        await disposed.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);

        handle.Verify(x => x.Dispose(), Times.Once);
    }

    [TestMethod]
    public void TestHostControllerProcessTermination_CustomHandleDisposalIsImmediateByDefault()
    {
        Mock<ITestHostHandle> handle = new();
        handle.SetupGet(x => x.HasExited).Returns(false);
        handle.Setup(x => x.WaitForExitAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken token) => Task.Delay(Timeout.InfiniteTimeSpan, token));
        var adapter = new TestHostHandleToProcessAdapter(handle.Object);

        adapter.Dispose();

        handle.Verify(x => x.Dispose(), Times.Once);
    }

    [TestMethod]
    public void TestHostControllerOutputFinalization_AbandonmentTracksProxyAndOriginalDevice()
    {
        Mock<IPlatformOutputDevice> originalOutputDevice = new();
        ProxyOutputDevice proxyOutputDevice = new(originalOutputDevice.Object, serverModeOutputDevice: null);
        List<object> servicesStillRunning = [];

        MarkOutputDeviceStillRunning(servicesStillRunning, proxyOutputDevice);

        Assert.HasCount(2, servicesStillRunning);
        Assert.Contains(proxyOutputDevice, servicesStillRunning);
        Assert.Contains(originalOutputDevice.Object, servicesStillRunning);
    }

    [TestMethod]
    public void TestHostControllerFinalization_LateCancellationArmsSharedBoundedTokenSource()
    {
        Mock<IEnvironment> environment = new();
        environment
            .Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_FINALIZATION_TIMEOUT_SECONDS))
            .Returns("0.1");
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        using var host = new TestHostControllersTestHost(
            new([], [], [], testHostLauncher: null, requireProcessRestart: false),
            new(),
            passiveNode: null,
            environment.Object,
            loggerFactory.Object,
            Mock.Of<IClock>());

        CancellationTokenSource first = EnsureControllerFinalizationCancellationTokenSource(host);
        CancellationTokenSource second = EnsureControllerFinalizationCancellationTokenSource(host);

        Assert.IsFalse(first.Token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(500)));

        ArmControllerFinalizationTimeout(host);

        Assert.AreSame(first, second);
        Assert.IsTrue(first.Token.WaitHandle.WaitOne(TimeoutHelper.DefaultHangTimeSpanTimeout));
    }

    [TestMethod]
    public async Task TestHostControllerFinalization_CancellationDuringCleanupArmsStableToken()
    {
        Mock<IEnvironment> environment = new();
        environment
            .Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_FINALIZATION_TIMEOUT_SECONDS))
            .Returns("0.1");
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        using var host = new TestHostControllersTestHost(
            new([], [], [], testHostLauncher: null, requireProcessRestart: false),
            new(),
            passiveNode: null,
            environment.Object,
            loggerFactory.Object,
            Mock.Of<IClock>());
        using CancellationTokenSource applicationCancellationTokenSource = new();
        TaskCompletionSource<bool> cleanupStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> releaseCleanup = new(TaskCreationOptions.RunContinuationsAsynchronously);
        EnsureControllerFinalizationCancellationTokenSource(host);
        RegisterControllerFinalizationTransition(host, applicationCancellationTokenSource.Token);

        try
        {
            Task<bool> cleanup = TryRunControllerCleanupAsync(
                host,
                () =>
                {
                    cleanupStarted.TrySetResult(true);
                    return releaseCleanup.Task;
                });
            await cleanupStarted.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);

#pragma warning disable VSTHRD103 // CancelAsync is unavailable on net462, which this project also targets.
            applicationCancellationTokenSource.Cancel();
#pragma warning restore VSTHRD103

            await cleanup.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
            Assert.IsFalse(await cleanup);
        }
        finally
        {
            releaseCleanup.TrySetResult(true);
        }
    }

    [DataRow(true)]
    [DataRow(false)]
    [TestMethod]
    public async Task TestHostController_ComposeFactory_ShouldSucceed(bool withParameter)
    {
        TestHostControllersManager testHostControllerManager = new();
        CompositeExtensionFactory<TestHostProcessLifetimeHandlerPlusTestHostEnvironmentVariableProvider> compositeExtensionFactory =
            withParameter
            ? new(sp => new TestHostProcessLifetimeHandlerPlusTestHostEnvironmentVariableProvider(sp))
            : new(() => new TestHostProcessLifetimeHandlerPlusTestHostEnvironmentVariableProvider());
        testHostControllerManager.AddEnvironmentVariableProvider(compositeExtensionFactory);
        testHostControllerManager.AddProcessLifetimeHandler(compositeExtensionFactory);
        TestHostControllerConfiguration configuration = await testHostControllerManager.BuildAsync(_serviceProvider);
        Assert.IsTrue(configuration.RequireProcessRestart);
        Assert.HasCount(1, configuration.LifetimeHandlers);
        Assert.HasCount(1, configuration.EnvironmentVariableProviders);
        Assert.AreEqual((object)configuration.LifetimeHandlers[0], configuration.EnvironmentVariableProviders[0]);
        Assert.AreEqual(((ICompositeExtensionFactory)compositeExtensionFactory).GetInstance(), configuration.LifetimeHandlers[0]);
        Assert.AreEqual(((ICompositeExtensionFactory)compositeExtensionFactory).GetInstance(), configuration.EnvironmentVariableProviders[0]);
    }

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates.
    [TestMethod]
    [DataRow("environment")]
    [DataRow("lifetime")]
    [DataRow("launcher")]
    [DataRow("consumer")]
    public void ControllerRegistration_NullCompositeFactoryIsRejectedBeforeBuild(string kind)
    {
        TestHostControllersManager manager = new();
        Action register = kind switch
        {
            "environment" => () => manager.AddEnvironmentVariableProvider((CompositeExtensionFactory<TestHostEnvironmentVariableProvider>)null!),
            "lifetime" => () => manager.AddProcessLifetimeHandler((CompositeExtensionFactory<TestHostProcessLifetimeHandler>)null!),
            "launcher" => () => manager.AddTestHostLauncher((CompositeExtensionFactory<TestHostLauncher>)null!),
            _ => () => manager.AddDataConsumer((CompositeExtensionFactory<Consumer>)null!),
        };
        ArgumentNullException error = Assert.ThrowsExactly<ArgumentNullException>(register);
        Assert.AreEqual("compositeServiceFactory", error.ParamName);
    }

    [TestMethod]
    [DataRow("environment")]
    [DataRow("lifetime")]
    [DataRow("launcher")]
    [DataRow("consumer")]
    public void ControllerRegistration_SameCompositeFactoryCannotBeRegisteredTwiceInOneCategory(string kind)
    {
        TestHostControllersManager manager = new();
        Action register;
        if (kind == "environment")
        {
            CompositeExtensionFactory<TestHostEnvironmentVariableProvider> factory = new(() => new TestHostEnvironmentVariableProvider("environment"));
            register = () => manager.AddEnvironmentVariableProvider(factory);
        }
        else if (kind == "lifetime")
        {
            CompositeExtensionFactory<TestHostProcessLifetimeHandler> factory = new(() => new TestHostProcessLifetimeHandler("lifetime"));
            register = () => manager.AddProcessLifetimeHandler(factory);
        }
        else if (kind == "launcher")
        {
            CompositeExtensionFactory<TestHostLauncher> factory = new(() => new TestHostLauncher("launcher"));
            register = () => manager.AddTestHostLauncher(factory);
        }
        else
        {
            CompositeExtensionFactory<Consumer> factory = new(() => new Consumer("consumer"));
            register = () => manager.AddDataConsumer(factory);
        }

        register();
        ArgumentException error = Assert.ThrowsExactly<ArgumentException>(register);
        Assert.Contains("already", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("registered", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ControllerRegistration_CompositeLauncherIsInitializedAndStoredOnlyWhenEnabled(bool enabled)
    {
        TestHostControllersManager manager = new();
        var launcher = new Mock<ITestHostLauncher>();
        launcher.SetupGet(value => value.Uid).Returns("composite.launcher");
        launcher.Setup(value => value.IsEnabledAsync()).ReturnsAsync(enabled);
        Mock<IAsyncInitializableExtension> initialization = launcher.As<IAsyncInitializableExtension>();
        initialization.Setup(value => value.InitializeAsync()).Returns(Task.CompletedTask);
        int constructions = 0;
        CompositeExtensionFactory<ITestHostLauncher> factory = new(serviceProvider =>
        {
            Assert.AreSame(_serviceProvider, serviceProvider);
            constructions++;
            return launcher.Object;
        });
        manager.AddTestHostLauncher(factory);

        TestHostControllerConfiguration first = await manager.BuildAsync(_serviceProvider);
        TestHostControllerConfiguration second = await manager.BuildAsync(_serviceProvider);

        Assert.AreEqual(enabled, first.RequireProcessRestart);
        Assert.AreEqual(enabled, second.RequireProcessRestart);
        Assert.AreSame(enabled ? launcher.Object : null, first.TestHostLauncher);
        Assert.AreSame(first.TestHostLauncher, second.TestHostLauncher);
        Assert.AreEqual(1, constructions);
        initialization.Verify(value => value.InitializeAsync(), enabled ? Times.Once : Times.Never);
        Assert.HasCount(enabled ? 1 : 0, _serviceProvider.Services.Where(value => ReferenceEquals(value, launcher.Object)));
        launcher.Verify(value => value.LaunchTestHostAsync(It.IsAny<TestHostLaunchContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ControllerRegistration_CompositeLaunchersRejectDuplicateIdentityAndMultipleEnabledInstances(bool sameIdentity)
    {
        TestHostControllersManager manager = new();
        CompositeExtensionFactory<TestHostLauncher> first = new(() => new TestHostLauncher("first"));
        CompositeExtensionFactory<TestHostLauncher> second = new(() => new TestHostLauncher(sameIdentity ? "first" : "second"));
        manager.AddTestHostLauncher(first);
        manager.AddTestHostLauncher(second);

        InvalidOperationException error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => manager.BuildAsync(_serviceProvider));

        if (sameIdentity)
        {
            Assert.Contains("first", error.Message);
            Assert.Contains(typeof(TestHostLauncher).ToString(), error.Message);
        }
        else
        {
            Assert.Contains("one", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("launcher", error.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [TestMethod]
    public async Task ControllerRegistration_CompositeAndOrdinaryFactoriesPreserveRelativeOrderAndFirstProviderPriority()
    {
        TestHostControllersManager manager = new();
        manager.AddEnvironmentVariableProvider(_ => new TestHostEnvironmentVariableProvider("ordinary.first"));
        CompositeExtensionFactory<TestHostEnvironmentVariableProvider> composite = new(() => new TestHostEnvironmentVariableProvider("composite"));
        manager.AddEnvironmentVariableProvider(composite);
        manager.AddEnvironmentVariableProvider(_ => new TestHostEnvironmentVariableProvider("ordinary.last"));
        manager.AddEnvironmentVariableProviderFirst(_ => new TestHostEnvironmentVariableProvider("priority"));
        manager.AddProcessLifetimeHandler(_ => new TestHostProcessLifetimeHandler("ordinary.handler"));
        CompositeExtensionFactory<TestHostProcessLifetimeHandler> handler = new(() => new TestHostProcessLifetimeHandler("composite.handler"));
        manager.AddProcessLifetimeHandler(handler);
        Consumer? createdConsumer = null;
        CompositeExtensionFactory<Consumer> consumer = new(() => createdConsumer = new Consumer("controller.consumer"));
        manager.AddDataConsumer(consumer);

        TestHostControllerConfiguration configuration = await manager.BuildAsync(_serviceProvider);

        Assert.AreSequenceEqual(
            ["priority", "ordinary.first", "composite", "ordinary.last"],
            configuration.EnvironmentVariableProviders.Select(value => value.Uid));
        Assert.AreSequenceEqual(["ordinary.handler", "composite.handler"], configuration.LifetimeHandlers.Select(value => value.Uid));
        Assert.AreEqual("controller.consumer", Assert.ContainsSingle(configuration.DataConsumer).Uid);
        Assert.AreSame(createdConsumer, configuration.DataConsumer[0], "Data consumer registration clones its composite factory but must register the actual produced instance.");
        Assert.IsTrue(configuration.RequireProcessRestart);
        Assert.IsNull(configuration.TestHostLauncher);
    }

    [TestMethod]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task ControllerRegistration_CompletionObserversDoNotIntroduceControllerAndHonorEnabledState(bool controller, bool enabled)
    {
        TestHostControllersManager manager = new();
        var observer = new Mock<ITestHostControllerRunCompletionHandler>();
        observer.SetupGet(value => value.Uid).Returns("completion.observer");
        observer.Setup(value => value.IsEnabledAsync()).ReturnsAsync(enabled);
        int constructions = 0;
        manager.AddRunCompletionHandler(_ =>
        {
            constructions++;
            return observer.Object;
        });
        if (controller)
        {
            manager.AddEnvironmentVariableProvider(_ => new TestHostEnvironmentVariableProvider("existing.controller"));
        }

        TestHostControllerConfiguration configuration = await manager.BuildAsync(_serviceProvider);

        Assert.AreEqual(controller, configuration.RequireProcessRestart);
        Assert.AreEqual(controller ? 1 : 0, constructions);
        Assert.HasCount(controller && enabled ? 1 : 0, configuration.RunCompletionHandlers);
        if (controller && enabled)
        {
            Assert.AreSame(observer.Object, configuration.RunCompletionHandlers[0]);
        }
    }

    [TestMethod]
    public async Task TestHostLauncher_WhenRegistered_ForcesProcessRestartAndIsStored()
    {
        TestHostControllersManager testHostControllerManager = new();
        TestHostLauncher launcher = new("launcher");
        testHostControllerManager.AddTestHostLauncher(_ => launcher);
        TestHostControllerConfiguration configuration = await testHostControllerManager.BuildAsync(_serviceProvider);
        Assert.IsTrue(configuration.RequireProcessRestart);
        Assert.AreEqual((object)launcher, configuration.TestHostLauncher);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "AppContainer pipe authorization is Windows-only.")]
    public async Task TestHostLauncher_AuthorizationConsumerResolvesAndStoresIdentity()
    {
        TestHostControllersManager testHostControllerManager = new();
        TestHostLauncher launcher = new("launcher");
        testHostControllerManager.AddTestHostLauncher(_ => launcher);

        ITestHostLauncher? builtLauncher = await testHostControllerManager.BuildTestHostLauncherAsync(_serviceProvider);
        IReadOnlyList<string>? authorizedSecurityIdentities =
            await _serviceProvider.ResolveTestHostControllerAuthorizedSecurityIdentitiesAsync(
                builtLauncher,
                "testhost.exe",
                new NopLogger(),
                CancellationToken.None);

        Assert.AreSequenceEqual([ContosoPackageSid], authorizedSecurityIdentities);
        Assert.AreSame(authorizedSecurityIdentities, _serviceProvider.TestHostControllerAuthorizedSecurityIdentities);
    }

    [TestMethod]
    public async Task TestHostLauncher_MultipleRegistered_ShouldFail()
    {
        TestHostControllersManager testHostControllerManager = new();
        testHostControllerManager.AddTestHostLauncher(_ => new TestHostLauncher("launcher1"));
        testHostControllerManager.AddTestHostLauncher(_ => new TestHostLauncher("launcher2"));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => testHostControllerManager.BuildAsync(_serviceProvider));
    }

    [TestMethod]
    public async Task TestHostLauncher_DuplicatedId_ShouldFail()
    {
        TestHostControllersManager testHostControllerManager = new();
        testHostControllerManager.AddTestHostLauncher(_ => new TestHostLauncher("duplicatedId"));
        testHostControllerManager.AddTestHostLauncher(_ => new TestHostLauncher("duplicatedId"));
        InvalidOperationException invalidOperationException = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => testHostControllerManager.BuildAsync(_serviceProvider));
        Assert.IsTrue(invalidOperationException.Message.Contains("duplicatedId") && invalidOperationException.Message.Contains(typeof(TestHostLauncher).ToString()));
    }
#pragma warning restore TPEXP

    [DataRow(true)]
    [DataRow(false)]
    [TestMethod]
    public void ComposeFactory_InvalidComposition_ShouldFail(bool withParameter)
    {
        CompositeExtensionFactory<InvalidComposition> compositeExtensionFactory =
            withParameter
            ? new CompositeExtensionFactory<InvalidComposition>(sp => new InvalidComposition(sp))
            : new CompositeExtensionFactory<InvalidComposition>(() => new InvalidComposition());
        InvalidOperationException invalidOperationException = Assert.ThrowsExactly<InvalidOperationException>(() => ((ICompositeExtensionFactory)compositeExtensionFactory).GetInstance());
        Assert.AreEqual(CompositeExtensionFactory<InvalidComposition>.ValidateCompositionErrorMessage, invalidOperationException.Message);
    }

    private static Task<bool> TryRunControllerExtensionAsync(
        Func<CancellationToken, Task> finalization,
        CancellationToken cancellationToken)
    {
        MethodInfo method = typeof(TestHostControllersTestHost).GetMethod(
            "TryRunControllerExtensionAsync",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not find TestHostControllersTestHost.TryRunControllerExtensionAsync.");
        return (Task<bool>?)method.Invoke(null, [finalization, cancellationToken])
            ?? throw new InvalidOperationException("TestHostControllersTestHost.TryRunControllerExtensionAsync returned null.");
    }

    private static Task<bool> WaitForExitAfterTerminationAsync(IProcess process, TimeSpan timeout, ILogger logger)
    {
        MethodInfo method = typeof(TestHostControllersTestHost).GetMethod(
            "WaitForExitAfterTerminationAsync",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not find TestHostControllersTestHost.WaitForExitAfterTerminationAsync.");
        return (Task<bool>?)method.Invoke(null, [process, timeout, logger])
            ?? throw new InvalidOperationException("TestHostControllersTestHost.WaitForExitAfterTerminationAsync returned null.");
    }

    private static Task<(int ExitCode, TestHostProcessInformation ProcessInformation, string? ExtensionInformation)> RunTestHostProcessAsync(
        TestHostControllersTestHost host,
        ProcessStartInfo processStartInfo,
        IReadOnlyList<string> partialCommandLine,
        int currentPid,
        IProcessHandler process,
        IConfiguration configuration,
        object testHostControllerIpc,
        TestHostControllerCancellationServer testHostControllerCancellationServer,
        ProxyOutputDevice outputDevice,
        ITelemetryInformation telemetryInformation,
        CancellationToken applicationCancellationToken)
    {
        MethodInfo method = typeof(TestHostControllersTestHost).GetMethod(
            "RunTestHostProcessAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not find TestHostControllersTestHost.RunTestHostProcessAsync.");
        return (Task<(int ExitCode, TestHostProcessInformation ProcessInformation, string? ExtensionInformation)>?)method.Invoke(
            host,
            [
                processStartInfo,
                partialCommandLine,
                currentPid,
                process,
                configuration,
                testHostControllerIpc,
                testHostControllerCancellationServer,
                outputDevice,
                telemetryInformation,
                Stopwatch.StartNew(),
                applicationCancellationToken,
            ])
            ?? throw new InvalidOperationException("TestHostControllersTestHost.RunTestHostProcessAsync returned null.");
    }

    private static IDisposable CreateTestHostControllerIpc(
        TestHostControllersTestHost host,
        CancellationToken cancellationToken)
    {
        MethodInfo method = typeof(TestHostControllersTestHost).GetMethod(
            "CreateTestHostControllerIpc",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not find TestHostControllersTestHost.CreateTestHostControllerIpc.");
        return (IDisposable?)method.Invoke(host, [null, cancellationToken])
            ?? throw new InvalidOperationException("TestHostControllersTestHost.CreateTestHostControllerIpc returned null.");
    }

    private static void MarkOutputDeviceStillRunning(List<object> servicesStillRunning, ProxyOutputDevice outputDevice)
    {
        MethodInfo method = typeof(TestHostControllersTestHost).GetMethod(
            "MarkOutputDeviceStillRunning",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not find TestHostControllersTestHost.MarkOutputDeviceStillRunning.");
        method.Invoke(null, [servicesStillRunning, outputDevice]);
    }

    private static CancellationTokenSource EnsureControllerFinalizationCancellationTokenSource(TestHostControllersTestHost host)
    {
        MethodInfo method = typeof(TestHostControllersTestHost).GetMethod(
            "EnsureControllerFinalizationCancellationTokenSource",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not find TestHostControllersTestHost.EnsureControllerFinalizationCancellationTokenSource.");
        return (CancellationTokenSource?)method.Invoke(host, null)
            ?? throw new InvalidOperationException("TestHostControllersTestHost.EnsureControllerFinalizationCancellationTokenSource returned null.");
    }

    private static void ArmControllerFinalizationTimeout(TestHostControllersTestHost host)
    {
        MethodInfo method = typeof(TestHostControllersTestHost).GetMethod(
            "ArmControllerFinalizationTimeout",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not find TestHostControllersTestHost.ArmControllerFinalizationTimeout.");
        method.Invoke(host, null);
    }

    private static void RegisterControllerFinalizationTransition(TestHostControllersTestHost host, CancellationToken applicationCancellationToken)
    {
        MethodInfo method = typeof(TestHostControllersTestHost).GetMethod(
            "RegisterControllerFinalizationTransition",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not find TestHostControllersTestHost.RegisterControllerFinalizationTransition.");
        method.Invoke(host, [applicationCancellationToken]);
    }

    private static Task<bool> TryRunControllerCleanupAsync(TestHostControllersTestHost host, Func<Task> cleanup)
    {
        MethodInfo method = typeof(TestHostControllersTestHost).GetMethod(
            "TryRunControllerCleanupAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not find TestHostControllersTestHost.TryRunControllerCleanupAsync.");
        return (Task<bool>?)method.Invoke(host, [cleanup])
            ?? throw new InvalidOperationException("TestHostControllersTestHost.TryRunControllerCleanupAsync returned null.");
    }

    [SuppressMessage("Design", "TA0001:Extension should not implement cross-functional areas", Justification = "Done on purpose for testing error")]
    private sealed class InvalidComposition : ITestHostProcessLifetimeHandler, ITestSessionLifetimeHandler
    {
        public InvalidComposition(IServiceProvider serviceProvider)
        {
        }

        public InvalidComposition()
        {
        }

        public string Uid => nameof(InvalidComposition);

        public string Version => PlatformVersion.Version;

        public string DisplayName => nameof(InvalidComposition);

        public string Description => nameof(InvalidComposition);

        public Task BeforeTestHostProcessStartAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<bool> IsEnabledAsync() => throw new NotImplementedException();

        public Task OnTestHostProcessExitedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext) => throw new NotImplementedException();

        public Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext) => throw new NotImplementedException();
    }

    private sealed class TestHostProcessLifetimeHandlerPlusTestHostEnvironmentVariableProvider : ITestHostProcessLifetimeHandler, ITestHostEnvironmentVariableProvider
    {
        public TestHostProcessLifetimeHandlerPlusTestHostEnvironmentVariableProvider()
        {
        }

        public TestHostProcessLifetimeHandlerPlusTestHostEnvironmentVariableProvider(IServiceProvider serviceProvider)
        {
        }

        public string Uid => nameof(TestHostProcessLifetimeHandlerPlusTestHostEnvironmentVariableProvider);

        public string Version => PlatformVersion.Version;

        public string DisplayName => nameof(TestHostProcessLifetimeHandlerPlusTestHostEnvironmentVariableProvider);

        public string Description => nameof(TestHostProcessLifetimeHandlerPlusTestHostEnvironmentVariableProvider);

        public Task<ValidationResult> ValidateTestHostEnvironmentVariablesAsync(IReadOnlyEnvironmentVariables environmentVariables) => throw new NotImplementedException();

        public Task BeforeTestHostProcessStartAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task OnTestHostProcessExitedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task UpdateAsync(IEnvironmentVariables environmentVariables) => throw new NotImplementedException();
    }

    private sealed class TestHostProcessLifetimeHandler : ITestHostProcessLifetimeHandler
    {
        public TestHostProcessLifetimeHandler(string id) => Uid = id;

        public string Uid { get; }

        public string Version => nameof(Consumer);

        public string DisplayName => nameof(Consumer);

        public string Description => nameof(Consumer);

        public Task BeforeTestHostProcessStartAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task OnTestHostProcessExitedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates.
    private sealed class TestHostLauncher : ITestHostLauncher, ITestHostControllerConnectionAuthorizer
    {
        public TestHostLauncher(string id) => Uid = id;

        public string Uid { get; }

        public string Version => nameof(TestHostLauncher);

        public string DisplayName => nameof(TestHostLauncher);

        public string Description => nameof(TestHostLauncher);

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task<ITestHostHandle> LaunchTestHostAsync(TestHostLaunchContext context, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<IReadOnlyList<string>> GetAuthorizedSecurityIdentitiesAsync(string testHostFileName, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<string>>([ContosoPackageSid]);
    }
#pragma warning restore TPEXP

    private sealed class TestHostEnvironmentVariableProvider : ITestHostEnvironmentVariableProvider
    {
        public TestHostEnvironmentVariableProvider(string id) => Uid = id;

        public string Uid { get; }

        public string Version => nameof(Consumer);

        public string DisplayName => nameof(Consumer);

        public string Description => nameof(Consumer);

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task UpdateAsync(IEnvironmentVariables environmentVariables) => throw new NotImplementedException();

        public Task<ValidationResult> ValidateTestHostEnvironmentVariablesAsync(IReadOnlyEnvironmentVariables environmentVariables) => throw new NotImplementedException();
    }

    public sealed class TestSessionLifetimeHandlerPlusConsumer : ITestSessionLifetimeHandler, IDataConsumer
    {
        public TestSessionLifetimeHandlerPlusConsumer()
        {
        }

        public TestSessionLifetimeHandlerPlusConsumer(IServiceProvider serviceProvider)
        {
        }

        public string Uid => nameof(TestSessionLifetimeHandlerPlusConsumer);

        public string Version => PlatformVersion.Version;

        public string DisplayName => nameof(TestSessionLifetimeHandlerPlusConsumer);

        public string Description => nameof(TestSessionLifetimeHandlerPlusConsumer);

        public Type[] DataTypesConsumed => [];

        public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext) => throw new NotImplementedException();

        public Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext) => throw new NotImplementedException();
    }

    private sealed class TestSessionLifetimeHandler : ITestSessionLifetimeHandler
    {
        public TestSessionLifetimeHandler(string id) => Uid = id;

        public string Uid { get; }

        public string Version => nameof(Consumer);

        public string DisplayName => nameof(Consumer);

        public string Description => nameof(Consumer);

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext) => throw new NotImplementedException();

        public Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext) => throw new NotImplementedException();
    }

    private sealed class Consumer : IDataConsumer
    {
        public Consumer(string id) => Uid = id;

        public string Uid { get; }

        public string Version => nameof(Consumer);

        public string DisplayName => nameof(Consumer);

        public string Description => nameof(Consumer);

        public Type[] DataTypesConsumed => throw new NotImplementedException();

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private sealed class ApplicationLifecycleCallbacks : ITestHostApplicationLifetime
    {
        public ApplicationLifecycleCallbacks(string id) => Uid = id;

        public string Uid { get; }

        public string Version => nameof(ApplicationLifecycleCallbacks);

        public string DisplayName => nameof(ApplicationLifecycleCallbacks);

        public string Description => nameof(ApplicationLifecycleCallbacks);

        public Task AfterRunAsync(int exitCode, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task BeforeRunAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SingletonExtensions_EnforceRegistrationAndInitializeOnlyEnabledInstance(bool enabled)
    {
        TestHostManager manager = new();
        ArgumentNullException invokerNull = Assert.ThrowsExactly<ArgumentNullException>(() => manager.AddTestFrameworkInvoker(null!));
        ArgumentNullException filterNull = Assert.ThrowsExactly<ArgumentNullException>(() => manager.AddTestExecutionFilterFactory(null!));
        Assert.AreEqual("testFrameworkInvokerFactory", invokerNull.ParamName);
        Assert.AreEqual("testExecutionFilterFactory", filterNull.ParamName);
        Mock<Microsoft.Testing.Platform.Requests.ITestFrameworkInvoker> invoker = new();
        invoker.Setup(i => i.IsEnabledAsync()).ReturnsAsync(enabled);
        Mock<IAsyncInitializableExtension> invokerInitialization = invoker.As<IAsyncInitializableExtension>();
        invokerInitialization.Setup(i => i.InitializeAsync()).Returns(Task.CompletedTask);
        Mock<Microsoft.Testing.Platform.Requests.ITestExecutionFilterFactory> filter = new();
        filter.Setup(f => f.IsEnabledAsync()).ReturnsAsync(enabled);
        Mock<IAsyncInitializableExtension> filterInitialization = filter.As<IAsyncInitializableExtension>();
        filterInitialization.Setup(i => i.InitializeAsync()).Returns(Task.CompletedTask);
        int factoryCalls = 0;
        manager.AddTestFrameworkInvoker(_ =>
        {
            factoryCalls++;
            return invoker.Object;
        });
        manager.AddTestExecutionFilterFactory(_ =>
        {
            factoryCalls++;
            return filter.Object;
        });
        Assert.ThrowsExactly<InvalidOperationException>(() => manager.AddTestFrameworkInvoker(_ => invoker.Object));
        Assert.ThrowsExactly<InvalidOperationException>(() => manager.AddTestExecutionFilterFactory(_ => filter.Object));
        Assert.AreEqual(0, factoryCalls);

        ActionResult<Microsoft.Testing.Platform.Requests.ITestFrameworkInvoker> builtInvoker = await manager.TryBuildTestAdapterInvokerAsync(_serviceProvider);
        ActionResult<Microsoft.Testing.Platform.Requests.ITestExecutionFilterFactory> builtFilter = await manager.TryBuildTestExecutionFilterFactoryAsync(_serviceProvider);

        Assert.AreEqual(2, factoryCalls);
        Assert.AreEqual(enabled, builtInvoker.IsSuccess);
        Assert.AreEqual(enabled, builtFilter.IsSuccess);
        Assert.AreSame(enabled ? invoker.Object : null, builtInvoker.Result);
        Assert.AreSame(enabled ? filter.Object : null, builtFilter.Result);
        invokerInitialization.Verify(i => i.InitializeAsync(), enabled ? Times.Once() : Times.Never());
        filterInitialization.Verify(i => i.InitializeAsync(), enabled ? Times.Once() : Times.Never());
    }

    [TestMethod]
    public void ActionResult_FactoryAndImplicitSuccessPreserveIdentityAndFailureHasNoResult()
    {
        object item = new();
        var explicitSuccess = ActionResult.Ok(item);
        ActionResult<object> implicitSuccess = item;
        var failure = ActionResult.Fail<object>();

        Assert.IsTrue(explicitSuccess.IsSuccess);
        Assert.AreSame(item, explicitSuccess.Result);
        Assert.IsTrue(implicitSuccess.IsSuccess);
        Assert.AreSame(item, implicitSuccess.Result);
        Assert.IsFalse(failure.IsSuccess);
        Assert.IsNull(failure.Result);
    }

    [TestMethod]
    public async Task BuilderHelpers_RegisterDistinctTreeAndFailureProvidersLazily()
    {
        Func<Microsoft.Testing.Platform.Extensions.CommandLine.ICommandLineOptionsProvider>? tree = null;
        Func<IServiceProvider, Microsoft.Testing.Platform.Extensions.CommandLine.ICommandLineOptionsProvider>? failure = null;
        Mock<ICommandLineManager> manager = new();
        manager.Setup(m => m.AddProvider(It.IsAny<Func<Microsoft.Testing.Platform.Extensions.CommandLine.ICommandLineOptionsProvider>>()))
            .Callback<Func<Microsoft.Testing.Platform.Extensions.CommandLine.ICommandLineOptionsProvider>>(factory => tree = factory);
        manager.Setup(m => m.AddProvider(It.IsAny<Func<IServiceProvider, Microsoft.Testing.Platform.Extensions.CommandLine.ICommandLineOptionsProvider>>()))
            .Callback<Func<IServiceProvider, Microsoft.Testing.Platform.Extensions.CommandLine.ICommandLineOptionsProvider>>(factory => failure = factory);
        Mock<ITestApplicationBuilder> builder = new();
        builder.SetupGet(b => b.CommandLine).Returns(manager.Object);
        int identityReads = 0;
        Mock<IExtension> owner = new();
        owner.SetupGet(o => o.Uid).Returns(() =>
        {
            identityReads++;
            return "owner";
        });
        owner.SetupGet(o => o.Version).Returns("1.2.3");
        owner.SetupGet(o => o.DisplayName).Returns("Owner display");
        owner.SetupGet(o => o.Description).Returns("Owner description");

        builder.Object.AddTreeNodeFilterService(owner.Object);
        builder.Object.AddMaximumFailedTestsService(owner.Object);
        Assert.AreEqual(0, identityReads);
        Assert.IsNotNull(tree);
        Assert.IsNotNull(failure);
        ServiceProvider services = new();
        services.AddService(new TestFrameworkCapabilities(new Mock<IGracefulStopTestExecutionCapability>().Object));
        Microsoft.Testing.Platform.Extensions.CommandLine.ICommandLineOptionsProvider treeProvider = tree();
        Microsoft.Testing.Platform.Extensions.CommandLine.ICommandLineOptionsProvider failureProvider = failure(services);

        Assert.AreEqual(2, identityReads);
        Assert.AreEqual("owner", treeProvider.Uid);
        Assert.AreEqual("Owner display", failureProvider.DisplayName);
        Microsoft.Testing.Platform.Extensions.CommandLine.CommandLineOption treeOption = Assert.ContainsSingle(treeProvider.GetCommandLineOptions());
        Microsoft.Testing.Platform.Extensions.CommandLine.CommandLineOption failureOption = Assert.ContainsSingle(failureProvider.GetCommandLineOptions());
        Assert.AreEqual("treenode-filter", treeOption.Name);
        Assert.AreEqual("maximum-failed-tests", failureOption.Name);
        Assert.AreEqual(Microsoft.Testing.Platform.Extensions.CommandLine.ArgumentArity.ExactlyOne, treeOption.Arity);
        Assert.AreEqual(Microsoft.Testing.Platform.Extensions.CommandLine.ArgumentArity.ExactlyOne, failureOption.Arity);
        Assert.IsTrue((await failureProvider.ValidateOptionArgumentsAsync(failureOption, ["3"])).IsValid);
        ValidationResult rejected = await failureProvider.ValidateOptionArgumentsAsync(failureOption, ["0"]);
        Assert.IsFalse(rejected.IsValid);
        Assert.Contains("0", rejected.ErrorMessage!);
    }

    [TestMethod]
    public async Task BuilderHelper_FilterProviderRegistrationIsLazyAndBuildsOriginalEnabledOwner()
    {
        Microsoft.Testing.Platform.TestHost.TestHostManager manager = new();
        Mock<ITestApplicationBuilder> builder = new();
        builder.SetupGet(b => b.TestHost).Returns(manager);
        Mock<ITestExecutionFilterProvider> provider = new();
        provider.SetupGet(p => p.Uid).Returns("registered-filter-owner");
        provider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(true);
        ServiceProvider services = new();
        int created = 0;
        builder.Object.AddTestExecutionFilterProvider(serviceProvider =>
        {
            Assert.AreSame(services, serviceProvider);
            created++;
            return provider.Object;
        });
        Assert.AreEqual(0, created);
        Assert.AreEqual(
            "providerFactory",
            Assert.ThrowsExactly<ArgumentNullException>(() => builder.Object.AddTestExecutionFilterProvider(null!)).ParamName);

        ITestExecutionFilterProvider[] built = await manager.BuildTestExecutionFilterProvidersAsync(services);

        Assert.AreSame(provider.Object, Assert.ContainsSingle(built));
        Assert.AreEqual(1, created);
        provider.Verify(p => p.IsEnabledAsync(), Times.Once);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ControllerConnection_PeerExitCallbackDoesNotBecomeApplicationCancellation(bool withCallback)
    {
        using CancellationTokenSource peerExit = new();
        peerExit.Cancel();
        int exited = 0;
        int timedOut = 0;
        CancellationToken observed = default;
        Func<Task>? onExit = null;
        if (withCallback)
        {
            onExit = () =>
            {
                exited++;
                return Task.CompletedTask;
            };
        }

        bool connected = await TestHostControllersTestHost.WaitForTestHostControllerConnectionOrProcessExitAsync(
            token =>
            {
                observed = token;
                return Task.FromCanceled(token);
            },
            600,
            CancellationToken.None,
            () =>
            {
                timedOut++;
                return Task.CompletedTask;
            },
            peerExit.Token,
            onExit);

        Assert.IsFalse(connected);
        Assert.IsTrue(observed.IsCancellationRequested);
        Assert.AreEqual(withCallback ? 1 : 0, exited);
        Assert.AreEqual(0, timedOut);
    }

    [TestMethod]
    public async Task ControllerConnection_ApplicationCancellationIsNotSwallowedAsPeerExit()
    {
        using CancellationTokenSource application = new();
        using CancellationTokenSource peerExit = new();
        application.Cancel();
        peerExit.Cancel();
        CancellationToken observed = default;
        int sideEffects = 0;

        OperationCanceledException failure = await Assert.ThrowsExactlyAsync<TaskCanceledException>(
            () => TestHostControllersTestHost.WaitForTestHostControllerConnectionOrProcessExitAsync(
                token =>
                {
                    observed = token;
                    return Task.FromCanceled(token);
                },
                600,
                application.Token,
                () =>
                {
                    sideEffects++;
                    return Task.CompletedTask;
                },
                peerExit.Token,
                () =>
                {
                    sideEffects++;
                    return Task.CompletedTask;
                }));

        Assert.AreEqual(observed, failure.CancellationToken);
        Assert.AreEqual(0, sideEffects);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ControllerTermination_CooperativeAndPostKillCompletionAvoidFalseWarnings(bool needsKill)
    {
        Mock<IProcess> process = new();
        if (needsKill)
        {
            process.SetupSequence(p => p.WaitForExitAsync(CancellationToken.None))
                .ThrowsAsync(new IOException("first wait sentinel"))
                .Returns(Task.CompletedTask);
            process.Setup(p => p.Kill()).Throws(new NotSupportedException("kill sentinel"));
        }
        else
        {
            process.Setup(p => p.WaitForExitAsync(CancellationToken.None)).Returns(Task.CompletedTask);
        }

        Mock<ILogger> logger = new();
        List<(LogLevel Level, string Text)> logs = [];
        logger.Setup(l => l.LogAsync(It.IsAny<LogLevel>(), It.IsAny<string>(), null, LoggingExtensions.Formatter))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>((level, text, _, _) => logs.Add((level, text)))
            .Returns(Task.CompletedTask);
        int canceled = 0;

        await TestHostControllersTestHost.HandleCanceledTestHostAsync(
            process.Object, () => canceled++, logger.Object, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        Assert.AreEqual(1, canceled);
        process.Verify(p => p.Kill(), needsKill ? Times.Once() : Times.Never());
        process.Verify(p => p.WaitForExitAsync(CancellationToken.None), Times.Exactly(needsKill ? 2 : 1));
        Assert.IsEmpty(logs.Where(l => l.Level == LogLevel.Warning));
        if (needsKill)
        {
            Assert.HasCount(3, logs);
            Assert.StartsWith("Waiting for the test host to exit during cancellation teardown failed; continuing cleanup.", logs[0].Text);
            Assert.Contains("first wait sentinel", logs[0].Text);
            Assert.AreEqual("Test host did not exit within '00:00:30' after cooperative cancellation; terminating it.", logs[1].Text);
            Assert.StartsWith("Test host termination during cancellation failed; continuing cleanup.", logs[2].Text);
            Assert.Contains("kill sentinel", logs[2].Text);
        }
        else
        {
            Assert.IsEmpty(logs);
        }
    }

    [TestMethod]
    public async Task ControllerConnectionTermination_ExitedAndThrowingKillPartitionsAreSafe()
    {
        Mock<IProcess> process = new();
        process.SetupGet(p => p.HasExited).Returns(false);
        process.Setup(p => p.Kill()).Throws(new NotSupportedException("connection kill sentinel"));
        process.Setup(p => p.WaitForExitAsync(CancellationToken.None)).Returns(Task.CompletedTask);
        List<string> logs = [];
        Mock<ILogger> logger = new();
        logger.Setup(l => l.LogAsync(LogLevel.Debug, It.IsAny<string>(), null, LoggingExtensions.Formatter))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>((_, text, _, _) => logs.Add(text))
            .Returns(Task.CompletedTask);

        await TestHostControllersTestHost.TerminateTestHostAfterConnectionFailureAsync(process.Object, logger.Object, TimeSpan.FromSeconds(30));

        string diagnostic = Assert.ContainsSingle(logs);
        Assert.StartsWith("Test host termination after a connection failure failed; continuing cleanup.", diagnostic);
        Assert.Contains("connection kill sentinel", diagnostic);
        process.Verify(p => p.Kill(), Times.Once);
        process.Verify(p => p.WaitForExitAsync(CancellationToken.None), Times.Once);
        process.SetupGet(p => p.HasExited).Returns(true);
        process.Invocations.Clear();

        await TestHostControllersTestHost.TerminateTestHostAfterConnectionFailureAsync(process.Object, logger.Object, TimeSpan.FromSeconds(30));

        process.Verify(p => p.Kill(), Times.Never);
        process.Verify(p => p.WaitForExitAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.HasCount(1, logs);
    }
}
