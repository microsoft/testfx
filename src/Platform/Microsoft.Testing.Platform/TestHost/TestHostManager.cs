// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

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
    private readonly List<Func<IServiceProvider, ITestApplicationLifecycleCallbacks>> _testApplicationLifecycleCallbacksFactories = [];
    private readonly List<Func<IServiceProvider, IDataConsumer>> _dataConsumerFactories = [];
    private readonly List<Func<IServiceProvider, ITestSessionLifetimeHandler>> _testSessionLifetimeHandlerFactories = [];
    private readonly List<ICompositeExtensionFactory> _dataConsumersCompositeServiceFactories = [];
    private readonly List<ICompositeExtensionFactory> _testSessionLifetimeHandlerCompositeFactories = [];

    // Non-exposed extension points
    private Func<IServiceProvider, ITestExecutionFilterFactory>? _testExecutionFilterFactory;
    private Func<IServiceProvider, ITestFrameworkInvoker>? _testFrameworkInvokerFactory;

    public void AddTestFrameworkInvoker(Func<IServiceProvider, ITestFrameworkInvoker> testFrameworkInvokerFactory)
    {
        ArgumentGuard.IsNotNull(testFrameworkInvokerFactory);
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
            throw new InvalidOperationException(PlatformResources.TEstExecutionFilterFactoryFactoryAlreadySetErrorMessage);
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
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.ExtensionWithSameUidAlreadyRegisteredErrorMessage, service.Uid, currentRegisteredExtension.GetType()));
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
        _factoryOrdering.Add(dataConsumerFactory);
    }

    public void AddDataConsumer<T>(CompositeExtensionFactory<T> compositeServiceFactory)
        where T : class, IDataConsumer
    {
        ArgumentGuard.IsNotNull(compositeServiceFactory);
        if (_dataConsumersCompositeServiceFactories.Contains(compositeServiceFactory))
        {
            throw new ArgumentException(PlatformResources.CompositeServiceFactoryInstanceAlreadyRegistered);
        }

        _dataConsumersCompositeServiceFactories.Add(compositeServiceFactory);
        _factoryOrdering.Add(compositeServiceFactory);
    }

    internal async Task<(IExtension Consumer, int RegistrationOrder)[]> BuildDataConsumersAsync(ServiceProvider serviceProvider, List<ICompositeExtensionFactory> alreadyBuiltServices)
    {
        List<(IExtension Consumer, int RegistrtionOrder)> dataConsumers = [];
        foreach (Func<IServiceProvider, IDataConsumer> dataConsumerFactory in _dataConsumerFactories)
        {
            IDataConsumer service = dataConsumerFactory(serviceProvider);

            // Check if we have already extensions of the same type with same id registered
            if (dataConsumers.Any(x => x.Consumer.Uid == service.Uid))
            {
                (IExtension consumer, int order) = dataConsumers.Single(x => x.Consumer.Uid == service.Uid);
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.ExtensionWithSameUidAlreadyRegisteredErrorMessage, service.Uid, consumer.GetType()));
            }

            // We initialize only if enabled
            if (await service.IsEnabledAsync())
            {
                if (service is IAsyncInitializableExtension async)
                {
                    await async.InitializeAsync();
                }

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
        ArgumentGuard.IsNotNull(testSessionLifetimeHandleFactory);
        _testSessionLifetimeHandlerFactories.Add(testSessionLifetimeHandleFactory);
        _factoryOrdering.Add(testSessionLifetimeHandleFactory);
    }

    public void AddTestSessionLifetimeHandle<T>(CompositeExtensionFactory<T> compositeServiceFactory)
        where T : class, ITestSessionLifetimeHandler
    {
        ArgumentGuard.IsNotNull(compositeServiceFactory);
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
                if (service is IAsyncInitializableExtension async)
                {
                    await async.InitializeAsync();
                }

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
