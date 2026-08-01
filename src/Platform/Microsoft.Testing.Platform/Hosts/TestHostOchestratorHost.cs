// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.ExceptionServices;

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
        }
        catch (OperationCanceledException) when (applicationCancellationToken.CancellationToken.IsCancellationRequested)
        {
            // We do nothing we're canceling
            exitCode = (int)ExitCode.TestSessionAborted;
        }
        catch
        {
            // The orchestrator faulted (for example retry rejecting server mode or hot reload, which throws
            // before running anything). A lifetime that acquired a resource in BeforeRunAsync still has to
            // release it, or an Azure DevOps test run created there stays "InProgress" and never appears in
            // the build's Tests tab. Failures during that release are swallowed so they cannot replace the
            // exception that actually caused the run to fail.
            await RunAfterRunAsync((int)ExitCode.GenericFailure, swallowFailures: true).ConfigureAwait(false);
            throw;
        }

        // AfterRunAsync also runs when the orchestration was canceled, because cancellation is exactly when
        // releasing what BeforeRunAsync acquired matters most.
        await RunAfterRunAsync(exitCode, swallowFailures: false).ConfigureAwait(false);

        return exitCode;

        async Task RunAfterRunAsync(int reportedExitCode, bool swallowFailures)
        {
            Exception? firstFailure = null;

            foreach (ITestHostOrchestratorApplicationLifetime orchestratorLifetime in _serviceProvider.GetServicesInternal<ITestHostOrchestratorApplicationLifetime>())
            {
                // Every lifetime gets its AfterRunAsync and its disposal, whatever the previous one did:
                // one extension failing to release its resource must not strand everyone else's.
                try
                {
                    await orchestratorLifetime.AfterRunAsync(reportedExitCode, applicationCancellationToken.CancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (applicationCancellationToken.CancellationToken.IsCancellationRequested)
                {
                    await logger.LogDebugAsync($"Orchestrator lifetime '{orchestratorLifetime.Uid}' did not complete AfterRunAsync because the run was canceled.").ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Logged rather than discarded: a lifetime that consistently fails to release its
                    // resource is otherwise undiagnosable, even with --diagnostic.
                    await logger.LogWarningAsync($"Orchestrator lifetime '{orchestratorLifetime.Uid}' failed in AfterRunAsync: {ex}").ConfigureAwait(false);
                    firstFailure ??= ex;
                }

                try
                {
                    await DisposeHelper.DisposeAsync(orchestratorLifetime).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await logger.LogWarningAsync($"Orchestrator lifetime '{orchestratorLifetime.Uid}' failed to dispose: {ex}").ConfigureAwait(false);
                    firstFailure ??= ex;
                }
            }

            // On the faulted path the caller is about to rethrow the exception that actually failed the
            // run; surfacing a cleanup failure instead would hide it.
            if (firstFailure is not null && !swallowFailures)
            {
                ExceptionDispatchInfo.Capture(firstFailure).Throw();
            }
        }
    }
}
