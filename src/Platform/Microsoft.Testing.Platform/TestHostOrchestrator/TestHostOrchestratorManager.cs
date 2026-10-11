// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.TestHostOrchestrator;

internal class TestHostOrchestratorManager :
    ITestHostOrchestratorManager,
    Extensions.TestHostOrchestrator.ITestHostOrchestratorManager,
    ITestHostExecutionOrchestratorMiddlewareManager
{
    private readonly List<Func<IServiceProvider, ITestHostOrchestratorApplicationLifetime>> _testHostOrchestratorApplicationLifetimeFactories = [];
    private List<Func<IServiceProvider, ITestHostExecutionOrchestrator>>? _factories;
    private List<Func<IServiceProvider, ITestHostExecutionOrchestratorMiddleware>>? _middlewareFactories;

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

    public void AddTestHostExecutionOrchestratorMiddleware(Func<IServiceProvider, ITestHostExecutionOrchestratorMiddleware> factory)
    {
        _ = factory ?? throw new ArgumentNullException(nameof(factory));
        _middlewareFactories ??= [];
        _middlewareFactories.Add(factory);
    }

    internal async Task<TestHostOrchestratorConfiguration> BuildAsync(ServiceProvider serviceProvider)
    {
        List<ITestHostExecutionOrchestrator> orchestrators = [];
        if (_factories is not null)
        {
            await ExtensionBuilderHelper.BuildAndRegisterExtensionsAsync(_factories, serviceProvider, orchestrators).ConfigureAwait(false);
        }

        List<ITestHostExecutionOrchestratorMiddleware> middleware = [];
        if (_middlewareFactories is not null)
        {
            // Preserves registration order: BuildAndRegisterExtensionsAsync appends in the order the
            // factories were added, which is the documented outermost-first composition order.
            await ExtensionBuilderHelper.BuildAndRegisterExtensionsAsync(_middlewareFactories, serviceProvider, middleware).ConfigureAwait(false);
        }

        return new TestHostOrchestratorConfiguration([.. orchestrators], [.. middleware]);
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
