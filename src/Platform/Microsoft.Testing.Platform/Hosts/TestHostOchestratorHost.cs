// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed class TestHostOrchestratorHost(TestHostOrchestratorConfiguration testHostOrchestratorConfiguration, ServiceProvider serviceProvider) : ITestHost
{
    private readonly TestHostOrchestratorConfiguration _testHostOrchestratorConfiguration = testHostOrchestratorConfiguration;
    private readonly ServiceProvider _serviceProvider = serviceProvider;

    public async Task<int> RunAsync()
    {
        ILogger logger = _serviceProvider.GetLoggerFactory().CreateLogger<TestHostOrchestratorHost>();
        if (_testHostOrchestratorConfiguration.TestHostOrchestrators.Length > 1)
        {
            throw new NotSupportedException("Multiple test orchestrator not supported");
        }

        ITestHostOrchestrator testHostOrchestrator = _testHostOrchestratorConfiguration.TestHostOrchestrators[0];
        ITestApplicationCancellationTokenSource applicationCancellationToken = _serviceProvider.GetTestApplicationCancellationTokenSource();
        int exitCode;
        await logger.LogInformationAsync($"Running test orchestrator '{testHostOrchestrator.Uid}'").ConfigureAwait(false);
        try
        {
            foreach (ITestHostOrchestratorApplicationLifetime orchestratorLifetime in _serviceProvider.GetServicesInternal<ITestHostOrchestratorApplicationLifetime>())
            {
                await orchestratorLifetime.BeforeRunAsync(applicationCancellationToken.CancellationToken).ConfigureAwait(false);
            }

            exitCode = await testHostOrchestrator.OrchestrateTestHostExecutionAsync(applicationCancellationToken.CancellationToken).ConfigureAwait(false);

            foreach (ITestHostOrchestratorApplicationLifetime orchestratorLifetime in _serviceProvider.GetServicesInternal<ITestHostOrchestratorApplicationLifetime>())
            {
                await orchestratorLifetime.AfterRunAsync(exitCode, applicationCancellationToken.CancellationToken).ConfigureAwait(false);
                await DisposeHelper.DisposeAsync(orchestratorLifetime).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (applicationCancellationToken.CancellationToken.IsCancellationRequested)
        {
            // We do nothing we're canceling
            exitCode = ExitCodes.TestSessionAborted;
        }

        return exitCode;
    }
}
