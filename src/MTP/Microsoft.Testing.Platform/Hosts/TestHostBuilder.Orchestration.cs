// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHostControllers;
using Microsoft.Testing.Platform.TestHostOrchestrator;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class TestHostBuilder
{
    private async Task<IHost?> TryBuildTestHostOrchestratorHostAsync(BuildContext context, string? orchestratorChain)
    {
        if (context.CommandLineHandler.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey))
        {
            return null;
        }

        TestHostOrchestratorConfiguration testHostOrchestratorConfiguration = await _testHostOrchestratorManager.BuildAsync(context.ServiceProvider, orchestratorChain).ConfigureAwait(false);

        // Middleware only wraps an existing orchestrator invocation (see
        // ITestHostExecutionOrchestratorMiddleware); it does not stand in for one. Registering middleware
        // without also registering exactly one orchestrator is a misconfiguration, not silently ignored.
        bool hasActiveMiddleware = testHostOrchestratorConfiguration.Middleware.Length > 0;

        if (testHostOrchestratorConfiguration.TestHostOrchestrators.Length == 0 && !hasActiveMiddleware)
        {
            return null;
        }

        ValidateTestHostOrchestratorMiddlewareConfiguration(
            testHostOrchestratorConfiguration.TestHostOrchestrators.Length,
            testHostOrchestratorConfiguration.Middleware.Length);

        bool requiresAuthorization = testHostOrchestratorConfiguration.TestHostOrchestrators.Any(
            static orchestrator => orchestrator is ITestHostControllerConnectionAuthorizationConsumer);
        bool requiresLauncherCheck = testHostOrchestratorConfiguration.TestHostOrchestrators.Any(
            static orchestrator => orchestrator is PartitioningOrchestrator);
        if (requiresAuthorization || requiresLauncherCheck)
        {
            ITestHostLauncher? testHostLauncher =
                await ((TestHostControllersManager)TestHostControllers).BuildTestHostLauncherAsync(context.ServiceProvider).ConfigureAwait(false);
            if (requiresAuthorization)
            {
                ExecutableInfo executableInfo = context.ServiceProvider.GetTestApplicationModuleInfo().GetCurrentExecutableInfo();
                await context.ServiceProvider.ResolveTestHostControllerAuthorizedSecurityIdentitiesAsync(
                    testHostLauncher,
                    executableInfo.FilePath,
                    context.LoggerFactory.CreateLogger<TestHostBuilder>(),
                    context.TestApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);
            }
        }

        context.PoliciesService.ProcessRole = TestProcessRole.TestHostOrchestrator;
        await context.ProxyOutputDevice.HandleProcessRoleAsync(TestProcessRole.TestHostOrchestrator, context.TestApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);

        ITestHostOrchestratorApplicationLifetime[] orchestratorLifetimes = testHostOrchestratorConfiguration.IsOutermostProcess
            ? await _testHostOrchestratorManager.BuildTestHostOrchestratorApplicationLifetimesAsync(context.ServiceProvider).ConfigureAwait(false)
            : [];
        context.ServiceProvider.AddServices(orchestratorLifetimes);
        AddInvocationLifetimeRunner(
            context,
            context.ServiceProvider,
            setOwnerMarker: true,
            setupTimeout: context.GlobalTestTimeout);

        CompleteBuilderActivity(context.BuilderActivity, nameof(TestHostOrchestratorHost));
        return new TestHostOrchestratorHost(testHostOrchestratorConfiguration, context.ServiceProvider);
    }

    internal static void ValidateTestHostOrchestratorMiddlewareConfiguration(int orchestratorCount, int middlewareCount)
    {
        if (middlewareCount > 0 && orchestratorCount != 1)
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.TestHostExecutionOrchestratorMiddlewareRequiresExactlyOneOrchestratorErrorMessage,
                    orchestratorCount));
        }
    }

    private async Task<IHost?> TryBuildTestHostControllersHostAsync(BuildContext context, string? orchestratorChain)
    {
        if (ShouldSkipTestHostControllersHost(
            context.TestHostControllerInfo,
            context.CommandLineHandler,
            context.SystemEnvironment))
        {
            return null;
        }

        PassiveNode? passiveNode = null;
        if (context.IsJsonRpcProtocol)
        {
            IMessageHandlerFactory messageHandlerFactory = ServerModeManager.Build(context.ServiceProvider);
            passiveNode = new PassiveNode(
                messageHandlerFactory,
                context.TestApplicationCancellationTokenSource,
                context.SystemEnvironment,
                context.SystemMonitorAsyncFactory,
                context.LoggerFactory.CreateLogger<PassiveNode>());
        }

        var testHostControllersServiceProvider = (ServiceProvider)context.ServiceProvider.Clone();
        testHostControllersServiceProvider.TryAddService(context.ProxyOutputDevice);
        testHostControllersServiceProvider.TryAddService(new MessageBusProxy());

        TestHostControllerConfiguration testHostControllers = await ((TestHostControllersManager)TestHostControllers).BuildAsync(testHostControllersServiceProvider).ConfigureAwait(false);
        if (!testHostControllers.RequireProcessRestart)
        {
            return null;
        }

        if (OperatingSystem.IsBrowser() || OperatingSystem.IsWasi())
        {
            throw new PlatformNotSupportedException(PlatformResources.TestHostControllerProcessRestartNotSupportedOnWebAssembly);
        }

        context.TestHostControllerInfo.IsCurrentProcessTestHostController = true;
        AddInvocationLifetimeRunner(
            context,
            testHostControllersServiceProvider,
            setOwnerMarker: true,
            setupTimeout: context.GlobalTestTimeout);
        context.PoliciesService.ProcessRole = TestProcessRole.TestHostController;
        await context.ProxyOutputDevice.HandleProcessRoleAsync(TestProcessRole.TestHostController, context.TestApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);

        TestHostControllersTestHost testHostControllersHost = new(
            testHostControllers,
            testHostControllersServiceProvider,
            passiveNode,
            context.SystemEnvironment,
            context.LoggerFactory,
            context.SystemClock)
        {
            OrchestratorChain = orchestratorChain,
        };

        await LogTestHostCreatedAsync(
            context.ServiceProvider,
            TelemetryProperties.ApplicationMode.TestHostControllers,
            context.BuilderMetrics,
            context.SystemClock.UtcNow,
            context.TestApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);

        CompleteBuilderActivity(context.BuilderActivity, nameof(TestHostControllersTestHost));
        return testHostControllersHost;
    }

    internal static bool ShouldSkipTestHostControllersHost(
        ITestHostControllerInfo testHostControllerInfo,
        ICommandLineOptions commandLineOptions,
        IEnvironment environment)
        => (testHostControllerInfo.HasTestHostController
                && environment.GetEnvironmentVariable(
                    $"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_SKIPEXTENSION}_{testHostControllerInfo.GetTestHostControllerPID()}") == "1")
            || (commandLineOptions.IsOptionSet(RetryPipeNameOptionKey)
                && environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_SKIPEXTENSION) == "1")
            || commandLineOptions.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey);
}
