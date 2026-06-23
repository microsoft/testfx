// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
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
    private readonly List<Func<IServiceProvider, ITestHostLauncher>> _testHostLauncherFactories = [];
    private readonly List<ICompositeExtensionFactory> _environmentVariableProviderCompositeFactories = [];
    private readonly List<ICompositeExtensionFactory> _lifetimeHandlerCompositeFactories = [];
    private readonly List<ICompositeExtensionFactory> _testHostLauncherCompositeFactories = [];
    private readonly List<ICompositeExtensionFactory> _alreadyBuiltServices = [];
    private readonly List<ICompositeExtensionFactory> _dataConsumersCompositeServiceFactories = [];

    [UnsupportedOSPlatform("browser")]
    public void AddEnvironmentVariableProvider(Func<IServiceProvider, ITestHostEnvironmentVariableProvider> environmentVariableProviderFactory)
        => AddEnvironmentVariableProvider(environmentVariableProviderFactory, insertAtStart: false);

    [UnsupportedOSPlatform("browser")]
    internal void AddEnvironmentVariableProviderFirst(Func<IServiceProvider, ITestHostEnvironmentVariableProvider> environmentVariableProviderFactory)
        => AddEnvironmentVariableProvider(environmentVariableProviderFactory, insertAtStart: true);

    private void AddEnvironmentVariableProvider(Func<IServiceProvider, ITestHostEnvironmentVariableProvider> environmentVariableProviderFactory, bool insertAtStart)
    {
        if (OperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException(PlatformResources.TestHostControllerProcessRestartNotSupportedOnWebAssembly);
        }

        _ = environmentVariableProviderFactory ?? throw new ArgumentNullException(nameof(environmentVariableProviderFactory));
        if (insertAtStart)
        {
            _environmentVariableProviderFactories.Insert(0, environmentVariableProviderFactory);
            _factoryOrdering.Insert(0, environmentVariableProviderFactory);
            return;
        }

        _environmentVariableProviderFactories.Add(environmentVariableProviderFactory);
        _factoryOrdering.Add(environmentVariableProviderFactory);
    }

    [UnsupportedOSPlatform("browser")]
    public void AddEnvironmentVariableProvider<T>(CompositeExtensionFactory<T> compositeServiceFactory)
        where T : class, ITestHostEnvironmentVariableProvider
    {
        if (OperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException(PlatformResources.TestHostControllerProcessRestartNotSupportedOnWebAssembly);
        }

        _ = compositeServiceFactory ?? throw new ArgumentNullException(nameof(compositeServiceFactory));
        if (_environmentVariableProviderCompositeFactories.Contains(compositeServiceFactory))
        {
            throw new ArgumentException(PlatformResources.CompositeServiceFactoryInstanceAlreadyRegistered);
        }

        _environmentVariableProviderCompositeFactories.Add(compositeServiceFactory);
        _factoryOrdering.Add(compositeServiceFactory);
    }

    [UnsupportedOSPlatform("browser")]
    public void AddProcessLifetimeHandler(Func<IServiceProvider, ITestHostProcessLifetimeHandler> lifetimeHandler)
    {
        if (OperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException(PlatformResources.TestHostControllerProcessRestartNotSupportedOnWebAssembly);
        }

        _ = lifetimeHandler ?? throw new ArgumentNullException(nameof(lifetimeHandler));
        _lifetimeHandlerFactories.Add(lifetimeHandler);
        _factoryOrdering.Add(lifetimeHandler);
    }

    [UnsupportedOSPlatform("browser")]
    public void AddProcessLifetimeHandler<T>(CompositeExtensionFactory<T> compositeServiceFactory)
        where T : class, ITestHostProcessLifetimeHandler
    {
        if (OperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException(PlatformResources.TestHostControllerProcessRestartNotSupportedOnWebAssembly);
        }

        _ = compositeServiceFactory ?? throw new ArgumentNullException(nameof(compositeServiceFactory));
        if (_lifetimeHandlerCompositeFactories.Contains(compositeServiceFactory))
        {
            throw new ArgumentException(PlatformResources.CompositeServiceFactoryInstanceAlreadyRegistered);
        }

        _lifetimeHandlerCompositeFactories.Add(compositeServiceFactory);
        _factoryOrdering.Add(compositeServiceFactory);
    }

    [UnsupportedOSPlatform("browser")]
    public void AddTestHostLauncher(Func<IServiceProvider, ITestHostLauncher> testHostLauncherFactory)
    {
        if (OperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException(PlatformResources.TestHostControllerProcessRestartNotSupportedOnWebAssembly);
        }

        _ = testHostLauncherFactory ?? throw new ArgumentNullException(nameof(testHostLauncherFactory));
        _testHostLauncherFactories.Add(testHostLauncherFactory);
        _factoryOrdering.Add(testHostLauncherFactory);
    }

    [UnsupportedOSPlatform("browser")]
    public void AddTestHostLauncher<T>(CompositeExtensionFactory<T> compositeServiceFactory)
        where T : class, ITestHostLauncher
    {
        if (OperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException(PlatformResources.TestHostControllerProcessRestartNotSupportedOnWebAssembly);
        }

        _ = compositeServiceFactory ?? throw new ArgumentNullException(nameof(compositeServiceFactory));
        if (_testHostLauncherCompositeFactories.Contains(compositeServiceFactory))
        {
            throw new ArgumentException(PlatformResources.CompositeServiceFactoryInstanceAlreadyRegistered);
        }

        _testHostLauncherCompositeFactories.Add(compositeServiceFactory);
        _factoryOrdering.Add(compositeServiceFactory);
    }

    [UnsupportedOSPlatform("browser")]
    public void AddDataConsumer<T>(CompositeExtensionFactory<T> compositeServiceFactory)
        where T : class, IDataConsumer
    {
        if (OperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException(PlatformResources.TestHostControllerProcessRestartNotSupportedOnWebAssembly);
        }

        _ = compositeServiceFactory ?? throw new ArgumentNullException(nameof(compositeServiceFactory));
        if (_dataConsumersCompositeServiceFactories.Contains(compositeServiceFactory))
        {
            throw new ArgumentException(PlatformResources.CompositeServiceFactoryInstanceAlreadyRegistered);
        }

        _dataConsumersCompositeServiceFactories.Add(compositeServiceFactory);
        _factoryOrdering.Add(compositeServiceFactory);
    }

    internal async Task<TestHostControllerConfiguration> BuildAsync(ServiceProvider serviceProvider)
    {
        List<(ITestHostEnvironmentVariableProvider TestHostEnvironmentVariableProvider, int RegistrationOrder)> environmentVariableProviders = [];
        foreach (Func<IServiceProvider, ITestHostEnvironmentVariableProvider> environmentVariableProviderFactory in _environmentVariableProviderFactories)
        {
            ITestHostEnvironmentVariableProvider envVarProvider = environmentVariableProviderFactory(serviceProvider);

            // Check if we have already extensions of the same type with same id registered
            environmentVariableProviders.ValidateUniqueExtension(envVarProvider, x => x.TestHostEnvironmentVariableProvider);

            // We initialize only if enabled
            if (await envVarProvider.IsEnabledAsync().ConfigureAwait(false))
            {
                await envVarProvider.TryInitializeAsync().ConfigureAwait(false);

                // Register the extension for usage
                environmentVariableProviders.Add((envVarProvider, _factoryOrdering.IndexOf(environmentVariableProviderFactory)));
                serviceProvider.TryAddService(envVarProvider);
            }
        }

        foreach (ICompositeExtensionFactory compositeServiceFactory in _environmentVariableProviderCompositeFactories)
        {
            // Get the singleton
            var extension = (IExtension)compositeServiceFactory.GetInstance(serviceProvider);
            bool isEnabledAsync = await extension.IsEnabledAsync().ConfigureAwait(false);

            // Check if we have already built the singleton for this composite factory
            if (!_alreadyBuiltServices.Contains(compositeServiceFactory))
            {
                // Check if we have already extensions of the same type with same id registered
                environmentVariableProviders.ValidateUniqueExtension(extension, x => x.TestHostEnvironmentVariableProvider);

                // We initialize only if enabled
                if (isEnabledAsync)
                {
                    await extension.TryInitializeAsync().ConfigureAwait(false);
                }

                // Add to the list of shared singletons
                _alreadyBuiltServices.Add(compositeServiceFactory);
            }

            // We register the extension only if enabled
            if (isEnabledAsync)
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
            lifetimeHandlers.ValidateUniqueExtension(lifetimeHandler, x => x.TestHostProcessLifetimeHandler);

            // We initialize only if enabled
            if (await lifetimeHandler.IsEnabledAsync().ConfigureAwait(false))
            {
                await lifetimeHandler.TryInitializeAsync().ConfigureAwait(false);

                // Register the extension for usage
                lifetimeHandlers.Add((lifetimeHandler, _factoryOrdering.IndexOf(lifetimeHandlerFactory)));
                serviceProvider.TryAddService(lifetimeHandler);
            }
        }

        foreach (ICompositeExtensionFactory compositeServiceFactory in _lifetimeHandlerCompositeFactories)
        {
            // Get the singleton
            var extension = (IExtension)compositeServiceFactory.GetInstance(serviceProvider);
            bool isEnabledAsync = await extension.IsEnabledAsync().ConfigureAwait(false);

            // Check if we have already built the singleton for this composite factory
            if (!_alreadyBuiltServices.Contains(compositeServiceFactory))
            {
                lifetimeHandlers.ValidateUniqueExtension(extension, x => x.TestHostProcessLifetimeHandler);

                // We initialize only if enabled
                if (isEnabledAsync)
                {
                    await extension.TryInitializeAsync().ConfigureAwait(false);
                }

                // Add to the list of shared singletons
                _alreadyBuiltServices.Add(compositeServiceFactory);
            }

            // We register the extension only if enabled
            if (isEnabledAsync)
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
                dataConsumers.ValidateUniqueExtension(instance, x => x.Consumer);

                // We initialize only if enabled
                if (await instance.IsEnabledAsync().ConfigureAwait(false))
                {
                    await instance.TryInitializeAsync().ConfigureAwait(false);
                }

                // Add to the list of shared singletons
                _alreadyBuiltServices.Add(compositeFactoryInstance);
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

        bool requireProcessRestart = environmentVariableProviders.Count > 0 || lifetimeHandlers.Count > 0 || dataConsumers.Count > 0;

        ITestHostLauncher? testHostLauncher = await BuildTestHostLauncherAsync(serviceProvider).ConfigureAwait(false);
        if (testHostLauncher is not null)
        {
            // A custom launcher only makes sense when the out-of-process test host is started, so we
            // force the controller (process restart) host even when no other controller extension is present.
            requireProcessRestart = true;
        }

        return new TestHostControllerConfiguration(
            [.. environmentVariableProviders.OrderBy(x => x.RegistrationOrder).Select(x => x.TestHostEnvironmentVariableProvider)],
            [.. lifetimeHandlers.OrderBy(x => x.RegistrationOrder).Select(x => x.TestHostProcessLifetimeHandler)],
            [.. dataConsumers.OrderBy(x => x.RegistrationOrder).Select(x => x.Consumer)],
            testHostLauncher,
            requireProcessRestart);
    }

    private async Task<ITestHostLauncher?> BuildTestHostLauncherAsync(ServiceProvider serviceProvider)
    {
        List<ITestHostLauncher> launchers = [];

        foreach (Func<IServiceProvider, ITestHostLauncher> testHostLauncherFactory in _testHostLauncherFactories)
        {
            ITestHostLauncher launcher = testHostLauncherFactory(serviceProvider);

            // Check if we have already extensions of the same type with same id registered
            launchers.ValidateUniqueExtension(launcher);

            // We initialize only if enabled
            if (await launcher.IsEnabledAsync().ConfigureAwait(false))
            {
                await launcher.TryInitializeAsync().ConfigureAwait(false);
                launchers.Add(launcher);
                serviceProvider.TryAddService(launcher);
            }
        }

        foreach (ICompositeExtensionFactory compositeServiceFactory in _testHostLauncherCompositeFactories)
        {
            // Get the singleton
            var extension = (IExtension)compositeServiceFactory.GetInstance(serviceProvider);
            bool isEnabledAsync = await extension.IsEnabledAsync().ConfigureAwait(false);

            // Check if we have already built the singleton for this composite factory
            if (!_alreadyBuiltServices.Contains(compositeServiceFactory))
            {
                launchers.ValidateUniqueExtension(extension);

                // We initialize only if enabled
                if (isEnabledAsync)
                {
                    await extension.TryInitializeAsync().ConfigureAwait(false);
                }

                // Add to the list of shared singletons
                _alreadyBuiltServices.Add(compositeServiceFactory);
            }

            // We register the extension only if enabled
            if (isEnabledAsync)
            {
                if (extension is ITestHostLauncher testHostLauncher)
                {
                    launchers.Add(testHostLauncher);
                    serviceProvider.TryAddService(testHostLauncher);
                }
                else
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.ExtensionDoesNotImplementGivenInterfaceErrorMessage, extension.GetType(), typeof(ITestHostLauncher)));
                }
            }
        }

        return launchers.Count switch
        {
            0 => null,
            1 => launchers[0],
            _ => throw new InvalidOperationException(PlatformResources.OnlyOneTestHostLauncherSupported),
        };
    }
}
