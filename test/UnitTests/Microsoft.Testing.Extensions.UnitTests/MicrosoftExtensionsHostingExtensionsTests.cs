// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
[ResourceLock(WellKnownResources.EnvironmentVariables)]
public sealed class MicrosoftExtensionsHostingExtensionsTests
{
    private static readonly string[] EnvironmentVariablesToClear =
    [
        // Affected-test selection sets this on the outer test process, but these tests verify
        // the exit code produced by their own in-process test application.
        "TESTINGPLATFORM_EXITCODE_IGNORE",
        "TESTINGPLATFORM_DIAGNOSTIC",
        "TESTINGPLATFORM_DIAGNOSTIC_VERBOSITY",
        "TESTINGPLATFORM_DIAGNOSTIC_OUTPUT_DIRECTORY",
        "TESTINGPLATFORM_DIAGNOSTIC_FILE_PREFIX",
        "TESTINGPLATFORM_DIAGNOSTIC_OUTPUT_FILEPREFIX",
        "TESTINGPLATFORM_DIAGNOSTIC_SYNCHRONOUS_WRITE",
        "TESTINGPLATFORM_DIAGNOSTIC_FILELOGGER_SYNCHRONOUSWRITE",
    ];

    private Dictionary<string, string?> _originalEnvironmentVariables = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _originalEnvironmentVariables = EnvironmentVariablesToClear.ToDictionary(
            static name => name,
            Environment.GetEnvironmentVariable);

        foreach (string name in EnvironmentVariablesToClear)
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [TestCleanup]
    public void TestCleanup()
    {
        foreach (KeyValuePair<string, string?> variable in _originalEnvironmentVariables)
        {
            Environment.SetEnvironmentVariable(variable.Key, variable.Value);
        }
    }

    [TestMethod]
    public async Task RunTestingPlatformAsync_NullHostThrows()
    {
#pragma warning disable MSTEST0049 // Cancellation is passed to the operation under test; this target's Assert overload has no token.
        ArgumentNullException exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => ((IHost)null!).RunTestingPlatformAsync([], static _ => { }, TestContext.CancellationToken));
#pragma warning restore MSTEST0049

        Assert.AreEqual("host", exception.ParamName);
    }

    [DataRow("--help", true)]
    [DataRow("-?", true)]
    [DataRow("--info", true)]
    [DataRow("--list-tests", false)]
    [TestMethod]
    public void ShouldBypassApplicationHost_RecognizesInformationalOptions(string option, bool expected)
        => Assert.AreEqual(expected, MicrosoftExtensionsHostingExtensions.ShouldBypassApplicationHost([option]));

    [TestMethod]
    public void ShouldBypassApplicationHost_ExpandsResponseFiles()
    {
        string responseFile = Path.Combine(Path.GetTempPath(), $"{nameof(ShouldBypassApplicationHost_ExpandsResponseFiles)}-{Guid.NewGuid():N}.rsp");
        try
        {
            File.WriteAllText(responseFile, "--help");

            Assert.IsTrue(MicrosoftExtensionsHostingExtensions.ShouldBypassApplicationHost([$"@{responseFile}"]));
        }
        finally
        {
            File.Delete(responseFile);
        }
    }

    [TestMethod]
    public async Task RunTestingPlatformAsync_NullArgsThrows()
    {
        using IHost host = Host.CreateApplicationBuilder().Build();

#pragma warning disable MSTEST0049 // Cancellation is passed to the operation under test; this target's Assert overload has no token.
        ArgumentNullException exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => host.RunTestingPlatformAsync(null!, static _ => { }, TestContext.CancellationToken));
#pragma warning restore MSTEST0049

        Assert.AreEqual("args", exception.ParamName);
    }

    [TestMethod]
    public async Task RunTestingPlatformAsync_NullConfigureThrows()
    {
        using IHost host = Host.CreateApplicationBuilder().Build();

#pragma warning disable MSTEST0049 // Cancellation is passed to the operation under test; this target's Assert overload has no token.
        ArgumentNullException exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => host.RunTestingPlatformAsync([], null!, TestContext.CancellationToken));
#pragma warning restore MSTEST0049

        Assert.AreEqual("configure", exception.ParamName);
    }

    [TestMethod]
    public async Task RunTestingPlatformAsync_WithoutHostApplicationLifetime_PreservesPreviousBehavior()
    {
        using IHost host = new HostWithoutApplicationLifetime(Host.CreateApplicationBuilder().Build());

        int exitCode = await host.RunTestingPlatformAsync(
            [],
            testApplication => testApplication.RegisterTestFramework(
                _ => new TestFrameworkCapabilities(),
                (_, _) => new EmptyTestFramework()),
            TestContext.CancellationToken);

        Assert.AreEqual(8, exitCode);
    }

    [TestMethod]
    public async Task RunTestingPlatformAsync_StartsHostBeforeBuildingMtpAndStopsAfterRun()
    {
        HostApplicationBuilder hostBuilder = Host.CreateApplicationBuilder();
        hostBuilder.Configuration["bridge:value"] = "from-host";
        var lifecycle = new RecordingHostedService();
        hostBuilder.Services.AddSingleton<IHostedService>(lifecycle);
        using IHost host = hostBuilder.Build();
        string? observedConfiguration = null;
        bool hostWasStartedWhenFrameworkWasCreated = false;

        int exitCode = await host.RunTestingPlatformAsync(
            [],
            testApplication =>
                testApplication.RegisterTestFramework(
                    _ => new TestFrameworkCapabilities(),
                    (_, serviceProvider) =>
                    {
                        hostWasStartedWhenFrameworkWasCreated = lifecycle.Started;
                        observedConfiguration = serviceProvider.GetConfiguration()["bridge:value"];
                        return new EmptyTestFramework();
                    }),
            TestContext.CancellationToken);

        Assert.AreEqual(8, exitCode);
        Assert.IsTrue(hostWasStartedWhenFrameworkWasCreated);
        Assert.AreEqual("from-host", observedConfiguration);
        Assert.IsTrue(lifecycle.Started);
        Assert.IsTrue(lifecycle.Stopped);
    }

    [TestMethod]
    public async Task RunTestingPlatformAsync_WhenMtpFails_StillStopsHost()
    {
        HostApplicationBuilder hostBuilder = Host.CreateApplicationBuilder();
        var lifecycle = new RecordingHostedService();
        hostBuilder.Services.AddSingleton<IHostedService>(lifecycle);
        using IHost host = hostBuilder.Build();
        var expectedException = new InvalidOperationException("framework failure");
        InvalidOperationException? actualException = null;

        try
        {
            await host.RunTestingPlatformAsync(
                [],
                testApplication =>
                    testApplication.RegisterTestFramework(
                        _ => new TestFrameworkCapabilities(),
                        (_, _) => throw expectedException),
                TestContext.CancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            actualException = exception;
        }

        Assert.AreSame(expectedException, actualException);
        Assert.IsTrue(lifecycle.Started);
        Assert.IsTrue(lifecycle.Stopped);
    }

    [TestMethod]
    public async Task RunTestingPlatformAsync_WhenMtpAndHostStopFail_PreservesMtpFailure()
    {
        HostApplicationBuilder hostBuilder = Host.CreateApplicationBuilder();
        var stopException = new InvalidOperationException("host stop failure");
        hostBuilder.Services.AddSingleton<IHostedService>(new ThrowingStopHostedService(stopException));
        using IHost host = hostBuilder.Build();
        var expectedException = new InvalidOperationException("framework failure");
        InvalidOperationException? actualException = null;

        try
        {
            await host.RunTestingPlatformAsync(
                [],
                testApplication =>
                    testApplication.RegisterTestFramework(
                        _ => new TestFrameworkCapabilities(),
                        (_, _) => throw expectedException),
                TestContext.CancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            actualException = exception;
        }

        Assert.IsNotNull(actualException);
        Assert.AreSame(expectedException, actualException);
        Assert.AreSame(stopException, actualException.Data[nameof(IHost.StopAsync)]);
    }

    [TestMethod]
    public async Task RunTestingPlatformAsync_WhenMtpAndHostStopFailAndExceptionDataIsUnavailable_AggregatesFailures()
    {
        HostApplicationBuilder hostBuilder = Host.CreateApplicationBuilder();
        var stopException = new InvalidOperationException("host stop failure");
        hostBuilder.Services.AddSingleton<IHostedService>(new ThrowingStopHostedService(stopException));
        using IHost host = hostBuilder.Build();
        var expectedException = new ThrowingDataException("framework failure");

        AggregateException exception = await Assert.ThrowsExactlyAsync<AggregateException>(
            () => host.RunTestingPlatformAsync(
                [],
                testApplication =>
                    testApplication.RegisterTestFramework(
                        _ => new TestFrameworkCapabilities(),
                        (_, _) => throw expectedException),
                TestContext.CancellationToken));

        Assert.HasCount(2, exception.InnerExceptions);
        Assert.AreSame(expectedException, exception.InnerExceptions[0]);
        Assert.AreSame(stopException, exception.InnerExceptions[1]);
    }

    [TestMethod]
    public async Task RunTestingPlatformAsync_WhenConfigureFails_ReleasesDiagnosticLog()
    {
        using IHost host = Host.CreateApplicationBuilder().Build();
        string diagnosticDirectory = CreateDiagnosticDirectory();
        var expectedException = new InvalidOperationException("configuration failure");
        InvalidOperationException? actualException = null;

        try
        {
            await host.RunTestingPlatformAsync(
                CreateDiagnosticArguments(diagnosticDirectory),
                _ => throw expectedException,
                TestContext.CancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            actualException = exception;
        }

        Assert.AreSame(expectedException, actualException);
        AssertDiagnosticLogIsReleased(diagnosticDirectory);
    }

    [TestMethod]
    public async Task RunTestingPlatformAsync_WhenHostStartFails_ReleasesDiagnosticLog()
    {
        HostApplicationBuilder hostBuilder = Host.CreateApplicationBuilder();
        var lifecycle = new RecordingHostedService();
        var expectedException = new InvalidOperationException("host start failure");
        hostBuilder.Services.AddSingleton<IHostedService>(lifecycle);
        hostBuilder.Services.AddSingleton<IHostedService>(new ThrowingHostedService(expectedException));
        using IHost host = hostBuilder.Build();
        string diagnosticDirectory = CreateDiagnosticDirectory();
        InvalidOperationException? actualException = null;

        try
        {
            await host.RunTestingPlatformAsync(
                CreateDiagnosticArguments(diagnosticDirectory),
                testApplication => testApplication.RegisterTestFramework(
                    _ => new TestFrameworkCapabilities(),
                    (_, _) => new EmptyTestFramework()),
                TestContext.CancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            actualException = exception;
        }

        Assert.AreSame(expectedException, actualException);
        Assert.IsTrue(lifecycle.Started);
        Assert.IsTrue(lifecycle.Stopped);
        AssertDiagnosticLogIsReleased(diagnosticDirectory);
    }

    [TestMethod]
    public async Task RunTestingPlatformAsync_WhenCancellationTokenIsCancelled_CancelsActiveMtpRun()
    {
        using IHost host = Host.CreateApplicationBuilder().Build();
        using var cancellationTokenSource = new CancellationTokenSource();
        var testFramework = new BlockingTestFramework();

        Task<int> runTask = host.RunTestingPlatformAsync(
            [],
            testApplication => testApplication.RegisterTestFramework(
                _ => new TestFrameworkCapabilities(),
                (_, _) => testFramework),
            cancellationTokenSource.Token);

        await testFramework.Started;
        cancellationTokenSource.Cancel();

        int exitCode = await runTask;

        Assert.AreEqual(3, exitCode);
        Assert.IsTrue(testFramework.CancellationObserved);
    }

    [TestMethod]
    public async Task RunTestingPlatformAsync_WhenHostStops_CancelsActiveMtpRun()
    {
        using IHost host = Host.CreateApplicationBuilder().Build();
        IHostApplicationLifetime hostApplicationLifetime = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<IHostApplicationLifetime>(host.Services);
        var testFramework = new BlockingTestFramework();

        Task<int> runTask = host.RunTestingPlatformAsync(
            [],
            testApplication => testApplication.RegisterTestFramework(
                _ => new TestFrameworkCapabilities(),
                (_, _) => testFramework),
            TestContext.CancellationToken);

        await testFramework.Started;
        hostApplicationLifetime.StopApplication();

        int exitCode = await runTask;

        Assert.AreEqual(3, exitCode);
        Assert.IsTrue(testFramework.CancellationObserved);
    }

    [TestMethod]
    public async Task RunTestingPlatformAsync_AfterRunCompletes_ReleasesCancellationRegistration()
    {
        using IHost host = new HostWithoutApplicationLifetime(Host.CreateApplicationBuilder().Build());
        using var cancellationTokenSource = new CancellationTokenSource();
        var testFramework = new CancellationObservingTestFramework();

        int exitCode = await host.RunTestingPlatformAsync(
            [],
            testApplication => testApplication.RegisterTestFramework(
                _ => new TestFrameworkCapabilities(),
                (_, _) => testFramework),
            cancellationTokenSource.Token);

        cancellationTokenSource.Cancel();

        Assert.AreEqual(8, exitCode);
        Assert.IsFalse(testFramework.CancellationObserved);
        testFramework.DisposeRegistration();
    }

    [TestMethod]
    public async Task RunTestingPlatformAsync_WhenMtpStops_DoesNotRequestHostStopBeforeCleanup()
    {
        using var host = new StopObservationHost(Host.CreateApplicationBuilder().Build());

        int exitCode = await host.RunTestingPlatformAsync(
            [],
            testApplication => testApplication.RegisterTestFramework(
                _ => new TestFrameworkCapabilities(),
                (_, serviceProvider) => new CancellingTestFramework(
                    (ITestApplicationCancellationTokenSource)serviceProvider.GetService(typeof(ITestApplicationCancellationTokenSource))!)),
            TestContext.CancellationToken);

        Assert.AreEqual(3, exitCode);
        Assert.IsFalse(host.ApplicationStoppingWasRequestedBeforeStopAsync);
    }

    private static string CreateDiagnosticDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), nameof(MicrosoftExtensionsHostingExtensionsTests), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string[] CreateDiagnosticArguments(string diagnosticDirectory)
        =>
        [
            "--diagnostic",
            "--diagnostic-synchronous-write",
            "--diagnostic-output-directory",
            diagnosticDirectory,
            "--diagnostic-file-prefix",
            "hosting-failure",
        ];

    private static void AssertDiagnosticLogIsReleased(string diagnosticDirectory)
    {
        try
        {
            string[] diagnosticFiles = Directory.GetFiles(diagnosticDirectory, "hosting-failure*.diag");
            Assert.HasCount(1, diagnosticFiles);

            using (File.Open(diagnosticFiles[0], FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
            }
        }
        finally
        {
            Directory.Delete(diagnosticDirectory, recursive: true);
        }
    }

    private sealed class RecordingHostedService : IHostedService
    {
        public bool Started { get; private set; }

        public bool Stopped { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Started = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Stopped = true;
            return Task.CompletedTask;
        }
    }

    private sealed class HostWithoutApplicationLifetime(IHost innerHost) : IHost
    {
        public IServiceProvider Services { get; } = new ServiceProviderWithoutApplicationLifetime(innerHost.Services);

        public void Dispose() => innerHost.Dispose();

        public Task StartAsync(CancellationToken cancellationToken = default)
            => innerHost.StartAsync(cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken = default)
            => innerHost.StopAsync(cancellationToken);
    }

    private sealed class ServiceProviderWithoutApplicationLifetime(IServiceProvider innerServiceProvider) : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(IHostApplicationLifetime) ? null : innerServiceProvider.GetService(serviceType);
    }

    private sealed class StopObservationHost(IHost innerHost) : IHost
    {
        private readonly IHostApplicationLifetime _hostApplicationLifetime =
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetRequiredService<IHostApplicationLifetime>(innerHost.Services);

        public IServiceProvider Services => innerHost.Services;

        public bool ApplicationStoppingWasRequestedBeforeStopAsync { get; private set; }

        public void Dispose() => innerHost.Dispose();

        public Task StartAsync(CancellationToken cancellationToken = default)
            => innerHost.StartAsync(cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            ApplicationStoppingWasRequestedBeforeStopAsync =
                _hostApplicationLifetime.ApplicationStopping.IsCancellationRequested;
            return innerHost.StopAsync(cancellationToken);
        }
    }

    private sealed class CancellingTestFramework(ITestApplicationCancellationTokenSource cancellationTokenSource) : EmptyTestFramework
    {
        public override Task ExecuteRequestAsync(ExecuteRequestContext context)
        {
            cancellationTokenSource.Cancel();
            context.Complete();
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingTestFramework : EmptyTestFramework
    {
        private readonly TaskCompletionSource<object?> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;

        public bool CancellationObserved { get; private set; }

        public override async Task ExecuteRequestAsync(ExecuteRequestContext context)
        {
            _started.SetResult(null);
            try
            {
                await Task.Delay(Timeout.Infinite, context.CancellationToken);
            }
            catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
            {
                CancellationObserved = true;
            }
            finally
            {
                context.Complete();
            }
        }
    }

    private sealed class CancellationObservingTestFramework : EmptyTestFramework
    {
        private CancellationTokenRegistration _cancellationRegistration;

        public bool CancellationObserved { get; private set; }

        public override Task ExecuteRequestAsync(ExecuteRequestContext context)
        {
            _cancellationRegistration = context.CancellationToken.Register(() => CancellationObserved = true);
            context.Complete();
            return Task.CompletedTask;
        }

        public void DisposeRegistration() => _cancellationRegistration.Dispose();
    }

    private sealed class ThrowingHostedService(InvalidOperationException exception) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => throw exception;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ThrowingStopHostedService(InvalidOperationException exception) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => throw exception;
    }

    private sealed class ThrowingDataException(string message) : Exception(message)
    {
        public override IDictionary Data => throw new NotSupportedException("Exception data is unavailable.");
    }

    private class EmptyTestFramework : ITestFramework
    {
        public string Uid => nameof(EmptyTestFramework);

        public string Version => "1.0.0";

        public string DisplayName => nameof(EmptyTestFramework);

        public string Description => nameof(EmptyTestFramework);

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
            => Task.FromResult(new CreateTestSessionResult { IsSuccess = true });

        public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
            => Task.FromResult(new CloseTestSessionResult { IsSuccess = true });

        public virtual Task ExecuteRequestAsync(ExecuteRequestContext context)
        {
            context.Complete();
            return Task.CompletedTask;
        }
    }
}
