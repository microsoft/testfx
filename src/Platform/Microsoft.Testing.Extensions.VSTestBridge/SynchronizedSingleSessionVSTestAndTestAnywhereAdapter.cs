// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.Requests;
using Microsoft.Testing.Extensions.VSTestBridge.Resources;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Extensions.VSTestBridge;

/// <summary>
/// A specialized bridged test framework base class that supports a single test session.
/// </summary>
public abstract class SynchronizedSingleSessionVSTestBridgedTestFramework : VSTestBridgedTestFrameworkBase, IDisposable
{
    private readonly IExtension _extension;
    private readonly Func<IEnumerable<Assembly>> _getTestAssemblies;
    private readonly CountdownEvent _incomingRequestCounter = new(1);
    private bool _isDisposed;
    private SessionUid? _sessionUid;

    /// <summary>
    /// Initializes a new instance of the <see cref="SynchronizedSingleSessionVSTestBridgedTestFramework"/> class.
    /// </summary>
    /// <param name="extension">The test framework extension.</param>
    /// <param name="getTestAssemblies">A function to get the list of assemblies for this session.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="capabilities">The test framework capabilities.</param>
    protected SynchronizedSingleSessionVSTestBridgedTestFramework(IExtension extension, Func<IEnumerable<Assembly>> getTestAssemblies,
        IServiceProvider serviceProvider, ITestFrameworkCapabilities capabilities)
        : base(serviceProvider, capabilities)
    {
        _extension = extension;
        _getTestAssemblies = getTestAssemblies;
    }

    /// <inheritdoc />
    public sealed override string Uid => _extension.Uid;

    /// <inheritdoc />
    public sealed override string DisplayName => _extension.DisplayName;

    /// <inheritdoc />
    public sealed override string Description => _extension.Description;

    /// <inheritdoc />
    public sealed override string Version => _extension.Version;

    /// <inheritdoc />
    public override async Task<bool> IsEnabledAsync() => await _extension.IsEnabledAsync().ConfigureAwait(false);

    /// <inheritdoc />
    public sealed override Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        if (_sessionUid is not null)
        {
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, ExtensionResources.VSTestBridgedTestFrameworkSessionAlreadyCreatedErrorMessage, _sessionUid.Value.Value));
        }

        _sessionUid = context.SessionUid;
        return Task.FromResult(new CreateTestSessionResult { IsSuccess = true });
    }

    /// <inheritdoc />
    public sealed override async Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
    {
        // Clear initial count
        _incomingRequestCounter.Signal();

        // Wait for remaining request processing
        await _incomingRequestCounter.WaitAsync(context.CancellationToken).ConfigureAwait(false);
        _sessionUid = null;
        return new CloseTestSessionResult { IsSuccess = true };
    }

    /// <summary>
    /// The dispose pattern.
    /// </summary>
    /// <param name="disposing">Whether to dispose managed state.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                // Dispose managed state (managed objects)
                _incomingRequestCounter.Dispose();
            }

            // Free unmanaged resources (unmanaged objects) and override finalizer
            // Set large fields to null
            _isDisposed = true;
        }
    }

    /// <summary>
    /// Discover tests asynchronously.
    /// </summary>
    /// <param name="request">The VSTest discovery request.</param>
    /// <param name="messageBus">The message bus.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected sealed override Task DiscoverTestsAsync(VSTestDiscoverTestExecutionRequest request, IMessageBus messageBus,
        CancellationToken cancellationToken)
        => ExecuteRequestWithRequestCountGuardAsync(async () => await SynchronizedDiscoverTestsAsync(request, messageBus, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Discovers tests asynchronously with handling of concurrency.
    /// </summary>
    /// <param name="request">The VSTest discovery request.</param>
    /// <param name="messageBus">The message bus.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected abstract Task SynchronizedDiscoverTestsAsync(VSTestDiscoverTestExecutionRequest request, IMessageBus messageBus,
        CancellationToken cancellationToken);

    /// <summary>
    /// Runs tests asynchronously.
    /// </summary>
    /// <param name="request">The VSTest run request.</param>
    /// <param name="messageBus">The message bus.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected sealed override Task RunTestsAsync(VSTestRunTestExecutionRequest request, IMessageBus messageBus,
        CancellationToken cancellationToken)
        => ExecuteRequestWithRequestCountGuardAsync(async () => await SynchronizedRunTestsAsync(request, messageBus, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Runs tests asynchronously with handling of concurrency.
    /// </summary>
    /// <param name="request">The VSTest run request.</param>
    /// <param name="messageBus">The message bus.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected abstract Task SynchronizedRunTestsAsync(VSTestRunTestExecutionRequest request, IMessageBus messageBus,
        CancellationToken cancellationToken);

    /// <summary>
    /// Executes the request.
    /// </summary>
    /// <param name="request">The test execution request.</param>
    /// <param name="messageBus">The message bus.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="NotSupportedException">Exception is thrown when the request is neither <see cref="DiscoverTestExecutionRequest"/> nor <see cref="RunTestExecutionRequest"/>.</exception>
    protected sealed override Task ExecuteRequestAsync(TestExecutionRequest request, IMessageBus messageBus,
        CancellationToken cancellationToken)
        => ExecuteRequestWithRequestCountGuardAsync(async () =>
        {
#pragma warning disable IL3000 // Avoid accessing Assembly file path when publishing as a single file
            string[] testAssemblyPaths = [.. _getTestAssemblies().Select(x => x.Location)];
#pragma warning restore IL3000 // Avoid accessing Assembly file path when publishing as a single file
            switch (request)
            {
                case DiscoverTestExecutionRequest discoverRequest:
                    VSTestDiscoverTestExecutionRequest vstestDiscoverRequest =
                        VSTestDiscoverTestExecutionRequestFactory.CreateRequest(discoverRequest, this, testAssemblyPaths, cancellationToken);
                    await SynchronizedDiscoverTestsAsync(vstestDiscoverRequest, messageBus, cancellationToken).ConfigureAwait(false);
                    break;

                case RunTestExecutionRequest runRequest:
                    VSTestRunTestExecutionRequest vstestRunRequest =
                        VSTestRunTestExecutionRequestFactory.CreateRequest(runRequest, this, testAssemblyPaths, cancellationToken);
                    await SynchronizedRunTestsAsync(vstestRunRequest, messageBus, cancellationToken).ConfigureAwait(false);
                    break;

                default:
                    throw new NotSupportedException($"VSTest Test Adapters do not support requests of type '{request.GetType()}'.");
            }
        });

    /// <inheritdoc />
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private async Task ExecuteRequestWithRequestCountGuardAsync(Func<Task> asyncFunc)
    {
        _incomingRequestCounter.AddCount();

        try
        {
            await asyncFunc().ConfigureAwait(false);
        }
        finally
        {
            _incomingRequestCounter.Signal();
        }
    }
}
