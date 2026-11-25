// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class TestHostBuilder
{
    private const string RetryPipeNameOptionKey = "internal-retry-pipename";

    private async Task<IHost> BuildHostAsync(BuildContext context)
    {
        string? orchestratorChain = _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTORCHESTRATORCHAIN);
        if (orchestratorChain is not null)
        {
            _environment.SetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTORCHESTRATORCHAIN, null);
        }

        if (context.EarlyHost is not null)
        {
            return context.EarlyHost;
        }

        IReadOnlyList<IArtifactPostProcessor> artifactPostProcessors =
            await ((ArtifactPostProcessingManager)ArtifactPostProcessing).BuildAsync(context.ServiceProvider).ConfigureAwait(false);
        context.ServiceProvider.AddServices([.. artifactPostProcessors]);

#pragma warning disable CA1416 // Preserve existing browser behavior while splitting the method.
        DotnetTestConnection pushOnlyProtocol = new(context.CommandLineHandler, _environment, _testApplicationModuleInfo, context.TestApplicationCancellationTokenSource, context.LoggerFactory.CreateLogger<DotnetTestConnection>());
        if (!context.LoggingState.CommandLineParseResult.HasTool
            || context.LoggingState.CommandLineParseResult.ToolName == ArtifactPostProcessingDispatcherTool.ToolName)
        {
            await pushOnlyProtocol.AfterCommonServiceSetupAsync().ConfigureAwait(false);
        }

        if (pushOnlyProtocol.IsServerMode)
        {
            context.ServiceProvider.AddService(pushOnlyProtocol);
        }

        IReadOnlyList<ITool> toolsInformation = await BuildToolsInformationAsync(context).ConfigureAwait(false);
        if (context.LoggingState.CommandLineParseResult.HasTool)
        {
            return await BuildToolsHostAsync(context, toolsInformation).ConfigureAwait(false);
        }

        if (context.IsHelpCommand)
        {
            if (pushOnlyProtocol.IsServerMode)
            {
                // Always perform the handshake (even though we are about to print help) so that
                // dotnet test on the SDK side can validate the test host's execution mode and
                // detect mismatches such as --help being injected via RunArguments during a
                // normal run. The handshake is needed for the SDK to know that the test host
                // is in help mode, so it can ignore any incoming CommandLineOptionMessages.
                bool isProtocolCompatible = await pushOnlyProtocol.IsCompatibleProtocolAsync(
                    HandshakeMessageHostTypes.TestHost,
                    ArtifactPostProcessingHandshakeProperties.Create(artifactPostProcessors)).ConfigureAwait(false);
                if (!isProtocolCompatible)
                {
                    CompleteBuilderActivity(context.BuilderActivity, nameof(InformativeCommandLineHost));
                    return new InformativeCommandLineHost((int)ExitCode.IncompatibleProtocolVersion, context.ServiceProvider);
                }

                await pushOnlyProtocol.HelpInvokedAsync().ConfigureAwait(false);
            }
            else
            {
                await context.CommandLineHandler.PrintHelpAsync(context.ProxyOutputDevice, toolsInformation, context.TestApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);
            }

            CompleteBuilderActivity(context.BuilderActivity, nameof(InformativeCommandLineHost));
            return new InformativeCommandLineHost(0, context.ServiceProvider);
        }
#pragma warning restore CA1416 // Preserve existing browser behavior while splitting the method.

        if (context.IsInfoCommand)
        {
            await context.CommandLineHandler.PrintInfoAsync(context.ProxyOutputDevice, toolsInformation, context.TestApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);
            CompleteBuilderActivity(context.BuilderActivity, nameof(InformativeCommandLineHost));
            return new InformativeCommandLineHost(0, context.ServiceProvider);
        }

        context.InvocationLifetimeHandlers = await BuildInvocationLifetimeHandlersIfOwnerAsync(context).ConfigureAwait(false);

        try
        {
            return await TryBuildTestHostOrchestratorHostAsync(context, orchestratorChain).ConfigureAwait(false) is { } testHostOrchestratorHost
                ? testHostOrchestratorHost
                : await TryBuildTestHostControllersHostAsync(context, orchestratorChain).ConfigureAwait(false) is { } testHostControllerHost
                ? testHostControllerHost
                : await BuildTestHostModeAsync(context).ConfigureAwait(false);
        }
        catch
        {
            await ReleaseInvocationLifetimeHandlersAsync(context.InvocationLifetimeHandlers).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<IReadOnlyList<ITool>> BuildToolsInformationAsync(BuildContext context)
    {
        var toolsServiceProvider = (ServiceProvider)context.ServiceProvider.Clone();
        toolsServiceProvider.TryAddService(context.ProxyOutputDevice);
        return await ((ToolsManager)Tools).BuildAsync(toolsServiceProvider).ConfigureAwait(false);
    }

    private async Task<IHost> BuildToolsHostAsync(BuildContext context, IReadOnlyList<ITool> toolsInformation)
    {
        context.ServiceProvider.TryAddService(context.ProxyOutputDevice);

        ToolsHost toolsHost = new(toolsInformation, context.ServiceProvider, context.CommandLineHandler, context.ProxyOutputDevice);
        await LogTestHostCreatedAsync(
            context.ServiceProvider,
            TelemetryProperties.ApplicationMode.Tool,
            context.BuilderMetrics,
            context.SystemClock.UtcNow,
            context.TestApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);

        CompleteBuilderActivity(context.BuilderActivity, nameof(ToolsHost));
        return toolsHost;
    }
}
