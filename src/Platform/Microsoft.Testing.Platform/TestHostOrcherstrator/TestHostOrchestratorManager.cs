// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;

internal class TestHostOrchestratorManager : ITestHostOrchestratorManager
{
    private List<Func<IServiceProvider, ITestHostOrchestrator>>? _factories;

    public void AddTestHostOrchestrator(Func<IServiceProvider, ITestHostOrchestrator> factory)
    {
        ArgumentGuard.IsNotNull(factory);
        _factories ??= [];
        _factories.Add(factory);
    }

    public async Task<TestHostOrchestratorConfiguration> BuildAsync(ServiceProvider serviceProvider)
    {
        if (_factories is null || _factories.Count == 0)
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
                if (orchestrator is IAsyncInitializableExtension async)
                {
                    await async.InitializeAsync();
                }

                // Register the extension for usage
                orchestrators.Add(orchestrator);
            }
        }

        return new TestHostOrchestratorConfiguration(orchestrators.ToArray());
    }
}
