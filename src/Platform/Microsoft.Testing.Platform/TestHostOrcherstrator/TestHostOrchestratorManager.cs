// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;

internal sealed class TestHostOrchestratorManager : ITestHostOrchestratorManager
{
    private readonly List<Func<IServiceProvider, ITestHostOrchestratorApplicationLifetime>> _testHostOrchestratorApplicationLifetimeFactories = [];
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
            ITestHostOrchestrator[] duplicates = orchestrators.Where(x => x.Uid == orchestrator.Uid).ToArray();
            if (duplicates.Length > 0)
            {
                var allDuplicates = duplicates.Concat([orchestrator]).ToArray();
                string typesList = string.Join(", ", allDuplicates.Select(x => $"'{x.GetType()}'"));
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.ExtensionWithSameUidAlreadyRegisteredErrorMessage, orchestrator.Uid, typesList));
            }

            // We initialize only if enabled
            if (await orchestrator.IsEnabledAsync().ConfigureAwait(false))
            {
                await orchestrator.TryInitializeAsync().ConfigureAwait(false);

                // Register the extension for usage
                orchestrators.Add(orchestrator);
            }
        }

        return new TestHostOrchestratorConfiguration([.. orchestrators]);
    }

    public void AddTestHostOrchestratorApplicationLifetime(Func<IServiceProvider, ITestHostOrchestratorApplicationLifetime> testHostOrchestratorApplicationLifetimeFactory)
    {
        Guard.NotNull(testHostOrchestratorApplicationLifetimeFactory);
        _testHostOrchestratorApplicationLifetimeFactories.Add(testHostOrchestratorApplicationLifetimeFactory);
    }

    internal async Task<ITestHostOrchestratorApplicationLifetime[]> BuildTestHostOrchestratorApplicationLifetimesAsync(ServiceProvider serviceProvider)
    {
        List<ITestHostOrchestratorApplicationLifetime> lifetimes = [];
        foreach (Func<IServiceProvider, ITestHostOrchestratorApplicationLifetime> testHostOrchestratorApplicationLifetimeFactory in _testHostOrchestratorApplicationLifetimeFactories)
        {
            ITestHostOrchestratorApplicationLifetime service = testHostOrchestratorApplicationLifetimeFactory(serviceProvider);

            // Check if we have already extensions of the same type with same id registered
            ITestHostOrchestratorApplicationLifetime[] duplicates = lifetimes.Where(x => x.Uid == service.Uid).ToArray();
            if (duplicates.Length > 0)
            {
                var allDuplicates = duplicates.Concat([service]).ToArray();
                string typesList = string.Join(", ", allDuplicates.Select(x => $"'{x.GetType()}'"));
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.ExtensionWithSameUidAlreadyRegisteredErrorMessage, service.Uid, typesList));
            }

            // We initialize only if enabled
            if (await service.IsEnabledAsync().ConfigureAwait(false))
            {
                await service.TryInitializeAsync().ConfigureAwait(false);

                // Register the extension for usage
                lifetimes.Add(service);
            }
        }

        return [.. lifetimes];
    }
}
