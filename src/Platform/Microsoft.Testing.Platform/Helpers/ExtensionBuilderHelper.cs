// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Helpers;

/// <summary>
/// Shared helpers implementing the common "instantiate → validate unique → check enabled →
/// initialize → register" loop used to build platform extensions across the various
/// <c>*Manager</c> classes.
/// </summary>
internal static class ExtensionBuilderHelper
{
    /// <summary>
    /// Builds and registers extensions produced by simple factories into <paramref name="result"/>.
    /// For each factory the extension is instantiated, validated for uniqueness, and — when enabled —
    /// initialized and added to the result (optionally registered in the service provider).
    /// </summary>
    /// <typeparam name="T">The extension type produced by the factories.</typeparam>
    public static async Task BuildAndRegisterExtensionsAsync<T>(
        IEnumerable<Func<IServiceProvider, T>> factories,
        ServiceProvider serviceProvider,
        List<T> result,
        bool registerInServiceProvider = false)
        where T : class, IExtension
    {
        foreach (Func<IServiceProvider, T> factory in factories)
        {
            T service = factory(serviceProvider);

            // Check if we have already extensions of the same type with same id registered
            result.ValidateUniqueExtension(service);

            // We initialize only if enabled
            if (await service.IsEnabledAsync().ConfigureAwait(false))
            {
                await service.TryInitializeAsync().ConfigureAwait(false);

                // Register the extension for usage
                result.Add(service);

                if (registerInServiceProvider)
                {
                    serviceProvider.TryAddService(service);
                }
            }
        }
    }

    /// <summary>
    /// Builds and registers extensions produced by simple factories into <paramref name="result"/>,
    /// preserving the registration order captured in <paramref name="factoryOrdering"/>.
    /// </summary>
    /// <typeparam name="T">The extension type produced by the factories.</typeparam>
    public static async Task BuildAndRegisterExtensionsAsync<T>(
        IEnumerable<Func<IServiceProvider, T>> factories,
        ServiceProvider serviceProvider,
        List<(IExtension Extension, int RegistrationOrder)> result,
        List<object> factoryOrdering,
        bool registerInServiceProvider = false)
        where T : class, IExtension
    {
        foreach (Func<IServiceProvider, T> factory in factories)
        {
            T service = factory(serviceProvider);

            // Check if we have already extensions of the same type with same id registered
            result.ValidateUniqueExtension(service, static x => x.Extension);

            // We initialize only if enabled
            if (await service.IsEnabledAsync().ConfigureAwait(false))
            {
                await service.TryInitializeAsync().ConfigureAwait(false);

                // Register the extension for usage
                result.Add((service, factoryOrdering.IndexOf(factory)));

                if (registerInServiceProvider)
                {
                    serviceProvider.TryAddService(service);
                }
            }
        }
    }

    /// <summary>
    /// Builds and registers extensions produced by composite factories, cloning each factory to get a
    /// fresh instance the first time it is seen and reusing the shared singleton afterwards. The built
    /// extension must implement <typeparamref name="T"/>, otherwise an <see cref="InvalidOperationException"/>
    /// is thrown.
    /// </summary>
    /// <typeparam name="T">The extension interface the built extension is expected to implement.</typeparam>
    public static async Task BuildAndRegisterCompositeExtensionsAsync<T>(
        IEnumerable<ICompositeExtensionFactory> compositeFactories,
        ServiceProvider serviceProvider,
        List<(IExtension Extension, int RegistrationOrder)> result,
        List<ICompositeExtensionFactory> alreadyBuiltServices,
        List<object> factoryOrdering)
        where T : IExtension
    {
        foreach (ICompositeExtensionFactory compositeServiceFactory in compositeFactories)
        {
            ICompositeExtensionFactory? compositeFactoryInstance;

            // We check if the same service is already built in some other build phase
            if ((compositeFactoryInstance = alreadyBuiltServices.SingleOrDefault(x => x.GetType() == compositeServiceFactory.GetType())) is null)
            {
                // We clone the instance because we want to have fresh instance per build phase
                compositeFactoryInstance = (ICompositeExtensionFactory)compositeServiceFactory.Clone();

                // Create the new fresh instance
                var instance = (IExtension)compositeFactoryInstance.GetInstance(serviceProvider);

                // Check if we have already extensions of the same type with same id registered
                result.ValidateUniqueExtension(instance, static x => x.Extension);

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
                if (extension is T typedExtension)
                {
                    // Register the extension for usage
                    result.Add((typedExtension, factoryOrdering.IndexOf(compositeServiceFactory)));
                    continue;
                }

                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.ExtensionDoesNotImplementGivenInterfaceErrorMessage, extension.GetType(), typeof(T)));
            }
        }
    }

    /// <summary>
    /// Builds and registers extensions produced by composite factories, reusing the singleton in place
    /// (without cloning) and tracking already-built factories in <paramref name="alreadyBuiltServices"/>.
    /// The built extension must implement <typeparamref name="T"/>, otherwise an
    /// <see cref="InvalidOperationException"/> is thrown. Enabled extensions are also registered in the
    /// service provider.
    /// </summary>
    /// <typeparam name="T">The extension interface the built extension is expected to implement.</typeparam>
    public static async Task BuildAndRegisterCompositeExtensionsInPlaceAsync<T>(
        IEnumerable<ICompositeExtensionFactory> compositeFactories,
        ServiceProvider serviceProvider,
        List<(IExtension Extension, int RegistrationOrder)> result,
        List<ICompositeExtensionFactory> alreadyBuiltServices,
        List<object> factoryOrdering)
        where T : IExtension
    {
        foreach (ICompositeExtensionFactory compositeServiceFactory in compositeFactories)
        {
            // Get the singleton
            var extension = (IExtension)compositeServiceFactory.GetInstance(serviceProvider);
            bool isEnabledAsync = await extension.IsEnabledAsync().ConfigureAwait(false);

            // Check if we have already built the singleton for this composite factory
            if (!alreadyBuiltServices.Contains(compositeServiceFactory))
            {
                // Check if we have already extensions of the same type with same id registered
                result.ValidateUniqueExtension(extension, static x => x.Extension);

                // We initialize only if enabled
                if (isEnabledAsync)
                {
                    await extension.TryInitializeAsync().ConfigureAwait(false);
                }

                // Add to the list of shared singletons
                alreadyBuiltServices.Add(compositeServiceFactory);
            }

            // We register the extension only if enabled
            if (isEnabledAsync)
            {
                if (extension is T typedExtension)
                {
                    // Register the extension for usage
                    result.Add((typedExtension, factoryOrdering.IndexOf(compositeServiceFactory)));
                    serviceProvider.TryAddService(typedExtension);
                }
                else
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.ExtensionDoesNotImplementGivenInterfaceErrorMessage, extension.GetType(), typeof(T)));
                }
            }
        }
    }
}
