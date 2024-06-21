// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;

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

public abstract class SynchronizedSingleSessionVSTestBridgedTestFramework : VSTestBridgedTestFrameworkBase, IDisposable
{
    private readonly IExtension _extension;
    private readonly Func<IEnumerable<Assembly>> _getTestAssemblies;
    private readonly CountdownEvent _incomingRequestCounter = new(1);
    private bool _isDisposed;
    private SessionUid? _sessionUid;

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
    public override async Task<bool> IsEnabledAsync() => await _extension.IsEnabledAsync();

    /// <inheritdoc />
    public sealed override Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        if (_sessionUid is not null)
        {
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, ExtensionResources.VSTestBridgedTestFrameworkSessionAlreadyCreatedErrorMessage, _sessionUid.Value.Value));
        }

        _sessionUid = context.SessionUid;
        return Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });
    }

    /// <inheritdoc />
    public sealed override async Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
    {
        // Clear initial count
        _incomingRequestCounter.Signal();

        // Wait for remaining request processing
        await _incomingRequestCounter.WaitAsync(context.CancellationToken);
        _sessionUid = null;
        return new CloseTestSessionResult() { IsSuccess = true };
    }

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

    protected sealed override Task DiscoverTestsAsync(VSTestDiscoverTestExecutionRequest request, IMessageBus messageBus,
        CancellationToken cancellationToken)
        => ExecuteRequestWithRequestCountGuardAsync(async () => await SynchronizedDiscoverTestsAsync(request, messageBus, cancellationToken));

    protected abstract Task SynchronizedDiscoverTestsAsync(VSTestDiscoverTestExecutionRequest request, IMessageBus messageBus,
        CancellationToken cancellationToken);

    protected sealed override Task RunTestsAsync(VSTestRunTestExecutionRequest request, IMessageBus messageBus,
        CancellationToken cancellationToken)
        => ExecuteRequestWithRequestCountGuardAsync(async () => await SynchronizedRunTestsAsync(request, messageBus, cancellationToken));

    protected abstract Task SynchronizedRunTestsAsync(VSTestRunTestExecutionRequest request, IMessageBus messageBus,
        CancellationToken cancellationToken);

    protected sealed override Task ExecuteRequestAsync(TestExecutionRequest request, IMessageBus messageBus,
        CancellationToken cancellationToken)
        => ExecuteRequestWithRequestCountGuardAsync(async () =>
        {
#pragma warning disable IL3000 // Avoid accessing Assembly file path when publishing as a single file
            string[] testAssemblyPaths = _getTestAssemblies().Select(x => x.Location).ToArray();
#pragma warning restore IL3000 // Avoid accessing Assembly file path when publishing as a single file
            switch (request)
            {
                case DiscoverTestExecutionRequest discoverRequest:
                    VSTestDiscoverTestExecutionRequest vstestDiscoverRequest =
                        VSTestDiscoverTestExecutionRequestFactory.CreateRequest(discoverRequest, this, testAssemblyPaths, cancellationToken);
                    await SynchronizedDiscoverTestsAsync(vstestDiscoverRequest, messageBus, cancellationToken);
                    break;

                case RunTestExecutionRequest runRequest:
                    VSTestRunTestExecutionRequest vstestRunRequest =
                        VSTestRunTestExecutionRequestFactory.CreateRequest(runRequest, this, testAssemblyPaths, cancellationToken);
                    await SynchronizedRunTestsAsync(vstestRunRequest, messageBus, cancellationToken);
                    break;

                default:
                    throw new NotSupportedException($"VSTest Test Adapters do not support requests of type '{request.GetType()}'.");
            }
        });

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
            await asyncFunc();
        }
        finally
        {
            _incomingRequestCounter.Signal();
        }
    }
}
