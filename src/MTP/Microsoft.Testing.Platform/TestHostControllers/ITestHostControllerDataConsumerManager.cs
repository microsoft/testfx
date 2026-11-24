// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;

namespace Microsoft.Testing.Platform.TestHostControllers;

/// <summary>
/// Optional capability of an <see cref="ITestHostControllersManager"/> that supports registering
/// data consumers which observe an already-running test host controller.
/// </summary>
/// <remarks>
/// <para>
/// This is a separate, optional interface rather than a new required member on
/// <see cref="ITestHostControllersManager"/>, so existing implementations of that interface remain compatible.
/// Prefer <see cref="TestHostControllersManagerExtensions.AddDataConsumerWhenControllerStarts(ITestHostControllersManager, Func{IServiceProvider, IDataConsumer})"/>
/// to register a consumer through an <see cref="ITestHostControllersManager"/> reference.
/// </para>
/// <para>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </para>
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ITestHostControllerDataConsumerManager
{
    /// <summary>
    /// Adds a consumer to the test host controller when a controller is required for another reason.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The consumer receives data published on the controller's message bus. It does not receive data
    /// produced in the child test host. Registration does not start a controller process, and the consumer
    /// is not created or invoked when no other extension requires a controller. Delivery is best-effort
    /// during cancellation or controller-finalization timeouts. This method does not provide a run-completion
    /// notification or aggregation across reports or retry attempts. On browser platforms registration is a no-op.
    /// </para>
    /// <para>
    /// This API is experimental. It may change, break, or be removed at any time without notice.
    /// </para>
    /// </remarks>
    /// <param name="factory">The factory method that creates the data consumer.</param>
    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    void AddDataConsumerWhenControllerStarts(Func<IServiceProvider, IDataConsumer> factory);

    /// <summary>
    /// Adds a composite consumer to the test host controller when a controller is required for another reason.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The consumer receives data published on the controller's message bus. It does not receive data
    /// produced in the child test host. Registration does not start a controller process, and the consumer
    /// is not created or invoked when no other extension requires a controller. Delivery is best-effort
    /// during cancellation or controller-finalization timeouts. This method does not provide a run-completion
    /// notification or aggregation across reports or retry attempts. On browser platforms registration is a no-op.
    /// </para>
    /// <para>
    /// This API is experimental. It may change, break, or be removed at any time without notice.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The type of the data consumer.</typeparam>
    /// <param name="compositeServiceFactory">The factory method that creates the composite service.</param>
    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    void AddDataConsumerWhenControllerStarts<T>(CompositeExtensionFactory<T> compositeServiceFactory)
        where T : class, IDataConsumer;
}
