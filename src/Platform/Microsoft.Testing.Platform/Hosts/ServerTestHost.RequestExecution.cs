// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class ServerTestHost
{
    private async Task<object> HandleRequestCoreAsync(RequestMessage message, RpcInvocationState rpcInvocationState, CancellationToken cancellationToken)
    {
        AssertInitialized();

        await _logger.LogDebugAsync($"Received {message.Method} request").ConfigureAwait(false);

        switch (message.Method, message.Params)
        {
            case (_, InvalidRequestParamsArgs invalidParams):
                throw new JsonRpcException(invalidParams.ErrorCode, invalidParams.ErrorMessage);

            case (JsonRpcMethods.Initialize, InitializeRequestArgs args):
                string negotiatedProtocolVersion = JsonRpcProtocolVersions.Negotiate(args.ProtocolVersions)
                    ?? throw new JsonRpcException(
                        ErrorCodes.ProtocolVersionNotSupported,
                        $"None of the client's protocol versions are supported. Server versions: {string.Join(", ", JsonRpcProtocolVersions.Supported)}.");

                _client = new(args.ClientInfo.Name, args.ClientInfo.Version);
                _clientInfoService = new ClientInfoService(args.ClientInfo.Name, args.ClientInfo.Version, new ClientCapabilitiesService(args.Capabilities.IsStateful));
                await _logger.LogDebugAsync(
                    $"Connection established with '{_client.Id}' version '{_client.Version}', protocol version '{negotiatedProtocolVersion}'").ConfigureAwait(false);

                INamedFeatureCapability? namedFeatureCapability = ServiceProvider.GetTestFrameworkCapabilities().GetCapability<INamedFeatureCapability>();
                return new InitializeResponseArgs(
                    ProcessId: ServiceProvider.GetEnvironment().ProcessId,
                    ServerInfo: new ServerInfo("test-anywhere", Version: PlatformVersion.Version),
                    Capabilities: new ServerCapabilities(
                        new ServerTestingCapabilities(
                            SupportsDiscovery: true,
                            // Current implementation of testing platform and VS doesn't allow multi-request.
                            MultiRequestSupport: false,
                            VSTestProviderSupport: namedFeatureCapability?.IsSupported(JsonRpcStrings.VSTestProviderSupport) == true,
                            SupportsAttachments: true,
                            MultiConnectionProvider: false)))
                {
                    ProtocolVersion = negotiatedProtocolVersion,
                };

            case (JsonRpcMethods.TestingDiscoverTests, DiscoverRequestArgs args):
                return await ExecuteRequestAsync(args, JsonRpcMethods.TestingDiscoverTests, rpcInvocationState, cancellationToken).ConfigureAwait(false);

            case (JsonRpcMethods.TestingRunTests, RunRequestArgs args):
                return await ExecuteRequestAsync(args, JsonRpcMethods.TestingRunTests, rpcInvocationState, cancellationToken).ConfigureAwait(false);

            default:
                throw new JsonRpcException(ErrorCodes.MethodNotFound, $"The method '{message.Method}' is not supported.");
        }
    }

    private async Task<ResponseArgsBase> ExecuteRequestAsync(
        RequestArgsBase args,
        string method,
        RpcInvocationState rpcInvocationState,
        CancellationToken cancellationToken)
    {
        var perRequestServiceProvider = (ServiceProvider)ServiceProvider.Clone();

        // Add custom linked ITestApplicationCooperativeLifetimeService cancellation token source
        perRequestServiceProvider.AddService(new PerRequestTestSessionContext(
            rpcInvocationState.CancellationToken,
            cancellationToken));

        perRequestServiceProvider.AddService(new TestHostTestFrameworkInvoker(perRequestServiceProvider));

        DateTimeOffset requestStart = _clock.UtcNow;

        // Avoid allocating request-scoped services that register cancellation callbacks when the request
        // was already cancelled before execution started.
        cancellationToken.ThrowIfCancellationRequested();

        ITestSessionContext perRequestTestSessionContext = perRequestServiceProvider.GetTestSessionContext();
        StopPoliciesService? requestPoliciesService = null;
        PerRequestServerDataConsumer? testNodeUpdateProcessor = null;
        DateTimeOffset adapterLoadStart = default;
        DateTimeOffset adapterLoadStop = default;
        DateTimeOffset requestExecuteStart = default;
        DateTimeOffset? requestExecuteStop = null;
        IGracefulStopTestExecutionCapability? perRequestGracefulStopCapability = null;
        try
        {
            StopPoliciesService applicationPoliciesService = perRequestServiceProvider.GetRequiredService<StopPoliciesService>();
            requestPoliciesService = new(perRequestServiceProvider.GetTestApplicationCancellationTokenSource())
            {
                ProcessRole = applicationPoliciesService.ProcessRole,
            };
            perRequestServiceProvider.ReplaceService<IStopPoliciesService>(requestPoliciesService);
            perRequestServiceProvider.ReplaceService<ITestApplicationProcessExitCode>(new TestApplicationResult(
                perRequestServiceProvider.GetOutputDevice(),
                perRequestServiceProvider.GetCommandLineOptions(),
                perRequestServiceProvider.GetEnvironment(),
                requestPoliciesService,
                perRequestServiceProvider.GetPlatformOTelService(),
                perRequestServiceProvider.GetRequiredService<ITestCoverageResult>()));

            // The JSON-RPC payload owns server request selection. Providers receive a server-origin
            // context so they can explicitly opt out; non-empty contributions are rejected below.
            ServerTestExecutionRequestFactory requestFactory = new(async (session, requestCancellationToken) =>
            {
                ICollection<TestNode>? testNodes = args.TestNodes;
                string? filter = args.GraphFilter;
                ITestExecutionFilter executionFilter = testNodes is not null
                    ? new TestNodeUidListFilter(testNodes.Select(node => node.Uid).ToArray())
                    : filter is not null
                        ? new TreeNodeFilter(filter)
                        : new NopFilter();

                TestExecutionRequestKind requestKind = method switch
                {
                    JsonRpcMethods.TestingRunTests => TestExecutionRequestKind.Run,
                    JsonRpcMethods.TestingDiscoverTests => TestExecutionRequestKind.Discovery,
                    _ => throw new NotImplementedException($"Request not implemented '{method}'"),
                };

                executionFilter = await TestExecutionFilterComposer.ComposeAsync(
                    executionFilter,
                    [.. perRequestServiceProvider.Services.OfType<ITestExecutionFilterProvider>()],
                    new TestExecutionFilterContext(requestKind, TestExecutionRequestOrigin.Server),
                    allowProviderContributions: false,
                    requestCancellationToken).ConfigureAwait(false);

                return requestKind == TestExecutionRequestKind.Run
                    ? new RunTestExecutionRequest(session, executionFilter)
                    : new DiscoverTestExecutionRequest(session, executionFilter);
            });

            // Build the per request objects
            ServerTestExecutionFilterFactory filterFactory = new();
            TestHostTestFrameworkInvoker invoker = new(perRequestServiceProvider);
            testNodeUpdateProcessor = new(perRequestServiceProvider, this, args.RunId, perRequestServiceProvider.GetTask());

            adapterLoadStart = _clock.UtcNow;

            // Add the client info service to the per request service provider
            RoslynDebug.Assert(_clientInfoService is not null, "Request should only have been called after initialization");
            perRequestServiceProvider.TryAddService(_clientInfoService);

            ProxyOutputDevice outputDevice = ServiceProvider.GetRequiredService<ProxyOutputDevice>();
            await outputDevice.InitializeAsync(this).ConfigureAwait(false);

            // Build the per request adapter
            ITestFramework perRequestTestFramework = await _buildTestFrameworkAsync(new TestFrameworkBuilderData(
                perRequestServiceProvider,
                requestFactory,
                invoker,
                filterFactory,
                outputDevice.OriginalOutputDevice,
                [testNodeUpdateProcessor],
                _testFrameworkManager,
                _testSessionManager,
                new MessageBusProxy(),
                method == JsonRpcMethods.TestingDiscoverTests,
                isServerRequest: true)).ConfigureAwait(false);
            perRequestGracefulStopCapability =
                perRequestServiceProvider.GetTestFrameworkCapabilities().GetCapability<IGracefulStopTestExecutionCapability>();
            if (perRequestGracefulStopCapability is not null)
            {
                await RegisterActiveGracefulStopCapabilityAsync(perRequestGracefulStopCapability).ConfigureAwait(false);
            }

            adapterLoadStop = _clock.UtcNow;
            requestExecuteStart = _clock.UtcNow;

            RoslynDebug.Assert(_client is not null, "Request should only have been called after initialization");

            // Execute the request
            await ExecuteRequestAsync(
                outputDevice,
                perRequestServiceProvider.GetTestSessionContext(),
                perRequestServiceProvider,
                perRequestServiceProvider.GetBaseMessageBus(),
                perRequestTestFramework,
                _client,
                method == JsonRpcMethods.TestingDiscoverTests).ConfigureAwait(false);

            // Check if there was a test adapter testSession failure
            ITestApplicationProcessExitCode testApplicationResult = perRequestServiceProvider.GetTestApplicationProcessExitCode();
            if (testApplicationResult.HasTestAdapterTestSessionFailure)
            {
                throw new InvalidOperationException($"TestAdapter testSession failure occurred, '{testApplicationResult.TestAdapterTestSessionFailureErrorMessage}'");
            }

            // Verify request cancellation, above the chain the exception will be
            // catch and propagated as correct json rpc error
            perRequestTestSessionContext.CancellationToken.ThrowIfCancellationRequested();

            requestExecuteStop = _clock.UtcNow;
        }
        finally
        {
            requestExecuteStop ??= _clock.UtcNow;

            if (perRequestGracefulStopCapability is not null)
            {
                UnregisterActiveGracefulStopCapability(perRequestGracefulStopCapability);
            }

            bool requestPoliciesServiceOwnedByProvider =
                requestPoliciesService is not null && perRequestServiceProvider.Services.Contains(requestPoliciesService);

            // Cleanup all services
            // We skip all services that are "cloned" per call because are reused and will be disposed on shutdown.
            await DisposeServiceProviderAsync(perRequestServiceProvider, obj => !ServiceProvider.Services.Contains(obj)).ConfigureAwait(false);

            if (!requestPoliciesServiceOwnedByProvider)
            {
                requestPoliciesService?.Dispose();
            }

            // We need to dispose this service manually because the shared DisposeServiceProviderAsync skip some special service like the ITestApplicationCooperativeLifetimeService
            // that needs to be disposed at process exits.
            // Here we have one crafted for per-call and we won't invoke the stopping events on it in the same way as the global one.
            ((PerRequestTestSessionContext)perRequestTestSessionContext).Dispose();
        }

        DateTimeOffset requestStop = _clock.UtcNow;
        RoslynDebug.Assert(requestExecuteStop != null);
        RoslynDebug.Assert(testNodeUpdateProcessor is not null);

        bool isRunRequest = method switch
        {
            JsonRpcMethods.TestingRunTests => true,
            JsonRpcMethods.TestingDiscoverTests => false,
            _ => throw new NotImplementedException($"Request not implemented '{method}'"),
        };

        Dictionary<string, object> metadata = isRunRequest
            ? GetRunMetrics(
                (RunRequestArgs)args,
                requestStart,
                requestStop,
                adapterLoadStart,
                adapterLoadStop,
                requestExecuteStart,
                requestExecuteStop.Value,
                testNodeUpdateProcessor.GetTestNodeStatistics())
            : GetDiscoveryMetrics(
                (DiscoverRequestArgs)args,
                requestStart,
                requestStop,
                adapterLoadStart,
                adapterLoadStop,
                requestExecuteStart,
                requestExecuteStop.Value,
                testNodeUpdateProcessor.GetTestNodeStatistics().TotalDiscoveredTests);

        await ServiceProvider.GetTelemetryCollector().LogEventAsync(
            isRunRequest ? TelemetryEvents.TestsRunEventName : TelemetryEvents.TestsDiscoveryEventName,
            metadata,
            cancellationToken).ConfigureAwait(false);

        return isRunRequest
            ? new RunResponseArgs([.. testNodeUpdateProcessor.Artifacts])
            : new DiscoverResponseArgs();
    }

    internal static Dictionary<string, object> GetDiscoveryMetrics(
        DiscoverRequestArgs args,
        DateTimeOffset requestStart,
        DateTimeOffset requestStop,
        DateTimeOffset adapterLoadStart,
        DateTimeOffset adapterLoadStop,
        DateTimeOffset requestExecuteStart,
        DateTimeOffset requestExecuteStop,
        long totalTestsDiscovered) => new()
        {
            { TelemetryProperties.RequestProperties.TotalDiscoveredTestsPropertyName, totalTestsDiscovered },
            { TelemetryProperties.RequestProperties.RequestStart, requestStart },
            { TelemetryProperties.RequestProperties.RequestStop, requestStop },
            { TelemetryProperties.RequestProperties.AdapterLoadStart, adapterLoadStart },
            { TelemetryProperties.RequestProperties.AdapterLoadStop, adapterLoadStop },
            { TelemetryProperties.RequestProperties.RequestExecuteStart, requestExecuteStart },
            { TelemetryProperties.RequestProperties.RequestExecuteStop, requestExecuteStop },
            { TelemetryProperties.RequestProperties.IsFilterEnabledPropertyName, (args.TestNodes is not null || args.GraphFilter is not null).AsTelemetryBool() },
        };

    internal static Dictionary<string, object> GetRunMetrics(
        RunRequestArgs args,
        DateTimeOffset requestStart,
        DateTimeOffset requestStop,
        DateTimeOffset adapterLoadStart,
        DateTimeOffset adapterLoadStop,
        DateTimeOffset requestExecuteStart,
        DateTimeOffset requestExecuteStop,
        TestNodeStatistics statistics) => new()
        {
            { TelemetryProperties.RequestProperties.TotalPassedTestsPropertyName, statistics.TotalPassedTests },
            { TelemetryProperties.RequestProperties.TotalFailedTestsPropertyName, statistics.TotalFailedTests },
            { TelemetryProperties.RequestProperties.TotalPassedRetriesPropertyName, statistics.TotalPassedRetries },
            { TelemetryProperties.RequestProperties.TotalFailedRetriesPropertyName, statistics.TotalFailedRetries },
            { TelemetryProperties.RequestProperties.RequestStart, requestStart },
            { TelemetryProperties.RequestProperties.RequestStop, requestStop },
            { TelemetryProperties.RequestProperties.AdapterLoadStart, adapterLoadStart },
            { TelemetryProperties.RequestProperties.AdapterLoadStop, adapterLoadStop },
            { TelemetryProperties.RequestProperties.RequestExecuteStart, requestExecuteStart },
            { TelemetryProperties.RequestProperties.RequestExecuteStop, requestExecuteStop },
            { TelemetryProperties.RequestProperties.IsFilterEnabledPropertyName, (args.TestNodes is not null || args.GraphFilter is not null).AsTelemetryBool() },
        };
}
