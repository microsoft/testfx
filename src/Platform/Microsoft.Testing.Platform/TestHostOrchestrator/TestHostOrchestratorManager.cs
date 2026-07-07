// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.TestHostOrchestrator;

internal class TestHostOrchestratorManager : ITestHostOrchestratorManager, Extensions.TestHostOrchestrator.ITestHostOrchestratorManager
{
    private readonly List<Func<IServiceProvider, ITestHostOrchestratorApplicationLifetime>> _testHostOrchestratorApplicationLifetimeFactories = [];
    private List<Func<IServiceProvider, ITestHostExecutionOrchestrator>>? _factories;

    public void AddTestHostOrchestrator(Func<IServiceProvider, ITestHostExecutionOrchestrator> factory)
    {
        _ = factory ?? throw new ArgumentNullException(nameof(factory));
        _factories ??= [];
        _factories.Add(factory);
    }

    void Extensions.TestHostOrchestrator.ITestHostOrchestratorManager.AddTestHostOrchestrator(Func<IServiceProvider, Extensions.TestHostOrchestrator.ITestHostOrchestrator> factory)
    {
        _ = factory ?? throw new ArgumentNullException(nameof(factory));
        _factories ??= [];
        _factories.Add(sp => factory(sp));
    }

    void Extensions.TestHostOrchestrator.ITestHostOrchestratorManager.AddTestHostOrchestratorApplicationLifetime(Func<IServiceProvider, ITestHostOrchestratorApplicationLifetime> testHostOrchestratorApplicationLifetimeFactory)
        => AddTestHostOrchestratorApplicationLifetime(testHostOrchestratorApplicationLifetimeFactory);

    internal async Task<TestHostOrchestratorConfiguration> BuildAsync(ServiceProvider serviceProvider)
    {
        if (_factories is null)
        {
            return new TestHostOrchestratorConfiguration([]);
        }

        List<ITestHostExecutionOrchestrator> orchestrators = [];
        await ExtensionBuilderHelper.BuildAndRegisterExtensionsAsync(_factories, serviceProvider, orchestrators).ConfigureAwait(false);

        return new TestHostOrchestratorConfiguration([.. orchestrators]);
    }

    public void AddTestHostOrchestratorApplicationLifetime(Func<IServiceProvider, ITestHostOrchestratorApplicationLifetime> testHostOrchestratorApplicationLifetimeFactory)
    {
        _ = testHostOrchestratorApplicationLifetimeFactory ?? throw new ArgumentNullException(nameof(testHostOrchestratorApplicationLifetimeFactory));
        _testHostOrchestratorApplicationLifetimeFactories.Add(testHostOrchestratorApplicationLifetimeFactory);
    }

    internal async Task<ITestHostOrchestratorApplicationLifetime[]> BuildTestHostOrchestratorApplicationLifetimesAsync(ServiceProvider serviceProvider)
    {
        List<ITestHostOrchestratorApplicationLifetime> lifetimes = [];
        await ExtensionBuilderHelper.BuildAndRegisterExtensionsAsync(_testHostOrchestratorApplicationLifetimeFactories, serviceProvider, lifetimes).ConfigureAwait(false);

        return [.. lifetimes];
    }
}
