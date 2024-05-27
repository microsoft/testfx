// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.TestHostControllers;

internal sealed class TestHostControllersManager : ITestHostControllersManager
{
    // Registration ordering
    private readonly List<object> _factoryOrdering = [];

    private readonly List<Func<IServiceProvider, ITestHostEnvironmentVariableProvider>> _environmentVariableProviderFactories = [];
    private readonly List<Func<IServiceProvider, ITestHostProcessLifetimeHandler>> _lifetimeHandlerFactories = [];
    private readonly List<Func<IServiceProvider, IDataConsumer>> _dataConsumerFactories = [];
    private readonly List<ICompositeExtensionFactory> _environmentVariableProviderCompositeFactories = [];
    private readonly List<ICompositeExtensionFactory> _lifetimeHandlerCompositeFactories = [];
    private readonly List<ICompositeExtensionFactory> _alreadyBuiltServices = [];
    private readonly List<ICompositeExtensionFactory> _dataConsumersCompositeServiceFactories = [];

    public void AddEnvironmentVariableProvider(Func<IServiceProvider, ITestHostEnvironmentVariableProvider> environmentVariableProviderFactory)
    {
        ArgumentGuard.IsNotNull(environmentVariableProviderFactory);
        _environmentVariableProviderFactories.Add(environmentVariableProviderFactory);
        _factoryOrdering.Add(environmentVariableProviderFactory);
    }

    public void AddEnvironmentVariableProvider<T>(CompositeExtensionFactory<T> compositeServiceFactory)
        where T : class, ITestHostEnvironmentVariableProvider
    {
        ArgumentGuard.IsNotNull(compositeServiceFactory);
        if (_environmentVariableProviderCompositeFactories.Contains(compositeServiceFactory))
        {
            throw new ArgumentException(PlatformResources.CompositeServiceFactoryInstanceAlreadyRegistered);
        }

        _environmentVariableProviderCompositeFactories.Add(compositeServiceFactory);
        _factoryOrdering.Add(compositeServiceFactory);
    }

    public void AddProcessLifetimeHandler(Func<IServiceProvider, ITestHostProcessLifetimeHandler> lifetimeHandler)
    {
        ArgumentGuard.IsNotNull(lifetimeHandler);
        _lifetimeHandlerFactories.Add(lifetimeHandler);
        _factoryOrdering.Add(lifetimeHandler);
    }

    public void AddProcessLifetimeHandler<T>(CompositeExtensionFactory<T> compositeServiceFactory)
        where T : class, ITestHostProcessLifetimeHandler
    {
        ArgumentGuard.IsNotNull(compositeServiceFactory);
        if (_lifetimeHandlerCompositeFactories.Contains(compositeServiceFactory))
        {
            throw new ArgumentException(PlatformResources.CompositeServiceFactoryInstanceAlreadyRegistered);
        }

        _lifetimeHandlerCompositeFactories.Add(compositeServiceFactory);
        _factoryOrdering.Add(compositeServiceFactory);
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

    internal async Task<TestHostControllerConfiguration> BuildAsync(ServiceProvider serviceProvider)
    {
        // For now the test host working directory and the current working directory are the same.
        // In future we could move the test host in a different directory for instance in case of
        // the need to rewrite binary files. If we don't move files are locked by ourself.
        var aggregatedConfiguration = (AggregatedConfiguration)serviceProvider.GetConfiguration();
        string? currentWorkingDirectory = aggregatedConfiguration[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory];
        ApplicationStateGuard.Ensure(currentWorkingDirectory is not null);
        aggregatedConfiguration.SetTestHostWorkingDirectory(currentWorkingDirectory);

        List<(ITestHostEnvironmentVariableProvider TestHostEnvironmentVariableProvider, int RegistrationOrder)> environmentVariableProviders = [];
        foreach (Func<IServiceProvider, ITestHostEnvironmentVariableProvider> environmentVariableProviderFactory in _environmentVariableProviderFactories)
        {
            ITestHostEnvironmentVariableProvider envVarProvider = environmentVariableProviderFactory(serviceProvider);

            // Check if we have already extensions of the same type with same id registered
            if (environmentVariableProviders.Any(x => x.TestHostEnvironmentVariableProvider.Uid == envVarProvider.Uid))
            {
                (ITestHostEnvironmentVariableProvider testHostEnvironmentVariableProvider, int _) = environmentVariableProviders.Single(x => x.TestHostEnvironmentVariableProvider.Uid == envVarProvider.Uid);
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.ExtensionWithSameUidAlreadyRegisteredErrorMessage, envVarProvider.Uid, testHostEnvironmentVariableProvider.GetType()));
            }

            // We initialize only if enabled
            if (await envVarProvider.IsEnabledAsync())
            {
                await envVarProvider.TryInitialize();

                // Register the extension for usage
                environmentVariableProviders.Add((envVarProvider, _factoryOrdering.IndexOf(environmentVariableProviderFactory)));
                serviceProvider.TryAddService(envVarProvider);
            }
        }

        foreach (ICompositeExtensionFactory compositeServiceFactory in _environmentVariableProviderCompositeFactories)
        {
            // Check if we have already built the singleton for this composite factory
            if (!_alreadyBuiltServices.Contains(compositeServiceFactory))
            {
                // Get the singleton for init
                var instance = (IExtension)compositeServiceFactory.GetInstance(serviceProvider);

                // Check if we have already extensions of the same type with same id registered
                if (environmentVariableProviders.Any(x => x.TestHostEnvironmentVariableProvider.Uid == instance.Uid))
                {
                    (ITestHostEnvironmentVariableProvider testHostEnvironmentVariableProvider, int _) = environmentVariableProviders.Single(x => x.TestHostEnvironmentVariableProvider.Uid == instance.Uid);
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.ExtensionWithSameUidAlreadyRegisteredErrorMessage, instance.Uid, testHostEnvironmentVariableProvider.GetType()));
                }

                // We initialize only if enabled
                if (await instance.IsEnabledAsync())
                {
                    await instance.TryInitialize();
                }

                // Add to the list of shared singletons
                _alreadyBuiltServices.Add(compositeServiceFactory);
            }

            // Get the singleton
            var extension = (IExtension)compositeServiceFactory.GetInstance(serviceProvider);

            // We register the extension only if enabled
            if (await extension.IsEnabledAsync())
            {
                if (extension is ITestHostEnvironmentVariableProvider testHostEnvironmentVariableProvider)
                {
                    // Register the extension for usage
                    environmentVariableProviders.Add((testHostEnvironmentVariableProvider, _factoryOrdering.IndexOf(compositeServiceFactory)));
                    serviceProvider.TryAddService(testHostEnvironmentVariableProvider);
                }
                else
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.ExtensionDoesNotImplementGivenInterfaceErrorMessage, extension.GetType(), typeof(ITestHostEnvironmentVariableProvider)));
                }
            }
        }

        List<(ITestHostProcessLifetimeHandler TestHostProcessLifetimeHandler, int RegistrationOrder)> lifetimeHandlers = [];
        foreach (Func<IServiceProvider, ITestHostProcessLifetimeHandler> lifetimeHandlerFactory in _lifetimeHandlerFactories)
        {
            ITestHostProcessLifetimeHandler lifetimeHandler = lifetimeHandlerFactory(serviceProvider);

            // Check if we have already extensions of the same type with same id registered
            if (lifetimeHandlers.Any(x => x.TestHostProcessLifetimeHandler.Uid == lifetimeHandler.Uid))
            {
                (ITestHostProcessLifetimeHandler testHostProcessLifetimeHandler, int _) = lifetimeHandlers.Single(x => x.TestHostProcessLifetimeHandler.Uid == lifetimeHandler.Uid);
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.ExtensionWithSameUidAlreadyRegisteredErrorMessage, lifetimeHandler.Uid, testHostProcessLifetimeHandler.GetType()));
            }

            // We initialize only if enabled
            if (await lifetimeHandler.IsEnabledAsync())
            {
                await lifetimeHandler.TryInitialize();

                // Register the extension for usage
                lifetimeHandlers.Add((lifetimeHandler, _factoryOrdering.IndexOf(lifetimeHandlerFactory)));
                serviceProvider.TryAddService(lifetimeHandler);
            }
        }

        foreach (ICompositeExtensionFactory compositeServiceFactory in _lifetimeHandlerCompositeFactories)
        {
            // Check if we have already built the singleton for this composite factory
            if (!_alreadyBuiltServices.Contains(compositeServiceFactory))
            {
                // Get the singleton for init
                var instance = (IExtension)compositeServiceFactory.GetInstance(serviceProvider);

                if (lifetimeHandlers.Any(x => x.TestHostProcessLifetimeHandler.Uid == instance.Uid))
                {
                    (ITestHostProcessLifetimeHandler testHostProcessLifetimeHandler, int _) = lifetimeHandlers.Single(x => x.TestHostProcessLifetimeHandler.Uid == instance.Uid);
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.ExtensionWithSameUidAlreadyRegisteredErrorMessage, instance.Uid, testHostProcessLifetimeHandler.GetType()));
                }

                // We initialize only if enabled
                if (await instance.IsEnabledAsync())
                {
                    await instance.TryInitialize();
                }

                // Add to the list of shared singletons
                _alreadyBuiltServices.Add(compositeServiceFactory);
            }

            // Get the singleton
            var extension = (IExtension)compositeServiceFactory.GetInstance(serviceProvider);

            // We register the extension only if enabled
            if (await extension.IsEnabledAsync())
            {
                if (extension is ITestHostProcessLifetimeHandler testHostProcessLifetimeHandler)
                {
                    // Register the extension for usage
                    lifetimeHandlers.Add((testHostProcessLifetimeHandler, _factoryOrdering.IndexOf(compositeServiceFactory)));
                    serviceProvider.TryAddService(testHostProcessLifetimeHandler);
                }
                else
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.ExtensionDoesNotImplementGivenInterfaceErrorMessage, extension.GetType(), typeof(ITestHostProcessLifetimeHandler)));
                }
            }
        }

        List<(IDataConsumer Consumer, int RegistrationOrder)> dataConsumers = [];
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
                await service.TryInitialize();

                // Register the extension for usage
                dataConsumers.Add((service, _factoryOrdering.IndexOf(dataConsumerFactory)));
            }
        }

        foreach (ICompositeExtensionFactory compositeServiceFactory in _dataConsumersCompositeServiceFactories)
        {
            ICompositeExtensionFactory? compositeFactoryInstance;

            // We check if the same service is already built in some other build phase
            if ((compositeFactoryInstance = _alreadyBuiltServices.SingleOrDefault(x => x.GetType() == compositeServiceFactory.GetType())) is null)
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
                    await instance.TryInitialize();
                }

                // Add to the list of shared singletons
                _alreadyBuiltServices.Add(compositeFactoryInstance);
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

        bool requireProcessRestart = environmentVariableProviders.Count > 0 || lifetimeHandlers.Count > 0 || dataConsumers.Count > 0;
        return new TestHostControllerConfiguration(
            environmentVariableProviders.OrderBy(x => x.RegistrationOrder).Select(x => x.TestHostEnvironmentVariableProvider).ToArray(),
            lifetimeHandlers.OrderBy(x => x.RegistrationOrder).Select(x => x.TestHostProcessLifetimeHandler).ToArray(),
            dataConsumers.OrderBy(x => x.RegistrationOrder).Select(x => x.Consumer).ToArray(),
            requireProcessRestart);
    }
}
