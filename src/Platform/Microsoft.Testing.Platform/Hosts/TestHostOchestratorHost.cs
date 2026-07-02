// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHostOrchestrator;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed class TestHostOrchestratorHost(TestHostOrchestratorConfiguration testHostOrchestratorConfiguration, ServiceProvider serviceProvider) : IHost
{
    private readonly TestHostOrchestratorConfiguration _testHostOrchestratorConfiguration = testHostOrchestratorConfiguration;
    private readonly ServiceProvider _serviceProvider = serviceProvider;

    public async Task<int> RunAsync()
    {
        using IPlatformActivity? activity = _serviceProvider.GetPlatformOTelService()?.StartActivity("TestHostOrchestratorHost");
        ILogger logger = _serviceProvider.GetLoggerFactory().CreateLogger<TestHostOrchestratorHost>();
        if (_testHostOrchestratorConfiguration.TestHostOrchestrators.Length > 1)
        {
            throw new NotSupportedException("Multiple test orchestrator not supported");
        }

        ITestHostExecutionOrchestrator testHostOrchestrator = _testHostOrchestratorConfiguration.TestHostOrchestrators[0];
        ITestApplicationCancellationTokenSource applicationCancellationToken = _serviceProvider.GetTestApplicationCancellationTokenSource();

        // When connected to dotnet test through the pipe protocol, handshake from the orchestrator too
        // (test hosts and test host controllers already do). This lets the SDK know that an orchestrator
        // (e.g. retry) is participating in the run, identified by the OrchestratorFeature property.
        IPushOnlyProtocol? pushOnlyProtocol = _serviceProvider.GetService<IPushOnlyProtocol>();
        if (pushOnlyProtocol is { IsServerMode: true })
        {
            Dictionary<byte, string> additionalHandshakeProperties = new()
            {
                [HandshakeMessagePropertyNames.OrchestratorFeature] = testHostOrchestrator.Uid,
            };

            bool isProtocolCompatible = await pushOnlyProtocol.IsCompatibleProtocolAsync(
                HandshakeMessageHostTypes.TestHostOrchestrator, additionalHandshakeProperties).ConfigureAwait(false);
            if (!isProtocolCompatible)
            {
                return (int)ExitCode.IncompatibleProtocolVersion;
            }

            if (pushOnlyProtocol.IsServerControlChannelSupported)
            {
                // The orchestrator has no graceful-stop capability of its own; a server-initiated cancel maps to
                // cancelling the application token, which propagates to the orchestrated test host processes.
                await pushOnlyProtocol.StartServerControlChannelAsync(_ =>
                {
                    applicationCancellationToken.Cancel();
                    return Task.CompletedTask;
                }).ConfigureAwait(false);
            }
        }

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
            exitCode = (int)ExitCode.TestSessionAborted;
        }

        return exitCode;
    }
}
