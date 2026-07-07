// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.TestHost;

internal sealed class TestHostManager : ITestHostManager
{
    // Registration ordering
    private readonly List<object> _factoryOrdering = [];

    // Exposed extension points
    private readonly List<Func<IServiceProvider, ITestHostApplicationLifetime>> _testApplicationLifecycleCallbacksFactories = [];
    private readonly List<Func<IServiceProvider, IDataConsumer>> _dataConsumerFactories = [];
    private readonly List<Func<IServiceProvider, ITestSessionLifetimeHandler>> _testSessionLifetimeHandlerFactories = [];
    private readonly List<ICompositeExtensionFactory> _dataConsumersCompositeServiceFactories = [];
    private readonly List<ICompositeExtensionFactory> _testSessionLifetimeHandlerCompositeFactories = [];

    // Non-exposed extension points
    private Func<IServiceProvider, ITestExecutionFilterFactory>? _testExecutionFilterFactory;
    private Func<IServiceProvider, ITestFrameworkInvoker>? _testFrameworkInvokerFactory;

    public void AddTestFrameworkInvoker(Func<IServiceProvider, ITestFrameworkInvoker> testFrameworkInvokerFactory)
    {
        if (_testFrameworkInvokerFactory is not null)
        {
            throw new InvalidOperationException(PlatformResources.TestAdapterInvokerFactoryAlreadySetErrorMessage);
        }

        _testFrameworkInvokerFactory = testFrameworkInvokerFactory ?? throw new ArgumentNullException(nameof(testFrameworkInvokerFactory));
    }

    internal async Task<ActionResult<ITestFrameworkInvoker>> TryBuildTestAdapterInvokerAsync(ServiceProvider serviceProvider)
    {
        if (_testFrameworkInvokerFactory is null)
        {
            return ActionResult.Fail<ITestFrameworkInvoker>();
        }

        ITestFrameworkInvoker testAdapterInvoke = _testFrameworkInvokerFactory(serviceProvider);

        // We initialize only if enabled
        if (await testAdapterInvoke.IsEnabledAsync().ConfigureAwait(false))
        {
            await testAdapterInvoke.TryInitializeAsync().ConfigureAwait(false);

            return ActionResult.Ok(testAdapterInvoke);
        }

        return ActionResult.Fail<ITestFrameworkInvoker>();
    }

    public void AddTestExecutionFilterFactory(Func<IServiceProvider, ITestExecutionFilterFactory> testExecutionFilterFactory)
    {
        if (_testExecutionFilterFactory is not null)
        {
            throw new InvalidOperationException(PlatformResources.TEstExecutionFilterFactoryFactoryAlreadySetErrorMessage);
        }

        _testExecutionFilterFactory = testExecutionFilterFactory ?? throw new ArgumentNullException(nameof(testExecutionFilterFactory));
    }

    internal async Task<ActionResult<ITestExecutionFilterFactory>> TryBuildTestExecutionFilterFactoryAsync(ServiceProvider serviceProvider)
    {
        if (_testExecutionFilterFactory is null)
        {
            return ActionResult.Fail<ITestExecutionFilterFactory>();
        }

        ITestExecutionFilterFactory testExecutionFilterFactory = _testExecutionFilterFactory(serviceProvider);

        // We initialize only if enabled
        if (await testExecutionFilterFactory.IsEnabledAsync().ConfigureAwait(false))
        {
            await testExecutionFilterFactory.TryInitializeAsync().ConfigureAwait(false);

            return ActionResult.Ok(testExecutionFilterFactory);
        }

        return ActionResult.Fail<ITestExecutionFilterFactory>();
    }

    public void AddTestHostApplicationLifetime(Func<IServiceProvider, ITestHostApplicationLifetime> testHostApplicationLifetime)
        => _testApplicationLifecycleCallbacksFactories.Add(testHostApplicationLifetime ?? throw new ArgumentNullException(nameof(testHostApplicationLifetime)));

    internal async Task<ITestHostApplicationLifetime[]> BuildTestApplicationLifecycleCallbackAsync(ServiceProvider serviceProvider)
    {
        List<ITestHostApplicationLifetime> testApplicationLifecycleCallbacks = [];
        await ExtensionBuilderHelper.BuildAndRegisterExtensionsAsync(_testApplicationLifecycleCallbacksFactories, serviceProvider, testApplicationLifecycleCallbacks).ConfigureAwait(false);

        return [.. testApplicationLifecycleCallbacks];
    }

    public void AddDataConsumer(Func<IServiceProvider, IDataConsumer> dataConsumerFactory)
    {
        _dataConsumerFactories.Add(dataConsumerFactory ?? throw new ArgumentNullException(nameof(dataConsumerFactory)));
        _factoryOrdering.Add(dataConsumerFactory);
    }

    public void AddDataConsumer<T>(CompositeExtensionFactory<T> compositeServiceFactory)
        where T : class, IDataConsumer
    {
        if (_dataConsumersCompositeServiceFactories.Contains(compositeServiceFactory ?? throw new ArgumentNullException(nameof(compositeServiceFactory))))
        {
            throw new ArgumentException(PlatformResources.CompositeServiceFactoryInstanceAlreadyRegistered);
        }

        _dataConsumersCompositeServiceFactories.Add(compositeServiceFactory);
        _factoryOrdering.Add(compositeServiceFactory);
    }

    internal async Task<(IExtension Consumer, int RegistrationOrder)[]> BuildDataConsumersAsync(ServiceProvider serviceProvider, List<ICompositeExtensionFactory> alreadyBuiltServices)
    {
        List<(IExtension Consumer, int RegistrationOrder)> dataConsumers = [];
        await ExtensionBuilderHelper.BuildAndRegisterExtensionsAsync(_dataConsumerFactories, serviceProvider, dataConsumers, _factoryOrdering).ConfigureAwait(false);
        await ExtensionBuilderHelper.BuildAndRegisterCompositeExtensionsAsync<IDataConsumer>(_dataConsumersCompositeServiceFactories, serviceProvider, dataConsumers, alreadyBuiltServices, _factoryOrdering).ConfigureAwait(false);

        return [.. dataConsumers];
    }

    [Obsolete("Use AddTestSessionLifetimeHandler instead.", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void AddTestSessionLifetimeHandle(Func<IServiceProvider, ITestSessionLifetimeHandler> testSessionLifetimeHandleFactory)
        => AddTestSessionLifetimeHandler(testSessionLifetimeHandleFactory);

    [Obsolete("Use AddTestSessionLifetimeHandler instead.", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void AddTestSessionLifetimeHandle<T>(CompositeExtensionFactory<T> compositeServiceFactory)
        where T : class, ITestSessionLifetimeHandler
        => AddTestSessionLifetimeHandler(compositeServiceFactory);

    public void AddTestSessionLifetimeHandler(Func<IServiceProvider, ITestSessionLifetimeHandler> testSessionLifetimeHandleFactory)
    {
        _testSessionLifetimeHandlerFactories.Add(testSessionLifetimeHandleFactory ?? throw new ArgumentNullException(nameof(testSessionLifetimeHandleFactory)));
        _factoryOrdering.Add(testSessionLifetimeHandleFactory);
    }

    public void AddTestSessionLifetimeHandler<T>(CompositeExtensionFactory<T> compositeServiceFactory)
        where T : class, ITestSessionLifetimeHandler
    {
        if (_testSessionLifetimeHandlerCompositeFactories.Contains(compositeServiceFactory ?? throw new ArgumentNullException(nameof(compositeServiceFactory))))
        {
            throw new ArgumentException(PlatformResources.CompositeServiceFactoryInstanceAlreadyRegistered);
        }

        _testSessionLifetimeHandlerCompositeFactories.Add(compositeServiceFactory);
        _factoryOrdering.Add(compositeServiceFactory);
    }

    internal async Task<(IExtension TestSessionLifetimeHandler, int RegistrationOrder)[]> BuildTestSessionLifetimeHandleAsync(ServiceProvider serviceProvider, List<ICompositeExtensionFactory> alreadyBuiltServices)
    {
        List<(IExtension TestSessionLifetimeHandler, int RegistrationOrder)> testSessionLifetimeHandlers = [];
        await ExtensionBuilderHelper.BuildAndRegisterExtensionsAsync(_testSessionLifetimeHandlerFactories, serviceProvider, testSessionLifetimeHandlers, _factoryOrdering).ConfigureAwait(false);
        await ExtensionBuilderHelper.BuildAndRegisterCompositeExtensionsAsync<ITestSessionLifetimeHandler>(_testSessionLifetimeHandlerCompositeFactories, serviceProvider, testSessionLifetimeHandlers, alreadyBuiltServices, _factoryOrdering).ConfigureAwait(false);

        return [.. testSessionLifetimeHandlers];
    }
}
