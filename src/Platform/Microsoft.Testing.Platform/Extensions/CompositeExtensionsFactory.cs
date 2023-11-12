// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Extensions;

public class CompositeExtensionFactory<T> : ICompositeExtensionFactory, ICloneable
    where T : class, IExtension
{
    private readonly object _syncLock = new();
    private readonly Func<IServiceProvider, T>? _factoryWithServiceProvider;
    private readonly Func<T>? _factory;
    private T? _instance;

    internal const /* for testing */ string ValidateCompositionErrorMessage =
"""
You cannot compose extensions that belong to different areas.
Valid composition are:
TestHostControllers: ITestHostProcessLifetimeHandler, ITestHostEnvironmentVariableProvider
TestHost: IDataConsumer, ITestApplicationLifetime
""";

    public CompositeExtensionFactory(Func<IServiceProvider, T> factory)
    {
        _factoryWithServiceProvider = factory;
    }

    public CompositeExtensionFactory(Func<T> factory)
    {
        _factory = factory;
    }

    object ICloneable.Clone() => _factory is null && _factoryWithServiceProvider is null
            ? throw new InvalidOperationException("At least one factory must be not null")
            : (object)(_factory is not null
            ? new CompositeExtensionFactory<T>(_factory)
            : new CompositeExtensionFactory<T>(_factoryWithServiceProvider ?? throw new InvalidOperationException("Unexpected null _factoryWithServiceProvider")));

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

    private bool ContainsTestHostExtension() => _instance is IDataConsumer or ITestSessionLifetimeHandler;

    private bool ContainsTestHostControllerExtension() => _instance is ITestHostProcessLifetimeHandler or ITestHostEnvironmentVariableProvider;
}
