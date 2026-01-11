// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.VSTestBridge.Capabilities;
using Microsoft.Testing.Extensions.VSTestBridge.Helpers;
using Microsoft.Testing.Extensions.VSTestBridge.Requests;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;
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
        ITrxReportCapability? capability = capabilities.GetCapability<ITrxReportCapability>();
        IsTrxEnabled = capability is IInternalVSTestBridgeTrxReportCapability internalCapability
            ? internalCapability.IsTrxEnabled
            : capability is ITrxReportCapability { IsSupported: true };
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
                TestExecutionRequest request => ExecuteRequestAsync(request, context.MessageBus, context.CancellationToken),

                _ => Task.CompletedTask,
            };

            await convertedRequest.ConfigureAwait(false);
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
}
