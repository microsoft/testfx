// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.VSTestBridge.Helpers;
using Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;
using Microsoft.Testing.Extensions.VSTestBridge.Requests;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.Testing.Extensions.VSTestBridge;

/// <summary>
/// Represents a base class for bridged test frameworks (support of Microsoft.Testing.Platform while supporting VSTest like APIs).
/// </summary>
public abstract class VSTestBridgedTestFrameworkBase : ITestFramework, IDataProducer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VSTestBridgedTestFrameworkBase"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="capabilities">The test framework capabilities.</param>
    protected VSTestBridgedTestFrameworkBase(IServiceProvider serviceProvider, ITestFrameworkCapabilities capabilities)
    {
        Guard.NotNull(serviceProvider);
        ServiceProvider = serviceProvider;
        IsTrxEnabled = capabilities.GetCapability<ITrxReportCapability>()?.IsSupported == true;
    }

    /// <inheritdoc />
    public abstract string Uid { get; }

    /// <inheritdoc />
    public abstract string Version { get; }

    /// <inheritdoc />
    public abstract string DisplayName { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public Type[] DataTypesProduced { get; } =
    [
        typeof(TestNodeUpdateMessage),
        typeof(SessionFileArtifact),
    ];

    /// <summary>
    /// Gets the service provider.
    /// </summary>
    protected internal IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Gets a value indicating whether the <see cref="TestNodeUid"/> should use <see cref="TestCase.FullyQualifiedName"/> instead of <see cref="TestCase.Id"/>.
    /// </summary>
    protected internal virtual bool UseFullyQualifiedNameAsTestNodeUid { get; }

    /// <summary>
    /// Gets a value indicating whether the TRX report is enabled.
    /// </summary>
    protected internal bool IsTrxEnabled { get; }

    /// <inheritdoc />
    public abstract Task<bool> IsEnabledAsync();

    /// <inheritdoc />
    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        try
        {
            DebugUtils.LaunchAttachDebugger();

            Task convertedRequest = context.Request switch
            {
                VSTestDiscoverTestExecutionRequest discoverRequest =>
                    DiscoverTestsAsync(UpdateDiscoverRequest(discoverRequest, context.MessageBus, context.CancellationToken), context.MessageBus, context.CancellationToken),

                VSTestRunTestExecutionRequest runRequest =>
                    RunTestsAsync(UpdateRunRequest(runRequest, context.MessageBus, context.CancellationToken), context.MessageBus, context.CancellationToken),

                TestExecutionRequest request => ExecuteRequestAsync(request, context.MessageBus, context.CancellationToken),

                _ => Task.CompletedTask,
            };

            await convertedRequest;
        }
        finally
        {
            // Complete the TA request.
            context.Complete();
        }
    }

    /// <inheritdoc />
    public abstract Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context);

    /// <inheritdoc />
    public abstract Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context);

    /// <summary>
    /// Execute the test execution request (discovery, run...).
    /// </summary>
    /// <param name="request">The test execution request.</param>
    /// <param name="messageBus">The message bus.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected abstract Task ExecuteRequestAsync(TestExecutionRequest request, IMessageBus messageBus,
        CancellationToken cancellationToken);

    /// <summary>
    /// Discovers tests.
    /// </summary>
    /// <param name="request">The VSTest discovery request.</param>
    /// <param name="messageBus">The message bus.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected abstract Task DiscoverTestsAsync(VSTestDiscoverTestExecutionRequest request, IMessageBus messageBus,
        CancellationToken cancellationToken);

    /// <summary>
    /// Runs the tests.
    /// </summary>
    /// <param name="request">The VSTest run request.</param>
    /// <param name="messageBus">The message bus.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected abstract Task RunTestsAsync(VSTestRunTestExecutionRequest request, IMessageBus messageBus,
        CancellationToken cancellationToken);

    private VSTestDiscoverTestExecutionRequest UpdateDiscoverRequest(
        VSTestDiscoverTestExecutionRequest discoverRequest,
        IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        // Before passing down the request, we need to replace the discovery sink with a custom implementation calling
        // both the original (VSTest) sink and our own.
        ILoggerFactory loggerFactory = ServiceProvider.GetRequiredService<ILoggerFactory>();
        TestCaseDiscoverySinkAdapter testCaseDiscoverySinkAdapter = new(
            this,
            discoverRequest.Session,
            discoverRequest.AssemblyPaths,
            ServiceProvider.GetTestApplicationModuleInfo(),
            ServiceProvider.GetTestFrameworkCapabilities().GetCapability<INamedFeatureCapability>(),
            ServiceProvider.GetCommandLineOptions(),
            ServiceProvider.GetClientInfo(),
            messageBus,
            loggerFactory,
            IsTrxEnabled,
            cancellationToken,
            discoverRequest.DiscoverySink);

        return new(discoverRequest.Session, discoverRequest.Filter, discoverRequest.AssemblyPaths, discoverRequest.DiscoveryContext,
            discoverRequest.MessageLogger, testCaseDiscoverySinkAdapter);
    }

    private VSTestRunTestExecutionRequest UpdateRunRequest(
        VSTestRunTestExecutionRequest runRequest,
        IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        // Before passing down the request, we need to replace the framework handle with a custom implementation calling
        // both the original (VSTest) framework handle and our own.
        ILoggerFactory loggerFactory = ServiceProvider.GetRequiredService<ILoggerFactory>();
        FrameworkHandlerAdapter frameworkHandlerAdapter = new(
            this,
            runRequest.Session,
            runRequest.AssemblyPaths,
            ServiceProvider.GetTestApplicationModuleInfo(),
            ServiceProvider.GetTestFrameworkCapabilities().GetCapability<INamedFeatureCapability>(),
            ServiceProvider.GetCommandLineOptions(),
            ServiceProvider.GetClientInfo(),
            messageBus,
            ServiceProvider.GetOutputDevice(),
            loggerFactory,
            IsTrxEnabled,
            cancellationToken, runRequest.FrameworkHandle);

        return new(runRequest.Session, runRequest.Filter, runRequest.AssemblyPaths, runRequest.RunContext,
            frameworkHandlerAdapter);
    }
}
