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
    /// <param name="testHostApplicationLifetimeFactory">The factory method for creating the test host application lifetime callbacks.</param>
    void AddTestHostApplicationLifetime(Func<IServiceProvider, ITestHostApplicationLifetime> testHostApplicationLifetimeFactory);

    /// <summary>
    /// Adds a data consumer.
    /// </summary>
    /// <param name="dataConsumerFactory">The factory method for creating the data consumer.</param>
    void AddDataConsumer(Func<IServiceProvider, IDataConsumer> dataConsumerFactory);

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

    /// <summary>
    /// Adds a test execution filter factory. Multiple filter factories can be registered,
    /// and if more than one filter is enabled, they will be combined using AND logic.
    /// </summary>
    /// <param name="testExecutionFilterFactory">The factory method for creating the test execution filter factory.</param>
    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    void AddTestExecutionFilterFactory(Func<IServiceProvider, ITestExecutionFilterFactory> testExecutionFilterFactory);

    /// <summary>
    /// Adds a request filter provider for server-mode test execution requests.
    /// Multiple providers can be registered and will be evaluated in registration order.
    /// The first provider that can handle the request will create the filter.
    /// </summary>
    /// <param name="requestFilterProvider">The factory method for creating the request filter provider.</param>
    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    void AddRequestFilterProvider(Func<IServiceProvider, IRequestFilterProvider> requestFilterProvider);
}
