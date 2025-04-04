// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.TestHost;

internal sealed class TestHostManager : ITestHostManager
{
#pragma warning disable TPEXP
    private readonly NopFilter _noOpFilter = new();
#pragma warning restore TPEXP

    // Registration ordering
    private readonly List<object> _factoryOrdering = [];

    // Exposed extension points
    private readonly List<Func<IServiceProvider, ITestApplicationLifecycleCallbacks>> _testApplicationLifecycleCallbacksFactories = [];
    private readonly List<Func<IServiceProvider, ITestExecutionFilter>> _testExecutionFilterFactories = [];
    private readonly List<Func<IServiceProvider, IDataConsumer>> _dataConsumerFactories = [];
    private readonly List<Func<IServiceProvider, ITestSessionLifetimeHandler>> _testSessionLifetimeHandlerFactories = [];
    private readonly List<ICompositeExtensionFactory> _testExecutionFilterCompositeServiceFactories = [];
    private readonly List<ICompositeExtensionFactory> _dataConsumersCompositeServiceFactories = [];
    private readonly List<ICompositeExtensionFactory> _testSessionLifetimeHandlerCompositeFactories = [];

    // Non-exposed extension points
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
        if (await testAdapterInvoke.IsEnabledAsync())
        {
            await testAdapterInvoke.TryInitializeAsync();

            return ActionResult.Ok(testAdapterInvoke);
        }

        return ActionResult.Fail<ITestFrameworkInvoker>();
    }

    public Task<ITestExecutionFilter> BuildFilterAsync(ICollection<TestNode>? testNodes)
        => testNodes is null or { Count: 0 }
            ? Task.FromResult<ITestExecutionFilter>(_noOpFilter)
            : Task.FromResult<ITestExecutionFilter>(new TestNodeUidListFilter(testNodes.Select(x => x.Uid).ToArray()));

    public async Task<(ITestExecutionFilter Filter, int RegistrationOrder)[]> BuildFilterAsync(IServiceProvider serviceProvider, List<ICompositeExtensionFactory> alreadyBuiltServices)
    {
        List<(ITestExecutionFilter Filter, int RegistrationOrder)> filters = [];

        foreach (ITestExecutionFilter testExecutionFilter in _testExecutionFilterFactories
                     .Select(testExecutionFilterFactory => testExecutionFilterFactory(serviceProvider)))
        {
            await testExecutionFilter.TryInitializeAsync();

            filters.Add((testExecutionFilter, _factoryOrdering.IndexOf(testExecutionFilter)));
        }

        foreach (ICompositeExtensionFactory compositeServiceFactory in _testExecutionFilterCompositeServiceFactories)
        {
            ICompositeExtensionFactory? compositeFactoryInstance;

            // We check if the same service is already built in some other build phase
            if ((compositeFactoryInstance = alreadyBuiltServices.SingleOrDefault(x => x.GetType() == compositeServiceFactory.GetType())) is null)
            {
                // We clone the instance because we want to have fresh instance per BuildTestApplicationLifecycleCallbackAsync call
                compositeFactoryInstance = (ICompositeExtensionFactory)compositeServiceFactory.Clone();

                // Create the new fresh instance
                var instance = (ITestExecutionFilter)compositeFactoryInstance.GetInstance(serviceProvider);

                // Check if we have already extensions of the same type with same id registered
                if (filters.Any(x => x.GetType() == instance.GetType()))
                {
                    (ITestExecutionFilter filter, int _) = filters.Single(x => x.GetType() == instance.GetType());
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.ExtensionWithSameUidAlreadyRegisteredErrorMessage, instance.GetType().Name, filter.GetType()));
                }

                await instance.TryInitializeAsync();

                // Add to the list of shared singletons
                alreadyBuiltServices.Add(compositeFactoryInstance);
            }

            // Get the singleton
            var testExecutionFilter = (ITestExecutionFilter)compositeFactoryInstance.GetInstance();

            // Register the filter for usage
            filters.Add((testExecutionFilter, _factoryOrdering.IndexOf(compositeServiceFactory)));
        }

        return filters.ToArray();
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

        return testApplicationLifecycleCallbacks.ToArray();
    }

    public void AddTestExecutionFilter(Func<IServiceProvider, ITestExecutionFilter> testFilter)
    {
        Guard.NotNull(testFilter);
        _testExecutionFilterFactories.Add(testFilter);
        _factoryOrdering.Add(testFilter);
    }

    public void AddTestExecutionFilter<T>(CompositeExtensionFactory<T> compositeServiceFactory)
        where T : class, ITestExecutionFilter
    {
        Guard.NotNull(compositeServiceFactory);

        if (_testExecutionFilterCompositeServiceFactories.Contains(compositeServiceFactory))
        {
            throw new ArgumentException(PlatformResources.CompositeServiceFactoryInstanceAlreadyRegistered);
        }

        _testExecutionFilterCompositeServiceFactories.Add(compositeServiceFactory);
        _factoryOrdering.Add(compositeServiceFactory);
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
            if (dataConsumers.Any(x => x.Consumer.Uid == service.Uid))
            {
                (IExtension consumer, int _) = dataConsumers.Single(x => x.Consumer.Uid == service.Uid);
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.ExtensionWithSameUidAlreadyRegisteredErrorMessage, service.Uid, consumer.GetType()));
            }

            // We initialize only if enabled
            if (await service.IsEnabledAsync())
            {
                await service.TryInitializeAsync();

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
                if (dataConsumers.Any(x => x.Consumer.Uid == instance.Uid))
                {
                    (IExtension consumer, int _) = dataConsumers.Single(x => x.Consumer.Uid == instance.Uid);
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.ExtensionWithSameUidAlreadyRegisteredErrorMessage, instance.Uid, consumer.GetType()));
                }

                // We initialize only if enabled
                if (await instance.IsEnabledAsync())
                {
                    await instance.TryInitializeAsync();
                }

                // Add to the list of shared singletons
                alreadyBuiltServices.Add(compositeFactoryInstance);
            }

            // Get the singleton
            var extension = (IExtension)compositeFactoryInstance.GetInstance();

            // We register the extension only if enabled
            if (await extension.IsEnabledAsync())
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

        return dataConsumers.ToArray();
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
            if (testSessionLifetimeHandlers.Any(x => x.TestSessionLifetimeHandler.Uid == service.Uid))
            {
                (IExtension testSessionLifetimeHandler, int _) = testSessionLifetimeHandlers.Single(x => x.TestSessionLifetimeHandler.Uid == service.Uid);
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.ExtensionWithSameUidAlreadyRegisteredErrorMessage, service.Uid, testSessionLifetimeHandler.GetType()));
            }

            // We initialize only if enabled
            if (await service.IsEnabledAsync())
            {
                await service.TryInitializeAsync();

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
                if (testSessionLifetimeHandlers.Any(x => x.TestSessionLifetimeHandler.Uid == instance.Uid))
                {
                    (IExtension testSessionLifetimeHandler, int _) = testSessionLifetimeHandlers.Single(x => x.TestSessionLifetimeHandler.Uid == instance.Uid);
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.ExtensionWithSameUidAlreadyRegisteredErrorMessage, instance.Uid, testSessionLifetimeHandler.GetType()));
                }

                // We initialize only if enabled
                if (await instance.IsEnabledAsync())
                {
                    await instance.TryInitializeAsync();
                }

                // Add to the list of shared singletons
                alreadyBuiltServices.Add(compositeFactoryInstance);
            }

            // Get the singleton
            var extension = (IExtension)compositeFactoryInstance.GetInstance();

            // We register the extension only if enabled
            if (await extension.IsEnabledAsync())
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

        return testSessionLifetimeHandlers.ToArray();
    }
}
