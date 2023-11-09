// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.TestHost;

internal sealed class TestHostManager : ITestHostManager
{
    // Exposed extension points
    private readonly List<Func<IServiceProvider, ITestApplicationLifecycleCallbacks>> _testApplicationLifecycleCallbacksFactories = [];
    private readonly List<Func<IServiceProvider, IDataConsumer>> _dataConsumerFactories = [];
    private readonly List<Func<IServiceProvider, ITestSessionLifetimeHandler>> _testSessionLifetimeHandlerFactories = [];
    private readonly List<ICompositeExtensionFactory> _dataConsumersCompositeServiceFactories = [];
    private readonly List<ICompositeExtensionFactory> _testSessionLifetimeHandlerCompositeFactories = [];

    // Non-exposed extension points
    private Func<IServiceProvider, ITestExecutionFilterFactory>? _testExecutionFilterFactory;
    private Func<IServiceProvider, ITestFrameworkInvoker>? _testAdapterInvoker;

    public void AddTestAdapterInvoker(Func<IServiceProvider, ITestFrameworkInvoker> testAdapterInvoker)
    {
        ArgumentGuard.IsNotNull(testAdapterInvoker);
        if (_testAdapterInvoker is not null)
        {
            throw new InvalidOperationException($"Custom ITestAdapterInvoker already set, '{_testAdapterInvoker.GetType()}'");
        }

        _testAdapterInvoker = testAdapterInvoker;
    }

    internal async Task<ActionResult<ITestFrameworkInvoker>> TryBuildTestAdapterInvokerAsync(ServiceProvider serviceProvider)
    {
        if (_testAdapterInvoker is null)
        {
            return ActionResult.Fail<ITestFrameworkInvoker>();
        }

        ITestFrameworkInvoker testAdapterInvoke = _testAdapterInvoker(serviceProvider);

        // We initialize only if enabled
        if (await testAdapterInvoke.IsEnabledAsync())
        {
            if (testAdapterInvoke is IAsyncInitializableExtension async)
            {
                await async.InitializeAsync();
            }

            return ActionResult.Ok(testAdapterInvoke);
        }

        return ActionResult.Fail<ITestFrameworkInvoker>();
    }

    public void AddTestExecutionFilterFactory(Func<IServiceProvider, ITestExecutionFilterFactory> testExecutionFilterFactory)
    {
        ArgumentGuard.IsNotNull(testExecutionFilterFactory);
        if (_testExecutionFilterFactory is not null)
        {
            throw new InvalidOperationException($"Custom ITestExecutionFilterFactory already set, '{_testExecutionFilterFactory.GetType()}'");
        }

        _testExecutionFilterFactory = testExecutionFilterFactory;
    }

    internal async Task<ActionResult<ITestExecutionFilterFactory>> TryBuildTestExecutionFilterFactoryAsync(ServiceProvider serviceProvider)
    {
        if (_testExecutionFilterFactory is null)
        {
            return ActionResult.Fail<ITestExecutionFilterFactory>();
        }

        ITestExecutionFilterFactory testExecutionFilterFactory = _testExecutionFilterFactory(serviceProvider);

        // We initialize only if enabled
        if (await testExecutionFilterFactory.IsEnabledAsync())
        {
            if (testExecutionFilterFactory is IAsyncInitializableExtension async)
            {
                await async.InitializeAsync();
            }

            return ActionResult.Ok(testExecutionFilterFactory);
        }

        return ActionResult.Fail<ITestExecutionFilterFactory>();
    }

    public void AddTestApplicationLifecycleCallbacks(Func<IServiceProvider, ITestApplicationLifecycleCallbacks> testApplicationLifecycleCallbacks)
    {
        ArgumentGuard.IsNotNull(testApplicationLifecycleCallbacks);
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
                throw new InvalidOperationException($"Another extension with the same Uid '{service.Uid}' is already registered. Extension type: '{currentRegisteredExtension.GetType()}'");
            }

            // We initialize only if enabled
            if (await service.IsEnabledAsync())
            {
                if (service is IAsyncInitializableExtension async)
                {
                    await async.InitializeAsync();
                }

                // Register the extension for usage
                testApplicationLifecycleCallbacks.Add(service);
            }
        }

        return testApplicationLifecycleCallbacks.ToArray();
    }

    public void AddDataConsumer(Func<IServiceProvider, IDataConsumer> dataConsumerFactory)
    {
        ArgumentGuard.IsNotNull(dataConsumerFactory);
        _dataConsumerFactories.Add(dataConsumerFactory);
    }

    public void AddDataConsumer<T>(CompositeExtensionFactory<T> compositeServiceFactory)
      where T : class, IDataConsumer
    {
        ArgumentGuard.IsNotNull(compositeServiceFactory);
        if (_dataConsumersCompositeServiceFactories.Contains(compositeServiceFactory))
        {
            throw new InvalidOperationException("Same instance already added");
        }

        _dataConsumersCompositeServiceFactories.Add(compositeServiceFactory);
    }

    internal async Task<IDataConsumer[]> BuildDataConsumersAsync(ServiceProvider serviceProvider, List<ICompositeExtensionFactory> alreadyBuiltServices)
    {
        List<IDataConsumer> dataConsumers = [];
        foreach (Func<IServiceProvider, IDataConsumer> dataConsumerFactory in _dataConsumerFactories)
        {
            IDataConsumer service = dataConsumerFactory(serviceProvider);

            // Check if we have already extensions of the same type with same id registered
            if (dataConsumers.Any(x => x.Uid == service.Uid))
            {
                IDataConsumer currentRegisteredExtension = dataConsumers.Single(x => x.Uid == service.Uid);
                throw new InvalidOperationException($"Another extension with the same Uid '{service.Uid}' is already registered. Extension type: '{currentRegisteredExtension.GetType()}'");
            }

            // We initialize only if enabled
            if (await service.IsEnabledAsync())
            {
                if (service is IAsyncInitializableExtension async)
                {
                    await async.InitializeAsync();
                }

                // Register the extension for usage
                dataConsumers.Add(service);
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
                if (dataConsumers.Any(x => x.Uid == instance.Uid))
                {
                    IDataConsumer currentRegisteredExtension = dataConsumers.Single(x => x.Uid == instance.Uid);
                    throw new InvalidOperationException($"Another extension with the same Uid '{instance.Uid}' is already registered. Extension type: '{currentRegisteredExtension.GetType()}'");
                }

                // We initialize only if enabled
                if (await instance.IsEnabledAsync())
                {
                    if (instance is IAsyncInitializableExtension async)
                    {
                        await async.InitializeAsync();
                    }
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
                    dataConsumers.Add(consumer);
                }
                else
                {
                    throw new InvalidOperationException($"Type '{extension.GetType()}' doesn't implement the '{typeof(IDataConsumer)}' interface");
                }
            }
        }

        return dataConsumers.ToArray();
    }

    public void AddTestSessionLifetimeHandle(Func<IServiceProvider, ITestSessionLifetimeHandler> testSessionLifetimeHandleFactory)
    {
        ArgumentGuard.IsNotNull(testSessionLifetimeHandleFactory);
        _testSessionLifetimeHandlerFactories.Add(testSessionLifetimeHandleFactory);
    }

    public void AddTestSessionLifetimeHandle<T>(CompositeExtensionFactory<T> compositeServiceFactory)
        where T : class, ITestSessionLifetimeHandler
    {
        ArgumentGuard.IsNotNull(compositeServiceFactory);
        if (_testSessionLifetimeHandlerCompositeFactories.Contains(compositeServiceFactory))
        {
            throw new InvalidOperationException("Same instance already added");
        }

        _testSessionLifetimeHandlerCompositeFactories.Add(compositeServiceFactory);
    }

    internal async Task<ITestSessionLifetimeHandler[]> BuildTestSessionLifetimeHandleAsync(ServiceProvider serviceProvider, List<ICompositeExtensionFactory> alreadyBuiltServices)
    {
        List<ITestSessionLifetimeHandler> testSessionLifetimeHandlers = [];
        foreach (Func<IServiceProvider, ITestSessionLifetimeHandler> testSessionLifetimeHandlerFactory in _testSessionLifetimeHandlerFactories)
        {
            ITestSessionLifetimeHandler service = testSessionLifetimeHandlerFactory(serviceProvider);

            // Check if we have already extensions of the same type with same id registered
            if (testSessionLifetimeHandlers.Any(x => x.Uid == service.Uid))
            {
                ITestSessionLifetimeHandler currentRegisteredExtension = testSessionLifetimeHandlers.Single(x => x.Uid == service.Uid);
                throw new InvalidOperationException($"Another extension with the same Uid '{service.Uid}' is already registered. Extension type: '{currentRegisteredExtension.GetType()}'");
            }

            // We initialize only if enabled
            if (await service.IsEnabledAsync())
            {
                if (service is IAsyncInitializableExtension async)
                {
                    await async.InitializeAsync();
                }

                // Register the extension for usage
                testSessionLifetimeHandlers.Add(service);
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
                if (testSessionLifetimeHandlers.Any(x => x.Uid == instance.Uid))
                {
                    ITestSessionLifetimeHandler currentRegisteredExtension = testSessionLifetimeHandlers.Single(x => x.Uid == instance.Uid);
                    throw new InvalidOperationException($"Another extension with the same Uid '{instance.Uid}' is already registered. Extension type: '{currentRegisteredExtension.GetType()}'");
                }

                // We initialize only if enabled
                if (await instance.IsEnabledAsync())
                {
                    if (instance is IAsyncInitializableExtension async)
                    {
                        await async.InitializeAsync();
                    }
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
                    testSessionLifetimeHandlers.Add(testSessionLifetimeHandler);
                }
                else
                {
                    throw new InvalidOperationException($"Type '{extension.GetType()}' doesn't implement the '{typeof(ITestSessionLifetimeHandler)}' interface");
                }
            }
        }

        return testSessionLifetimeHandlers.ToArray();
    }
}
