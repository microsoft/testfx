// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;

namespace Microsoft.Testing.Platform.TestHostControllers;

/// <summary>
/// Represents a manager for test host controllers.
/// </summary>
public interface ITestHostControllersManager
{
    /// <summary>
    /// Adds an environment variable provider to the test host controller manager.
    /// </summary>
    /// <param name="environmentVariableProviderFactory">The factory method that creates the environment variable provider.</param>
    void AddEnvironmentVariableProvider(Func<IServiceProvider, ITestHostEnvironmentVariableProvider> environmentVariableProviderFactory);

    /// <summary>
    /// Adds an environment variable provider to the test host controller manager.
    /// </summary>
    /// <typeparam name="T">The type of the environment variable provider.</typeparam>
    /// <param name="compositeServiceFactory">The factory method that creates the composite service.</param>
    void AddEnvironmentVariableProvider<T>(CompositeExtensionFactory<T> compositeServiceFactory)
        where T : class, ITestHostEnvironmentVariableProvider;

    /// <summary>
    /// Adds a process lifetime handler to the test host controller manager.
    /// </summary>
    /// <param name="lifetimeHandler">The factory method that creates the process lifetime handler.</param>
    void AddProcessLifetimeHandler(Func<IServiceProvider, ITestHostProcessLifetimeHandler> lifetimeHandler);

    /// <summary>
    /// Adds a process lifetime handler to the test host controller manager.
    /// </summary>
    /// <typeparam name="T">The type of the process lifetime handler.</typeparam>
    /// <param name="compositeServiceFactory">The factory method that creates the composite service.</param>
    void AddProcessLifetimeHandler<T>(CompositeExtensionFactory<T> compositeServiceFactory)
        where T : class, ITestHostProcessLifetimeHandler;
}
