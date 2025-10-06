// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    private readonly List<Func<IServiceProvider, ITestExecutionFilterFactory>> _testExecutionFilterFactories = [];
    private readonly List<Func<IServiceProvider, IRequestFilterProvider>> _requestFilterProviders = [];
    private Func<IServiceProvider, ITestFrameworkInvoker>? _testFrameworkInvokerFactory;

    public void AddTestFrameworkInvoker(Func<IServiceProvider, ITestFrameworkInvoker> testFrameworkInvokerFactory)
    {
        Guard.NotNull(testFrameworkInvokerFactory);
        if (_testFrameworkInvokerFactory is not null)
        {
            throw new InvalidOperationException(PlatformResources.TestAdapterInvokerFactoryAlreadySetErrorMessage);
        }

        _testFrameworkInvokerFactory = testFrameworkInvokerFactory;
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
        Guard.NotNull(testExecutionFilterFactory);
        _testExecutionFilterFactories.Add(testExecutionFilterFactory);
    }

    internal bool HasFilterFactories() => _testExecutionFilterFactories.Count > 0;

    internal async Task<ActionResult<ITestExecutionFilterFactory>> TryBuildTestExecutionFilterFactoryAsync(ServiceProvider serviceProvider)
    {
        List<ITestExecutionFilter> filters = [];
        foreach (Func<IServiceProvider, ITestExecutionFilterFactory> factory in _testExecutionFilterFactories)
        {
            ITestExecutionFilterFactory filterFactory = factory(serviceProvider);

            if (await filterFactory.IsEnabledAsync().ConfigureAwait(false))
            {
                await filterFactory.TryInitializeAsync().ConfigureAwait(false);

                (bool success, ITestExecutionFilter? filter) = await filterFactory.TryCreateAsync().ConfigureAwait(false);
                if (success && filter is not null)
                {
                    filters.Add(filter);
                }
            }
        }

        if (filters.Count == 0)
        {
            return ActionResult.Ok<ITestExecutionFilterFactory>(new SingleFilterFactory(new NopFilter()));
        }

        if (filters.Count > 1)
        {
            ITestExecutionFilter aggregateFilter = new AggregateTestExecutionFilter(filters);
            return ActionResult.Ok<ITestExecutionFilterFactory>(new SingleFilterFactory(aggregateFilter));
        }

        return ActionResult.Ok<ITestExecutionFilterFactory>(new SingleFilterFactory(filters[0]));
    }

    /// <summary>
    /// Internal factory that wraps a single pre-built filter.
    /// </summary>
    private sealed class SingleFilterFactory : ITestExecutionFilterFactory
    {
        private readonly ITestExecutionFilter _filter;

        public SingleFilterFactory(ITestExecutionFilter filter)
        {
            _filter = filter;
        }

        public string Uid => nameof(SingleFilterFactory);

        public string Version => AppVersion.DefaultSemVer;

        public string DisplayName => "Single Filter Factory";

        public string Description => "Factory that wraps a pre-built filter";

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task<(bool Success, ITestExecutionFilter? TestExecutionFilter)> TryCreateAsync()
            => Task.FromResult((true, (ITestExecutionFilter?)_filter));
    }

    public void AddRequestFilterProvider(Func<IServiceProvider, IRequestFilterProvider> requestFilterProvider)
    {
        Guard.NotNull(requestFilterProvider);
        _requestFilterProviders.Add(requestFilterProvider);
    }

    internal bool HasRequestFilterProviders() => _requestFilterProviders.Count > 0;

    internal async Task<ITestExecutionFilter> ResolveRequestFilterAsync(ServerMode.RequestArgsBase args, ServiceProvider serviceProvider)
    {
        serviceProvider.AddService(new TestExecutionRequestContext(args));

        foreach (Func<IServiceProvider, IRequestFilterProvider> providerFactory in _requestFilterProviders)
        {
            IRequestFilterProvider provider = providerFactory(serviceProvider);

            if (await provider.IsEnabledAsync().ConfigureAwait(false) && provider.CanHandle(serviceProvider))
            {
                return await provider.CreateFilterAsync(serviceProvider).ConfigureAwait(false);
            }
        }

        return new NopFilter();
    }

    public void AddTestHostApplicationLifetime(Func<IServiceProvider, ITestHostApplicationLifetime> testHostApplicationLifetime)
    {
        Guard.NotNull(testHostApplicationLifetime);
        _testApplicationLifecycleCallbacksFactories.Add(testHostApplicationLifetime);
    }

    internal async Task<ITestHostApplicationLifetime[]> BuildTestApplicationLifecycleCallbackAsync(ServiceProvider serviceProvider)
    {
        List<ITestHostApplicationLifetime> testApplicationLifecycleCallbacks = [];
        foreach (Func<IServiceProvider, ITestHostApplicationLifetime> testApplicationLifecycleCallbacksFactory in _testApplicationLifecycleCallbacksFactories)
        {
            ITestHostApplicationLifetime service = testApplicationLifecycleCallbacksFactory(serviceProvider);

            // Check if we have already extensions of the same type with same id registered
            testApplicationLifecycleCallbacks.ValidateUniqueExtension(service);

            // We initialize only if enabled
            if (await service.IsEnabledAsync().ConfigureAwait(false))
            {
                await service.TryInitializeAsync().ConfigureAwait(false);

                // Register the extension for usage
                testApplicationLifecycleCallbacks.Add(service);
            }
        }

        return [.. testApplicationLifecycleCallbacks];
    }

    public void AddDataConsumer(Func<IServiceProvider, IDataConsumer> dataConsumerFactory)
    {
        Guard.NotNull(dataConsumerFactory);
        _dataConsumerFactories.Add(dataConsumerFactory);
        _factoryOrdering.Add(dataConsumerFactory);
    }

    public void AddDataConsumer<T>(CompositeExtensionFactory<T> compositeServiceFactory)
        where T : class, IDataConsumer
    {
        Guard.NotNull(compositeServiceFactory);
        if (_dataConsumersCompositeServiceFactories.Contains(compositeServiceFactory))
        {
            throw new ArgumentException(PlatformResources.CompositeServiceFactoryInstanceAlreadyRegistered);
        }

        _dataConsumersCompositeServiceFactories.Add(compositeServiceFactory);
        _factoryOrdering.Add(compositeServiceFactory);
    }

    internal async Task<(IExtension Consumer, int RegistrationOrder)[]> BuildDataConsumersAsync(ServiceProvider serviceProvider, List<ICompositeExtensionFactory> alreadyBuiltServices)
    {
        List<(IExtension Consumer, int RegistrationOrder)> dataConsumers = [];
        foreach (Func<IServiceProvider, IDataConsumer> dataConsumerFactory in _dataConsumerFactories)
        {
            IDataConsumer service = dataConsumerFactory(serviceProvider);

            // Check if we have already extensions of the same type with same id registered
            dataConsumers.ValidateUniqueExtension(service, x => x.Consumer);

            // We initialize only if enabled
            if (await service.IsEnabledAsync().ConfigureAwait(false))
            {
                await service.TryInitializeAsync().ConfigureAwait(false);

                // Register the extension for usage
                dataConsumers.Add((service, _factoryOrdering.IndexOf(dataConsumerFactory)));
            }
        }

        foreach (ICompositeExtensionFactory compositeServiceFactory in _dataConsumersCompositeServiceFactories)
        {
            ICompositeExtensionFactory? compositeFactoryInstance;

            // We check if the same service is already built in some other build phase
            if ((compositeFactoryInstance = alreadyBuiltServices.SingleOrDefault(x => x.GetType() == compositeServiceFactory.GetType())) is null)
            {
                // We clone the instance because we want to have fresh instance per BuildTestApplicationLifecycleCallbackAsync call
                compositeFactoryInstance = (ICompositeExtensionFactory)compositeServiceFactory.Clone();

                // Create the new fresh instance
                var instance = (IExtension)compositeFactoryInstance.GetInstance(serviceProvider);

                // Check if we have already extensions of the same type with same id registered
                dataConsumers.ValidateUniqueExtension(instance, x => x.Consumer);

                // We initialize only if enabled
                if (await instance.IsEnabledAsync().ConfigureAwait(false))
                {
                    await instance.TryInitializeAsync().ConfigureAwait(false);
                }

                // Add to the list of shared singletons
                alreadyBuiltServices.Add(compositeFactoryInstance);
            }

            // Get the singleton
            var extension = (IExtension)compositeFactoryInstance.GetInstance();

            // We register the extension only if enabled
            if (await extension.IsEnabledAsync().ConfigureAwait(false))
            {
                if (extension is IDataConsumer consumer)
                {
                    // Register the extension for usage
                    dataConsumers.Add((consumer, _factoryOrdering.IndexOf(compositeServiceFactory)));
                    continue;
                }

                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.ExtensionDoesNotImplementGivenInterfaceErrorMessage, extension.GetType(), typeof(IDataConsumer)));
            }
        }

        return [.. dataConsumers];
    }

    public void AddTestSessionLifetimeHandle(Func<IServiceProvider, ITestSessionLifetimeHandler> testSessionLifetimeHandleFactory)
    {
        Guard.NotNull(testSessionLifetimeHandleFactory);
        _testSessionLifetimeHandlerFactories.Add(testSessionLifetimeHandleFactory);
        _factoryOrdering.Add(testSessionLifetimeHandleFactory);
    }

    public void AddTestSessionLifetimeHandle<T>(CompositeExtensionFactory<T> compositeServiceFactory)
        where T : class, ITestSessionLifetimeHandler
    {
        Guard.NotNull(compositeServiceFactory);
        if (_testSessionLifetimeHandlerCompositeFactories.Contains(compositeServiceFactory))
        {
            throw new ArgumentException(PlatformResources.CompositeServiceFactoryInstanceAlreadyRegistered);
        }

        _testSessionLifetimeHandlerCompositeFactories.Add(compositeServiceFactory);
        _factoryOrdering.Add(compositeServiceFactory);
    }

    internal async Task<(IExtension TestSessionLifetimeHandler, int RegistrationOrder)[]> BuildTestSessionLifetimeHandleAsync(ServiceProvider serviceProvider, List<ICompositeExtensionFactory> alreadyBuiltServices)
    {
        List<(IExtension TestSessionLifetimeHandler, int RegistrationOrder)> testSessionLifetimeHandlers = [];
        foreach (Func<IServiceProvider, ITestSessionLifetimeHandler> testSessionLifetimeHandlerFactory in _testSessionLifetimeHandlerFactories)
        {
            ITestSessionLifetimeHandler service = testSessionLifetimeHandlerFactory(serviceProvider);

            // Check if we have already extensions of the same type with same id registered
            testSessionLifetimeHandlers.ValidateUniqueExtension(service, x => x.TestSessionLifetimeHandler);

            // We initialize only if enabled
            if (await service.IsEnabledAsync().ConfigureAwait(false))
            {
                await service.TryInitializeAsync().ConfigureAwait(false);

                // Register the extension for usage
                testSessionLifetimeHandlers.Add((service, _factoryOrdering.IndexOf(testSessionLifetimeHandlerFactory)));
            }
        }

        foreach (ICompositeExtensionFactory compositeServiceFactory in _testSessionLifetimeHandlerCompositeFactories)
        {
            ICompositeExtensionFactory? compositeFactoryInstance;

            // We check if the same service is already built in some other build phase
            if ((compositeFactoryInstance = alreadyBuiltServices.SingleOrDefault(x => x.GetType() == compositeServiceFactory.GetType())) is null)
            {
                // We clone the instance because we want to have fresh instance per BuildTestApplicationLifecycleCallbackAsync call
                compositeFactoryInstance = (ICompositeExtensionFactory)compositeServiceFactory.Clone();

                // Create the new fresh instance
                var instance = (IExtension)compositeFactoryInstance.GetInstance(serviceProvider);

                // Check if we have already extensions of the same type with same id registered
                testSessionLifetimeHandlers.ValidateUniqueExtension(instance, x => x.TestSessionLifetimeHandler);

                // We initialize only if enabled
                if (await instance.IsEnabledAsync().ConfigureAwait(false))
                {
                    await instance.TryInitializeAsync().ConfigureAwait(false);
                }

                // Add to the list of shared singletons
                alreadyBuiltServices.Add(compositeFactoryInstance);
            }

            // Get the singleton
            var extension = (IExtension)compositeFactoryInstance.GetInstance();

            // We register the extension only if enabled
            if (await extension.IsEnabledAsync().ConfigureAwait(false))
            {
                if (extension is ITestSessionLifetimeHandler testSessionLifetimeHandler)
                {
                    // Register the extension for usage
                    testSessionLifetimeHandlers.Add((testSessionLifetimeHandler, _factoryOrdering.IndexOf(compositeServiceFactory)));
                }
                else
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.ExtensionDoesNotImplementGivenInterfaceErrorMessage, extension.GetType(), typeof(ITestSessionLifetimeHandler)));
                }
            }
        }

        return [.. testSessionLifetimeHandlers];
    }
}
