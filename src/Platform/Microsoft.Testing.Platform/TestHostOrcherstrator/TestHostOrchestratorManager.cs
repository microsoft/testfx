// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;

internal sealed class TestHostOrchestratorManager : ITestHostOrchestratorManager
{
    private readonly List<Func<IServiceProvider, ITestApplicationLifecycleCallbacks>> _testApplicationLifecycleCallbacksFactories = [];
    private List<Func<IServiceProvider, ITestHostOrchestrator>>? _factories;

    public void AddTestHostOrchestrator(Func<IServiceProvider, ITestHostOrchestrator> factory)
    {
        Guard.NotNull(factory);
        _factories ??= [];
        _factories.Add(factory);
    }

    public async Task<TestHostOrchestratorConfiguration> BuildAsync(ServiceProvider serviceProvider)
    {
        if (_factories is null)
        {
            return new TestHostOrchestratorConfiguration([]);
        }

        List<ITestHostOrchestrator> orchestrators = [];
        foreach (Func<IServiceProvider, ITestHostOrchestrator> factory in _factories)
        {
            ITestHostOrchestrator orchestrator = factory(serviceProvider);

            // Check if we have already extensions of the same type with same id registered
            if (orchestrators.Any(x => x.Uid == orchestrator.Uid))
            {
                ITestHostOrchestrator currentRegisteredExtension = orchestrators.Single(x => x.Uid == orchestrator.Uid);
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.ExtensionWithSameUidAlreadyRegisteredErrorMessage, orchestrator.Uid, currentRegisteredExtension.GetType()));
            }

            // We initialize only if enabled
            if (await orchestrator.IsEnabledAsync())
            {
                await orchestrator.TryInitializeAsync();

                // Register the extension for usage
                orchestrators.Add(orchestrator);
            }
        }

        return new TestHostOrchestratorConfiguration([.. orchestrators]);
    }

    public void AddTestApplicationLifecycleCallbacks(Func<IServiceProvider, ITestApplicationLifecycleCallbacks> testApplicationLifecycleCallbacks)
    {
        Guard.NotNull(testApplicationLifecycleCallbacks);
        _testApplicationLifecycleCallbacksFactories.Add(testApplicationLifecycleCallbacks);
    }

    internal async Task<ITestApplicationLifecycleCallbacks[]> BuildTestApplicationLifecycleCallbackAsync(ServiceProvider serviceProvider)
    {
        List<ITestApplicationLifecycleCallbacks> testApplicationLifecycleCallbacks = [];
        foreach (Func<IServiceProvider, ITestApplicationLifecycleCallbacks> testApplicationLifecycleCallbacksFactory in _testApplicationLifecycleCallbacksFactories)
        {
            ITestApplicationLifecycleCallbacks service = testApplicationLifecycleCallbacksFactory(serviceProvider);

            // Check if we have already extensions of the same type with same id registered
            if (testApplicationLifecycleCallbacks.Any(x => x.Uid == service.Uid))
            {
                ITestApplicationLifecycleCallbacks currentRegisteredExtension = testApplicationLifecycleCallbacks.Single(x => x.Uid == service.Uid);
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.ExtensionWithSameUidAlreadyRegisteredErrorMessage, service.Uid, currentRegisteredExtension.GetType()));
            }

            // We initialize only if enabled
            if (await service.IsEnabledAsync())
            {
                await service.TryInitializeAsync();

                // Register the extension for usage
                testApplicationLifecycleCallbacks.Add(service);
            }
        }

        return [.. testApplicationLifecycleCallbacks];
    }
}
