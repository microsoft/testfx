// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Extensions;

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

    public CompositeExtensionFactory(Func<IServiceProvider, TExtension> factory)
    {
        ArgumentGuard.IsNotNull(factory, nameof(factory));
        _factoryWithServiceProvider = factory;
    }

    public CompositeExtensionFactory(Func<TExtension> factory)
    {
        ArgumentGuard.IsNotNull(factory, nameof(factory));
        _factory = factory;
    }

    object ICloneable.Clone()
        => _factory is not null
            ? new CompositeExtensionFactory<TExtension>(_factory)
            : new CompositeExtensionFactory<TExtension>(_factoryWithServiceProvider!);

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

    private bool ContainsTestHostExtension()
        => _instance is IDataConsumer or ITestSessionLifetimeHandler;

    private bool ContainsTestHostControllerExtension()
        => _instance is ITestHostProcessLifetimeHandler or ITestHostEnvironmentVariableProvider;
}
