// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.VSTestBridge.Helpers;
using Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;
using Microsoft.Testing.Extensions.VSTestBridge.Requests;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.VSTestBridge;

public abstract class VSTestBridgedTestFrameworkBase : ITestFramework, IDataProducer
{
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
        typeof(TestNodeFileArtifact)
    ];

    protected internal IServiceProvider ServiceProvider { get; }

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

    protected abstract Task ExecuteRequestAsync(TestExecutionRequest request, IMessageBus messageBus,
        CancellationToken cancellationToken);

    protected abstract Task DiscoverTestsAsync(VSTestDiscoverTestExecutionRequest request, IMessageBus messageBus,
        CancellationToken cancellationToken);

    protected abstract Task RunTestsAsync(VSTestRunTestExecutionRequest request, IMessageBus messageBus,
        CancellationToken cancellationToken);

    private VSTestDiscoverTestExecutionRequest UpdateDiscoverRequest(
        VSTestDiscoverTestExecutionRequest discoverRequest,
        IMessageBus messageBus, CancellationToken cancellationToken)
    {
        // Before passing down the request, we need to replace the discovery sink with a custom implementation calling
        // both the original (VSTest) sink and our own.
        ITestApplicationModuleInfo testApplicationModuleInfo = ServiceProvider.GetTestApplicationModuleInfo();
        ILoggerFactory loggerFactory = ServiceProvider.GetRequiredService<ILoggerFactory>();
        TestCaseDiscoverySinkAdapter testCaseDiscoverySinkAdapter = new(this, discoverRequest.Session, discoverRequest.AssemblyPaths, testApplicationModuleInfo, loggerFactory, messageBus, IsTrxEnabled, cancellationToken, discoverRequest.DiscoverySink);

        return new(discoverRequest.Session, discoverRequest.VSTestFilter, discoverRequest.AssemblyPaths, discoverRequest.DiscoveryContext,
            discoverRequest.MessageLogger, testCaseDiscoverySinkAdapter);
    }

    private VSTestRunTestExecutionRequest UpdateRunRequest(VSTestRunTestExecutionRequest runRequest, IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        // Before passing down the request, we need to replace the framework handle with a custom implementation calling
        // both the original (VSTest) framework handle and our own.
        ITestApplicationModuleInfo testApplicationModuleInfo = ServiceProvider.GetTestApplicationModuleInfo();
        ILoggerFactory loggerFactory = ServiceProvider.GetRequiredService<ILoggerFactory>();
        IOutputDevice outputDevice = ServiceProvider.GetOutputDevice();
        FrameworkHandlerAdapter frameworkHandlerAdapter = new(this, runRequest.Session, runRequest.AssemblyPaths, testApplicationModuleInfo,
            loggerFactory, messageBus, outputDevice, IsTrxEnabled, cancellationToken, runRequest.FrameworkHandle);

        return new(runRequest.Session, runRequest.VSTestFilter, runRequest.AssemblyPaths, runRequest.RunContext,
            frameworkHandlerAdapter);
    }
}
