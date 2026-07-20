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

    private static void ThrowIfBrowserPlatform()
    {
        if (OperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException(PlatformResources.TestHostControllerProcessRestartNotSupportedOnWebAssembly);
        }
    }

    [UnsupportedOSPlatform("browser")]
    public void AddEnvironmentVariableProvider(Func<IServiceProvider, ITestHostEnvironmentVariableProvider> environmentVariableProviderFactory)
        => AddEnvironmentVariableProvider(environmentVariableProviderFactory, insertAtStart: false);

    [UnsupportedOSPlatform("browser")]
    internal void AddEnvironmentVariableProviderFirst(Func<IServiceProvider, ITestHostEnvironmentVariableProvider> environmentVariableProviderFactory)
        => AddEnvironmentVariableProvider(environmentVariableProviderFactory, insertAtStart: true);

    private void AddEnvironmentVariableProvider(Func<IServiceProvider, ITestHostEnvironmentVariableProvider> environmentVariableProviderFactory, bool insertAtStart)
    {
        ThrowIfBrowserPlatform();

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
        ThrowIfBrowserPlatform();

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
        ThrowIfBrowserPlatform();

        _ = lifetimeHandler ?? throw new ArgumentNullException(nameof(lifetimeHandler));
        _lifetimeHandlerFactories.Add(lifetimeHandler);
        _factoryOrdering.Add(lifetimeHandler);
    }

    [UnsupportedOSPlatform("browser")]
    public void AddProcessLifetimeHandler<T>(CompositeExtensionFactory<T> compositeServiceFactory)
        where T : class, ITestHostProcessLifetimeHandler
    {
        ThrowIfBrowserPlatform();

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
        ThrowIfBrowserPlatform();

        _ = testHostLauncherFactory ?? throw new ArgumentNullException(nameof(testHostLauncherFactory));
        _testHostLauncherFactories.Add(testHostLauncherFactory);
        _factoryOrdering.Add(testHostLauncherFactory);
    }

    [UnsupportedOSPlatform("browser")]
    public void AddTestHostLauncher<T>(CompositeExtensionFactory<T> compositeServiceFactory)
        where T : class, ITestHostLauncher
    {
        ThrowIfBrowserPlatform();

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
        ThrowIfBrowserPlatform();

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
        List<(IExtension Extension, int RegistrationOrder)> environmentVariableProviders = [];
        await ExtensionBuilderHelper.BuildAndRegisterExtensionsAsync(_environmentVariableProviderFactories, serviceProvider, environmentVariableProviders, _factoryOrdering, registerInServiceProvider: true).ConfigureAwait(false);
        await ExtensionBuilderHelper.BuildAndRegisterCompositeExtensionsInPlaceAsync<ITestHostEnvironmentVariableProvider>(_environmentVariableProviderCompositeFactories, serviceProvider, environmentVariableProviders, _alreadyBuiltServices, _factoryOrdering).ConfigureAwait(false);

        List<(IExtension Extension, int RegistrationOrder)> lifetimeHandlers = [];
        await ExtensionBuilderHelper.BuildAndRegisterExtensionsAsync(_lifetimeHandlerFactories, serviceProvider, lifetimeHandlers, _factoryOrdering, registerInServiceProvider: true).ConfigureAwait(false);
        await ExtensionBuilderHelper.BuildAndRegisterCompositeExtensionsInPlaceAsync<ITestHostProcessLifetimeHandler>(_lifetimeHandlerCompositeFactories, serviceProvider, lifetimeHandlers, _alreadyBuiltServices, _factoryOrdering).ConfigureAwait(false);

        List<(IExtension Extension, int RegistrationOrder)> dataConsumers = [];
        await ExtensionBuilderHelper.BuildAndRegisterCompositeExtensionsAsync<IDataConsumer>(_dataConsumersCompositeServiceFactories, serviceProvider, dataConsumers, _alreadyBuiltServices, _factoryOrdering).ConfigureAwait(false);

        bool requireProcessRestart = environmentVariableProviders.Count > 0 || lifetimeHandlers.Count > 0 || dataConsumers.Count > 0;

        ITestHostLauncher? testHostLauncher = await BuildTestHostLauncherAsync(serviceProvider).ConfigureAwait(false);
        if (testHostLauncher is not null)
        {
            // A custom launcher only makes sense when the out-of-process test host is started, so we
            // force the controller (process restart) host even when no other controller extension is present.
            requireProcessRestart = true;
        }

        return new TestHostControllerConfiguration(
            [.. environmentVariableProviders.OrderBy(x => x.RegistrationOrder).Select(x => (ITestHostEnvironmentVariableProvider)x.Extension)],
            [.. lifetimeHandlers.OrderBy(x => x.RegistrationOrder).Select(x => (ITestHostProcessLifetimeHandler)x.Extension)],
            [.. dataConsumers.OrderBy(x => x.RegistrationOrder).Select(x => (IDataConsumer)x.Extension)],
            testHostLauncher,
            requireProcessRestart);
    }

    private async Task<ITestHostLauncher?> BuildTestHostLauncherAsync(ServiceProvider serviceProvider)
    {
        List<ITestHostLauncher> launchers = [];
        await ExtensionBuilderHelper.BuildAndRegisterExtensionsAsync(_testHostLauncherFactories, serviceProvider, launchers, registerInServiceProvider: true).ConfigureAwait(false);

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
