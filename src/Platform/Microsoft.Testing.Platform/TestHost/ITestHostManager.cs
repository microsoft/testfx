// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHost;

namespace Microsoft.Testing.Platform.TestHost;

public interface ITestHostManager
{
    void AddTestApplicationLifecycleCallbacks(Func<IServiceProvider, ITestApplicationLifecycleCallbacks> testApplicationLifecycleCallbacks);

    void AddDataConsumer(Func<IServiceProvider, IDataConsumer> dataConsumerFactory);

    void AddDataConsumer<T>(CompositeExtensionFactory<T> compositeServiceFactory)
        where T : class, IDataConsumer;

    void AddTestSessionLifetimeHandle(Func<IServiceProvider, ITestSessionLifetimeHandler> testSessionLifetimeHandleFactory);

    void AddTestSessionLifetimeHandle<T>(CompositeExtensionFactory<T> compositeServiceFactory)
        where T : class, ITestSessionLifetimeHandler;
}
