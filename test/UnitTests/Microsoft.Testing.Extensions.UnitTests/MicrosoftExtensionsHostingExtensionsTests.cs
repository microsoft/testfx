// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class MicrosoftExtensionsHostingExtensionsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunTestingPlatformAsync_NullHostThrows()
    {
#pragma warning disable MSTEST0049 // Cancellation is passed to the operation under test; this target's Assert overload has no token.
        ArgumentNullException exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => ((IHost)null!).RunTestingPlatformAsync([], static _ => { }, TestContext.CancellationToken));
#pragma warning restore MSTEST0049

        Assert.AreEqual("host", exception.ParamName);
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

    private sealed class EmptyTestFramework : ITestFramework
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

        public Task ExecuteRequestAsync(ExecuteRequestContext context)
        {
            context.Complete();
            return Task.CompletedTask;
        }
    }
}
