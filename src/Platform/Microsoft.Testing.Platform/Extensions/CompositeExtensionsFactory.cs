// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;

namespace Microsoft.Testing.Platform.Extensions;

/// <summary>
/// Represents a factory for creating composite extensions.
/// </summary>
/// <typeparam name="TExtension">The type of the extension.</typeparam>
/// <remarks>
/// This helper type is used to create a composite extension that is composed of multiple extensions without having to
/// handle either the communication between the extensions or the lifetime of the extensions instances.
/// </remarks>
public class CompositeExtensionFactory<TExtension> : ICompositeExtensionFactory, ICloneable
    where TExtension : class, IExtension
{
    private readonly object _syncLock = new();
    private readonly Func<IServiceProvider, TExtension>? _factoryWithServiceProvider;
    private readonly Func<TExtension>? _factory;
    private TExtension? _instance;

    internal const /* for testing */ string ValidateCompositionErrorMessage =
"""
You cannot compose extensions that belong to different areas.
Valid composition are:
TestHostControllers: ITestHostProcessLifetimeHandler, ITestHostEnvironmentVariableProvider
TestHost: IDataConsumer, ITestApplicationLifetime
""";

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeExtensionFactory{TExtension}"/> class.
    /// </summary>
    /// <param name="factory">The factory function that creates the extension with a service provider.</param>
    public CompositeExtensionFactory(Func<IServiceProvider, TExtension> factory)
    {
        Guard.NotNull(factory);
        _factoryWithServiceProvider = factory;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeExtensionFactory{TExtension}"/> class.
    /// </summary>
    /// <param name="factory">The factory function that creates the extension.</param>
    public CompositeExtensionFactory(Func<TExtension> factory)
    {
        Guard.NotNull(factory);
        _factory = factory;
    }

    /// <inheritdoc/>
    object ICloneable.Clone()
        => _factory is not null
            ? new CompositeExtensionFactory<TExtension>(_factory)
            : new CompositeExtensionFactory<TExtension>(_factoryWithServiceProvider!);

    /// <inheritdoc/>
    object ICompositeExtensionFactory.GetInstance(IServiceProvider? serviceProvider)
    {
        lock (_syncLock)
        {
            if (Volatile.Read(ref _instance) is null)
            {
                if (_factoryWithServiceProvider is not null)
                {
                    Volatile.Write(ref _instance, _factoryWithServiceProvider(serviceProvider!));
                }

                if (_factory is not null)
                {
                    Volatile.Write(ref _instance, _factory());
                }

                if (_instance is null)
                {
                    throw new InvalidOperationException("Initialization failed");
                }
            }
        }

        ValidateComposition();

        return _instance!;
    }

    private void ValidateComposition()
    {
        if (ContainsTestHostExtension() && ContainsTestHostControllerExtension())
        {
            throw new InvalidOperationException(ValidateCompositionErrorMessage);
        }
    }

    private bool ContainsTestHostExtension() => _instance is ITestSessionLifetimeHandler;

    private bool ContainsTestHostControllerExtension() => _instance is ITestHostProcessLifetimeHandler or ITestHostEnvironmentVariableProvider;
}
