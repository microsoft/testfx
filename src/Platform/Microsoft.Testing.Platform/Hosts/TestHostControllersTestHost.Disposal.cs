// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class TestHostControllersTestHost
{
    private async Task DisposeServicesAsync(List<object> alreadyDisposed)
    {
        // A CompositeExtensionFactory builds one object that is reused for every role it was registered
        // under, so the lifetime handlers and environment-variable providers disposed below can be the very
        // same instances that the message bus holds as IDataConsumer. Because we dispose them here, before
        // DisposeServiceProviderAsync gets a chance to close the handshake, we have to disable the bus first
        // or a consumer can be disposed while its ConsumeAsync is still running. The in-run disable is not
        // enough: it is gated on there being lifetime handlers, and it is skipped entirely on every early-out
        // path (an invalid platform setup, or any failure between building the bus and reaching it).
        object[] consumersStillRunning = [];
        if (ServiceProvider.GetService<BaseMessageBus>() is { } messageBus)
        {
            if (!await TryRunControllerCleanupAsync(
                () => EnsureMessageBusDisabledAsync(messageBus, ServiceProvider)).ConfigureAwait(false))
            {
                AbandonRemainingServices(alreadyDisposed);
                return;
            }

            // Disabling is bounded on an aborted run, so it can return while a consumer that ignores the
            // cancellation token is still inside ConsumeAsync. Those instances must be skipped here too, for
            // exactly the reason above: a multi-role extension is reached by these manual loops.
            consumersStillRunning = [.. messageBus.ConsumersStillRunning];
        }

        ITestHostEnvironmentVariableProvider[] variableProviders = _testHostsInformation.EnvironmentVariableProviders;
        ITestHostProcessLifetimeHandler[] lifetimeHandlers = _testHostsInformation.LifetimeHandlers;

        // Recording them as already disposed is what keeps them from being disposed by the service-provider
        // walk below either.
        alreadyDisposed.AddRange(consumersStillRunning);
        // A handler that ignored a bounded start or exit callback token may still be running. Do not invoke
        // another lifecycle callback or dispose that same instance underneath its abandoned callback.
        alreadyDisposed.AddRange(_servicesStillRunning);

        foreach (ITestHostProcessLifetimeHandler service in lifetimeHandlers)
        {
            if (alreadyDisposed.Contains(service))
            {
                continue;
            }

            if (!await TryRunControllerCleanupAsync(() => DisposeHelper.DisposeAsync(service)).ConfigureAwait(false))
            {
                AbandonRemainingServices(alreadyDisposed);
                return;
            }

            alreadyDisposed.Add(service);
        }

        foreach (ITestHostEnvironmentVariableProvider service in variableProviders)
        {
            if (alreadyDisposed.Contains(service))
            {
                continue;
            }

            if (!await TryRunControllerCleanupAsync(() => DisposeHelper.DisposeAsync(service)).ConfigureAwait(false))
            {
                AbandonRemainingServices(alreadyDisposed);
                return;
            }

            alreadyDisposed.Add(service);
        }

        if (!await TryRunControllerCleanupAsync(
            () => DisposeServiceProviderAsync(ServiceProvider, alreadyDisposed: alreadyDisposed)).ConfigureAwait(false))
        {
            AbandonRemainingServices(alreadyDisposed);
        }
    }

    private async Task<bool> TryRunControllerCleanupAsync(Func<Task> cleanup)
    {
        CancellationToken cancellationToken = _controllerFinalizationCancellationTokenSource?.Token ?? CancellationToken.None;
        return !cancellationToken.IsCancellationRequested
            && await TryRunControllerExtensionAsync(_ => cleanup(), cancellationToken).ConfigureAwait(false);
    }

    private void AbandonRemainingServices(List<object> alreadyDisposed)
    {
        _controllerFinalizationTimedOut = true;
        foreach (object service in ServiceProvider.Services)
        {
            if (!alreadyDisposed.Contains(service))
            {
                alreadyDisposed.Add(service);
            }

            if (service is BaseMessageBus messageBus)
            {
                foreach (IDataConsumer dataConsumer in messageBus.DataConsumerServices.Where(
                    dataConsumer => !alreadyDisposed.Contains(dataConsumer)))
                {
                    alreadyDisposed.Add(dataConsumer);
                }
            }
        }
    }

    protected override async Task DisposeProcessShutdownServiceAsync(object service)
    {
        if (!await TryRunControllerCleanupAsync(() => DisposeHelper.DisposeAsync(service)).ConfigureAwait(false))
        {
            _controllerFinalizationTimedOut = true;
            ScheduleFinalizationTimeoutWarning();
        }
    }

    public void Dispose()
    {
        _controllerFinalizationTransitionRegistration.Dispose();
        if (!_controllerFinalizationTimedOut)
        {
            _controllerFinalizationCancellationTokenSource?.Dispose();
        }

        _waitForPid.Dispose();
    }
}
