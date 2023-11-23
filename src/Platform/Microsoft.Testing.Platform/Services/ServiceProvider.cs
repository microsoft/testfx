// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETCOREAPP
using System.Reflection;
#endif

using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Services;

internal class ServiceProvider : IServiceProvider, ICloneable
{
    private readonly List<object> _services = [];

    internal IReadOnlyCollection<object> Services => _services;

    public bool AllowTestAdapterFrameworkRegistration { get; set; }

    private static Type[] InternalOnlyExtensions => new[]
    {
        // TestHost
        typeof(ITestApplicationLifecycleCallbacks),
        typeof(IDataConsumer),
        typeof(ITestSessionLifetimeHandler),

        // TestHostControllers
        typeof(ITestHostEnvironmentVariableProvider),
        typeof(ITestHostProcessLifetimeHandler),
    };

    public void AddService(object service, bool throwIfSameInstanceExit = true)
    {
        ArgumentGuard.Ensure(
            service is not ITestFramework || AllowTestAdapterFrameworkRegistration,
            nameof(service),
            "You cannot register a test adapter using Services.Add, use instead TestApplicationBuilder.RegisterTestFrameworkAdapter");

        // We don't want to add the same service twice, it's possible when with the CompositeExtensionFactory
        if (_services.Contains(service) && throwIfSameInstanceExit)
        {
            throw new InvalidOperationException($"Instance of type '{service.GetType()}' already added");
        }

        _services.Add(service);
    }

    public void AddServices(object[] services, bool throwIfSameInstanceExit = true)
    {
        foreach (object service in services)
        {
            AddService(service, throwIfSameInstanceExit);
        }
    }

    public bool TryAddService(object service)
    {
        ArgumentGuard.Ensure(
            service is not ITestFramework || AllowTestAdapterFrameworkRegistration,
            nameof(service),
            "You cannot register a test adapter using Services.Add, use instead TestApplicationBuilder.RegisterTestFrameworkAdapter");

        // We don't want to add the same service twice, it's possible when with the CompositeExtensionFactory
        if (_services.Contains(service))
        {
            return false;
        }

        _services.Add(service);
        return true;
    }

    public /* for testing purposes */ virtual IEnumerable<object> GetServicesInternal(
        Type serviceType,
        bool stopAtFirst = false,
        bool skipInternalOnlyExtensions = false)
    {
        if (skipInternalOnlyExtensions && InternalOnlyExtensions.Contains(serviceType))
        {
            yield break;
        }

        foreach (object serviceInstance in _services)
        {
#if !NETCOREAPP
            if (serviceType.GetTypeInfo().IsAssignableFrom(serviceInstance.GetType()))
            {
                yield return serviceInstance;
                if (stopAtFirst)
                {
                    yield break;
                }
            }
#else
            if (serviceInstance.GetType().IsAssignableTo(serviceType))
            {
                yield return serviceInstance;
                if (stopAtFirst)
                {
                    yield break;
                }
            }
#endif
        }
    }

    public object? GetServiceInternal(Type serviceType, bool skipInternalOnlyExtensions = false)
        => GetServicesInternal(serviceType, stopAtFirst: true, skipInternalOnlyExtensions).FirstOrDefault();

    public object Clone(Func<object, bool> filter)
    {
        var clone = new ServiceProvider();
        foreach (object service in _services)
        {
            if (filter(service))
            {
                clone._services.Add(service);
            }
        }

        return clone;
    }

    public object Clone()
    {
        var clone = new ServiceProvider();
        clone._services.AddRange(Services);

        return clone;
    }

    // IServiceProvider
    public object? GetService(Type serviceType)
        => GetServiceInternal(serviceType, skipInternalOnlyExtensions: true);
}
