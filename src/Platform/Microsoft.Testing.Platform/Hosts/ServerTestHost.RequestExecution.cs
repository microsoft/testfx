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
        var perRequestServiceProvider = (ServiceProvider)ServiceProvider.Clone();

        // Add custom linked ITestApplicationCooperativeLifetimeService cancellation token source
        perRequestServiceProvider.AddService(new PerRequestTestSessionContext(
            rpcInvocationState.CancellationToken,
            cancellationToken));

        perRequestServiceProvider.AddService(new TestHostTestFrameworkInvoker(perRequestServiceProvider));

        AssertInitialized();

        await _logger.LogDebugAsync($"Received {message.Method} request").ConfigureAwait(false);

        switch (message.Method, message.Params)
        {
            case (_, InvalidRequestParamsArgs invalidParams):
                throw new JsonRpcException(invalidParams.ErrorCode, invalidParams.ErrorMessage);

            case (JsonRpcMethods.Initialize, InitializeRequestArgs args):
                _client = new(args.ClientInfo.Name, args.ClientInfo.Version);
                _clientInfoService = new ClientInfoService(args.ClientInfo.Name, args.ClientInfo.Version);
                await _logger.LogDebugAsync($"Connection established with '{_client.Id}', protocol version {_client.Version}").ConfigureAwait(false);

                INamedFeatureCapability? namedFeatureCapability = ServiceProvider.GetTestFrameworkCapabilities().GetCapability<INamedFeatureCapability>();
                return new InitializeResponseArgs(
                    ProcessId: ServiceProvider.GetEnvironment().ProcessId,
                    ServerInfo: new ServerInfo("test-anywhere", Version: ProtocolVersion),
                    Capabilities: new ServerCapabilities(
                        new ServerTestingCapabilities(
                            SupportsDiscovery: true,
                            // Current implementation of testing platform and VS doesn't allow multi-request.
                            MultiRequestSupport: false,
                            VSTestProviderSupport: namedFeatureCapability?.IsSupported(JsonRpcStrings.VSTestProviderSupport) == true,
                            SupportsAttachments: true,
                            MultiConnectionProvider: false)));

            case (JsonRpcMethods.TestingDiscoverTests, DiscoverRequestArgs args):
                return await ExecuteRequestAsync(args, JsonRpcMethods.TestingDiscoverTests, perRequestServiceProvider, cancellationToken).ConfigureAwait(false);

            case (JsonRpcMethods.TestingRunTests, RunRequestArgs args):
                return await ExecuteRequestAsync(args, JsonRpcMethods.TestingRunTests, perRequestServiceProvider, cancellationToken).ConfigureAwait(false);

            default:
                throw new NotImplementedException();
        }
    }

    private async Task<ResponseArgsBase> ExecuteRequestAsync(RequestArgsBase args, string method, ServiceProvider perRequestServiceProvider, CancellationToken cancellationToken)
    {
        DateTimeOffset requestStart = _clock.UtcNow;
        ITestSessionContext perRequestTestSessionContext = perRequestServiceProvider.GetTestSessionContext();

        // Verify request cancellation, above the chain the exception will be
        // catch and propagated as correct json rpc error
        cancellationToken.ThrowIfCancellationRequested();

        // Note: Currently the request generation and filtering isn't extensible
        // in server mode, we create NoOp services, so that they're always available.
        ServerTestExecutionRequestFactory requestFactory = new(session =>
        {
            ICollection<TestNode>? testNodes = args.TestNodes;
            string? filter = args.GraphFilter;
            ITestExecutionFilter executionFilter = testNodes is not null
                ? new TestNodeUidListFilter(testNodes.Select(node => node.Uid).ToArray())
                : filter is not null
                    ? new TreeNodeFilter(filter)
                    : new NopFilter();

            return method == JsonRpcMethods.TestingRunTests
                ? new RunTestExecutionRequest(session, executionFilter)
                : method == JsonRpcMethods.TestingDiscoverTests
                    ? new DiscoverTestExecutionRequest(session, executionFilter)
                    : throw new NotImplementedException($"Request not implemented '{method}'");
        });

        // Build the per request objects
        ServerTestExecutionFilterFactory filterFactory = new();
        TestHostTestFrameworkInvoker invoker = new(perRequestServiceProvider);
        PerRequestServerDataConsumer testNodeUpdateProcessor = new(perRequestServiceProvider, this, args.RunId, perRequestServiceProvider.GetTask());

        // Add the client info service to the per request service provider
        RoslynDebug.Assert(_clientInfoService is not null, "Request should only have been called after initialization");
        perRequestServiceProvider.TryAddService(_clientInfoService);

        DateTimeOffset adapterLoadStart = _clock.UtcNow;

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
            method == JsonRpcMethods.TestingDiscoverTests)).ConfigureAwait(false);

        DateTimeOffset adapterLoadStop = _clock.UtcNow;
        DateTimeOffset requestExecuteStart = _clock.UtcNow;
        DateTimeOffset? requestExecuteStop = null;
        try
        {
            RoslynDebug.Assert(_client is not null, "Request should only have been called after initialization");

            // Execute the request
            await ExecuteRequestAsync(
                outputDevice,
                perRequestServiceProvider.GetTestSessionContext(),
                perRequestServiceProvider,
                perRequestServiceProvider.GetBaseMessageBus(),
                perRequestTestFramework,
                _client).ConfigureAwait(false);

            // Check if there was a test adapter testSession failure
            ITestApplicationProcessExitCode testApplicationResult = perRequestServiceProvider.GetTestApplicationProcessExitCode();
            if (testApplicationResult.HasTestAdapterTestSessionFailure)
            {
                throw new InvalidOperationException($"TestAdapter testSession failure occurred, '{testApplicationResult.TestAdapterTestSessionFailureErrorMessage}'");
            }

            // Verify request cancellation, above the chain the exception will be
            // catch and propagated as correct json rpc error
            perRequestTestSessionContext.CancellationToken.ThrowIfCancellationRequested();

            await SendTestUpdateCompleteAsync(args.RunId, cancellationToken).ConfigureAwait(false);
            requestExecuteStop = _clock.UtcNow;
        }
        finally
        {
            requestExecuteStop ??= _clock.UtcNow;

            // Cleanup all services
            // We skip all services that are "cloned" per call because are reused and will be disposed on shutdown.
            await DisposeServiceProviderAsync(perRequestServiceProvider, obj => !ServiceProvider.Services.Contains(obj)).ConfigureAwait(false);

            // We need to dispose this service manually because the shared DisposeServiceProviderAsync skip some special service like the ITestApplicationCooperativeLifetimeService
            // that needs to be disposed at process exits.
            // Here we have one crafted for per-call and we won't invoke the stopping events on it in the same way as the global one.
            ((PerRequestTestSessionContext)perRequestTestSessionContext).Dispose();
        }

        DateTimeOffset requestStop = _clock.UtcNow;
        RoslynDebug.Assert(requestExecuteStop != null);

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
