// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Requests;

namespace Microsoft.Testing.Platform.TestHost;

/// <summary>
/// Represents the interface for managing the test host.
/// </summary>
public interface ITestHostManager
{
    /// <summary>
    /// Adds a test application lifecycle callbacks.
    /// </summary>
    /// <param name="testApplicationLifecycleCallbacks">The factory method for creating the test application lifecycle callbacks.</param>
    void AddTestApplicationLifecycleCallbacks(Func<IServiceProvider, ITestApplicationLifecycleCallbacks> testApplicationLifecycleCallbacks);

    /// <summary>
    /// Adds a data consumer.
    /// </summary>
    /// <param name="dataConsumerFactory">The factory method for creating the data consumer.</param>
    void AddDataConsumer(Func<IServiceProvider, IDataConsumer> dataConsumerFactory);

    /// <summary>
    /// Registers a test execution filter.
    /// </summary>
    /// <param name="testFilterFactory">The factory method for creating the a test execution filter.</param>
    void RegisterTestExecutionFilter(Func<IServiceProvider, ITestExecutionFilter> testFilterFactory);

    /// <summary>
    /// Adds a data consumer of type T.
    /// </summary>
    /// <typeparam name="T">The type of the data consumer.</typeparam>
    /// <param name="compositeServiceFactory">The composite extension factory for creating the data consumer.</param>
    void AddDataConsumer<T>(CompositeExtensionFactory<T> compositeServiceFactory)
        where T : class, IDataConsumer;

    /// <summary>
    /// Adds a test session lifetime handle.
    /// </summary>
    /// <param name="testSessionLifetimeHandleFactory">The factory method for creating the test session lifetime handle.</param>
    void AddTestSessionLifetimeHandle(Func<IServiceProvider, ITestSessionLifetimeHandler> testSessionLifetimeHandleFactory);

    /// <summary>
    /// Adds a test session lifetime handle of type T.
    /// </summary>
    /// <typeparam name="T">The type of the test session lifetime handle.</typeparam>
    /// <param name="compositeServiceFactory">The composite extension factory for creating the test session lifetime handle.</param>
    void AddTestSessionLifetimeHandle<T>(CompositeExtensionFactory<T> compositeServiceFactory)
        where T : class, ITestSessionLifetimeHandler;
}
