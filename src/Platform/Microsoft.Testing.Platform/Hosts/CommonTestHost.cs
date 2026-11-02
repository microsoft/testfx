// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;

namespace Microsoft.Testing.Platform.Hosts;

/// <summary>
/// This represents either a test host (console or server), or a test host controller.
/// This doesn't represent an orchestrator host.
/// </summary>
[StackTraceHidden]
internal abstract partial class CommonHost(ServiceProvider serviceProvider) : IHost
{
#if NET9_0_OR_GREATER
    private readonly Lock _activeGracefulStopCapabilitiesSync = new();
#else
    private readonly object _activeGracefulStopCapabilitiesSync = new();
#endif
    private readonly List<IGracefulStopTestExecutionCapability> _activeGracefulStopCapabilities = [];
    private CancellationToken _gracefulSessionStopCancellationToken;
    private bool _isGracefulSessionStopRequested;

    public ServiceProvider ServiceProvider => serviceProvider;

    protected IPushOnlyProtocol? PushOnlyProtocol => ServiceProvider.GetService<IPushOnlyProtocol>();

    protected abstract bool RunTestApplicationLifeCycleCallbacks { get; }

    /// <summary>
    /// Gets a value indicating whether this host is the one that observes the test results, and therefore owns the
    /// run-level verdict reported to telemetry.
    /// </summary>
    /// <remarks>
    /// The test host controller shares the application's <c>TestApplicationResult</c> but never consumes a test node
    /// update: the tests run in the child test host it launches.
    /// </remarks>
    private bool OwnsRunVerdict => this is not TestHostControllersTestHost;

    public async Task<int> RunAsync()
    {
        CancellationToken testApplicationCancellationToken = ServiceProvider.GetTestApplicationCancellationTokenSource().CancellationToken;
        List<object> alreadyDisposed = [];

        int exitCode = (int)ExitCode.GenericFailure;
        IPlatformOpenTelemetryService? platformOTelService = null;
        IPlatformActivity? activity = null;
        try
        {
            platformOTelService = ServiceProvider.GetPlatformOTelService();
            if (platformOTelService is not null)
            {
                ExtensionHelper.ConfigureOTelLegacyAttributes(
                    PlatformOpenTelemetryOptions.FromEnvironment(ServiceProvider.GetEnvironment()));
            }

            string hostType = GetHostType();

            // When the builder activity has already been closed (or OTel was configured late) there is no ambient
            // parent, so fall back to the W3C trace context published by the process that started this run.
            string? environmentParentId = platformOTelService is not null && !platformOTelService.HasCurrentActivity
                ? EnvironmentTraceContext.TryGetParentId(ServiceProvider.GetEnvironment())
                : null;

            if (environmentParentId is not null && platformOTelService!.RootTraceState is null)
            {
                platformOTelService.RootTraceState = EnvironmentTraceContext.TryGetTraceState(ServiceProvider.GetEnvironment());
            }

            activity = platformOTelService?.StartActivity(
                hostType,
                tags: [new(TestingPlatformSemanticConventions.Attributes.TestHostType, hostType)],
                parentId: environmentParentId);

            if (PushOnlyProtocol is null || PushOnlyProtocol?.IsServerMode == false)
            {
                exitCode = await RunTestAppAsync(platformOTelService, testApplicationCancellationToken, alreadyDisposed).ConfigureAwait(false);

                if (testApplicationCancellationToken.IsCancellationRequested)
                {
                    exitCode = (int)ExitCode.TestSessionAborted;
                }

                return exitCode;
            }

            try
            {
                RoslynDebug.Assert(PushOnlyProtocol is not null);

                IReadOnlyDictionary<byte, string>? additionalHandshakeProperties =
                    SupportsArtifactPostProcessing(hostType)
                        ? ArtifactPostProcessingHandshakeProperties.Create(ServiceProvider.GetServicesInternal<IArtifactPostProcessor>())
                        : null;
                bool isValidProtocol = await PushOnlyProtocol.IsCompatibleProtocolAsync(hostType, additionalHandshakeProperties).ConfigureAwait(false);

                if (isValidProtocol && PushOnlyProtocol.IsServerControlChannelSupported)
                {
                    // Start listening for server-initiated signals (e.g. session cancellation) before running tests
                    // so a signal that arrives mid-run is observed. React by stopping gracefully where possible.
                    await PushOnlyProtocol.StartServerControlChannelAsync(RequestGracefulSessionStopAsync).ConfigureAwait(false);
                }

                exitCode = isValidProtocol
                    ? await RunTestAppAsync(platformOTelService, testApplicationCancellationToken, alreadyDisposed).ConfigureAwait(false)
                    : (int)ExitCode.IncompatibleProtocolVersion;
            }
            finally
            {
                if (PushOnlyProtocol is not null)
                {
                    await PushOnlyProtocol.OnExitAsync().ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (testApplicationCancellationToken.IsCancellationRequested)
        {
            // We do nothing we're canceling
        }
        finally
        {
            // Normalize the cancellation verdict *before* the span and the run telemetry read it. The
            // post-finally adjustment below runs too late for them, so without this a cancelled run was traced
            // as a generic failure and test.run.exit_code did not match the code the process exits with.
            if (testApplicationCancellationToken.IsCancellationRequested)
            {
                exitCode = (int)ExitCode.TestSessionAborted;
            }

            // Emit the run-level telemetry while the OpenTelemetry providers and the root span are still alive.
            // DisposeServiceProviderAsync below tears the providers down in registration order, and they are
            // registered before TestApplicationResult, so anything recorded after this point would be dropped.
            //
            // Only the host that actually observed the results owns the verdict. The test host controller shares
            // the same TestApplicationResult instance but consumes no TestNodeUpdateMessage (the tests run in the
            // child process), so reporting from there would emit a second, contradictory run record with zero
            // counts and a ZeroTests exit code.
            if (OwnsRunVerdict
                && ServiceProvider.GetService<ITestApplicationProcessExitCode>() is TestApplicationResult testApplicationResult)
            {
                testApplicationResult.ReportRunTelemetry(activity, exitCode);
            }

            // Record the run verdict on the root span before closing it, so a trace search on
            // test.run.exit_code finds failing runs without having to open them.
            activity?.SetTag(TestingPlatformSemanticConventions.Attributes.TestRunExitCode, exitCode);
            activity?.SetStatus(
                exitCode == (int)ExitCode.Success ? PlatformActivityStatusCode.Ok : PlatformActivityStatusCode.Error);

            // Dispose the activity
            activity?.Dispose();

            await DisposeServiceProviderCoreAsync(
                ServiceProvider,
                filter: null,
                alreadyDisposed: alreadyDisposed,
                isProcessShutdown: true,
                disposeServiceAsync: DisposeProcessShutdownServiceAsync).ConfigureAwait(false);
            if (ServiceProvider.GetService<FileLoggerProvider>() is { } fileLoggerProvider
                && !alreadyDisposed.Contains(fileLoggerProvider))
            {
                await DisposeProcessShutdownServiceAsync(fileLoggerProvider).ConfigureAwait(false);
                alreadyDisposed.Add(fileLoggerProvider);
            }

            // Dispose the LoggerFactoryProxy last so that all user-registered logger providers
            // (e.g., Microsoft.Extensions.Logging providers added via the Microsoft.Testing.Extensions.Logging
            // bridge such as Serilog, Application Insights, OpenTelemetry) get a chance to flush their buffers.
            // The proxy is skipped by DisposeServiceProviderAsync for ordering reasons.
            if (ServiceProvider.GetService<LoggerFactoryProxy>() is { } loggerFactoryProxy
                && !alreadyDisposed.Contains(loggerFactoryProxy))
            {
                await DisposeProcessShutdownServiceAsync(loggerFactoryProxy).ConfigureAwait(false);
                alreadyDisposed.Add(loggerFactoryProxy);
            }

            if (PushOnlyProtocol is not null && !alreadyDisposed.Contains(PushOnlyProtocol))
            {
                await DisposeProcessShutdownServiceAsync(PushOnlyProtocol).ConfigureAwait(false);
                alreadyDisposed.Add(PushOnlyProtocol);
            }

            // This is intentional that we are not disposing the CTS.
            // An unobserved task exception could be raised after the dispose, and we want to use OutputDevice there
            // which needs CTS down the path.
            // await DisposeHelper.DisposeAsync(ServiceProvider.GetTestApplicationCancellationTokenSource());
        }

        if (testApplicationCancellationToken.IsCancellationRequested)
        {
            exitCode = (int)ExitCode.TestSessionAborted;
        }

        return exitCode;
    }

    internal static bool SupportsArtifactPostProcessing(string hostType)
        => hostType is HandshakeMessageHostTypes.TestHost
            or HandshakeMessageHostTypes.ServerTestHost
            or HandshakeMessageHostTypes.TestHostController;

    protected virtual string HostType
        => this switch
        {
            ConsoleTestHost => HandshakeMessageHostTypes.TestHost,
            TestHostControllersTestHost => HandshakeMessageHostTypes.TestHostController,
            ServerTestHost => HandshakeMessageHostTypes.ServerTestHost,
            _ => throw new InvalidOperationException($"Unknown host type '{GetType().FullName}'"),
        };

    private string GetHostType()
    {
        // TestHostOrchestratorHost does not inherit from CommonHost, so the orchestrator handshake is
        // performed there directly (see TestHostOrchestratorHost.RunAsync) rather than going through
        // this path. This method only covers the test host and test host controller roles.
        string hostType = HostType;
        return hostType;
    }

    // Reaction to a server-initiated session cancellation coming over the reverse control pipe. Prefer a graceful
    // stop so the framework stops scheduling new tests but still emits trx/logs/artifacts for whatever completed
    // (mirroring the local '--maximum-failed-tests' behavior). Fall back to hard cancellation when the running
    // framework has no graceful-stop capability (e.g. the test host controller), which is the only lever left.
    protected Task RegisterActiveGracefulStopCapabilityAsync(IGracefulStopTestExecutionCapability capability)
    {
        CancellationToken cancellationToken;
        bool stopCapability;
        lock (_activeGracefulStopCapabilitiesSync)
        {
            stopCapability = _isGracefulSessionStopRequested && !_activeGracefulStopCapabilities.Contains(capability);
            cancellationToken = _gracefulSessionStopCancellationToken;
            _activeGracefulStopCapabilities.Add(capability);
        }

        return stopCapability
            ? capability.StopTestExecutionAsync(cancellationToken)
            : Task.CompletedTask;
    }

    protected void UnregisterActiveGracefulStopCapability(IGracefulStopTestExecutionCapability capability)
    {
        lock (_activeGracefulStopCapabilitiesSync)
        {
            _activeGracefulStopCapabilities.Remove(capability);
        }
    }

    protected async Task RequestGracefulSessionStopAsync(CancellationToken cancellationToken)
    {
        IGracefulStopTestExecutionCapability[] capabilities;
        lock (_activeGracefulStopCapabilitiesSync)
        {
            _isGracefulSessionStopRequested = true;
            _gracefulSessionStopCancellationToken = cancellationToken;
            capabilities = [.. _activeGracefulStopCapabilities.Distinct()];
        }

        if (capabilities.Length > 0)
        {
            await Task.WhenAll(capabilities.Select(capability => capability.StopTestExecutionAsync(cancellationToken))).ConfigureAwait(false);
            return;
        }

        IGracefulStopTestExecutionCapability? applicationCapability =
            ServiceProvider.GetService<ITestFrameworkCapabilities>()?.GetCapability<IGracefulStopTestExecutionCapability>();

        if (applicationCapability is not null)
        {
            await applicationCapability.StopTestExecutionAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            ServiceProvider.GetTestApplicationCancellationTokenSource().Cancel();
        }
    }

    private async Task<int> RunTestAppAsync(IPlatformOpenTelemetryService? platformOTelService, CancellationToken testApplicationCancellationToken, List<object> alreadyDisposed)
    {
        if (RunTestApplicationLifeCycleCallbacks)
        {
            using (platformOTelService?.StartActivity("BeforeRunCallbacks"))
            {
                // Get the test application lifecycle callbacks to be able to call the before run
                foreach (ITestHostApplicationLifetime testApplicationLifecycleCallbacks in ServiceProvider.GetServicesInternal<ITestHostApplicationLifetime>())
                {
                    using IPlatformActivity? activity = platformOTelService?.StartActivity(testApplicationLifecycleCallbacks.Uid, testApplicationLifecycleCallbacks.ToOTelTags());
                    await testApplicationLifecycleCallbacks.BeforeRunAsync(testApplicationCancellationToken).ConfigureAwait(false);
                }
            }
        }

        int exitCode;
        using (platformOTelService?.StartActivity("Run"))
        {
            exitCode = await InternalRunAsync(testApplicationCancellationToken, alreadyDisposed).ConfigureAwait(false);
        }

        if (RunTestApplicationLifeCycleCallbacks)
        {
            using (platformOTelService?.StartActivity("AfterRunCallbacks"))
            {
                foreach (ITestHostApplicationLifetime testApplicationLifecycleCallbacks in ServiceProvider.GetServicesInternal<ITestHostApplicationLifetime>())
                {
                    using IPlatformActivity? activity = platformOTelService?.StartActivity(testApplicationLifecycleCallbacks.Uid, testApplicationLifecycleCallbacks.ToOTelTags());
                    await testApplicationLifecycleCallbacks.AfterRunAsync(exitCode, testApplicationCancellationToken).ConfigureAwait(false);
                    await DisposeHelper.DisposeAsync(testApplicationLifecycleCallbacks).ConfigureAwait(false);
                    alreadyDisposed.Add(testApplicationLifecycleCallbacks);
                }
            }
        }

        return exitCode;
    }

    protected abstract Task<int> InternalRunAsync(CancellationToken cancellationToken, List<object> alreadyDisposed);

    protected virtual Task DisposeProcessShutdownServiceAsync(object service)
        => DisposeHelper.DisposeAsync(service);
}
