// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.ServerMode.IntegrationTests.Messages.V100;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MSTest.Acceptance.IntegrationTests.Messages.V100;

namespace Playground;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Opt-out telemetry
        Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");

        if (Environment.GetEnvironmentVariable("TESTSERVERMODE") != "1")
        {
            // To attach to the children
            Microsoft.Testing.TestInfrastructure.DebuggerUtility.AttachCurrentProcessToParentVSProcess();
            ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);
            testApplicationBuilder.AddMSTest(() => [Assembly.GetEntryAssembly()!]);
            testApplicationBuilder.TestHostControllers.AddProcessLifetimeHandler(_ => new OutOfProc());
            // Enable Trx
            // testApplicationBuilder.AddTrxReportProvider();

            // Enable Telemetry
            // testApplicationBuilder.AddAppInsightsTelemetryProvider();
            using ITestApplication testApplication = await testApplicationBuilder.BuildAsync();
            return await testApplication.RunAsync();
        }
        else
        {
            Environment.SetEnvironmentVariable("TESTSERVERMODE", "0");
            using TestingPlatformClient client = await TestingPlatformClientFactory.StartAsServerAndConnectAsync(Environment.ProcessPath!, enableDiagnostic: true);

            await client.InitializeAsync();
            List<TestNodeUpdate> testNodeUpdates = new();
            ResponseListener discoveryResponse = await client.DiscoverTestsAsync(Guid.NewGuid(), node =>
            {
                testNodeUpdates.AddRange(node);
                return Task.CompletedTask;
            });
            await discoveryResponse.WaitCompletionAsync();

            ResponseListener runRequest = await client.RunTestsAsync(Guid.NewGuid(), testNodeUpdates.Select(x => x.Node).ToArray(), node => Task.CompletedTask);
            await runRequest.WaitCompletionAsync();

            await client.ExitAsync();

            return 0;
        }
    }
}

public class OutOfProc : ITestHostProcessLifetimeHandler
{
    public string Uid => nameof(OutOfProc);

    public string Version => "1.0.0";

    public string DisplayName => nameof(OutOfProc);

    public string Description => nameof(OutOfProc);

    public Task BeforeTestHostProcessStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task OnTestHostProcessExitedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellation) => Task.CompletedTask;

    public Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellation) => Task.CompletedTask;
}
