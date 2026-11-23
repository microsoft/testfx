// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.TestHostControllers;

/// <summary>
/// Extension methods for <see cref="ITestHostControllersManager"/>.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public static class TestHostControllersManagerExtensions
{
    /// <summary>
    /// Adds a data consumer to the test host controller when a controller is required for another reason.
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
    /// <param name="manager">The test host controllers manager.</param>
    /// <param name="factory">The factory method that creates the data consumer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="manager"/> or <paramref name="factory"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException"><paramref name="manager"/> does not support controller data consumers.</exception>
    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    public static void AddDataConsumerWhenControllerStarts(
        this ITestHostControllersManager manager,
        Func<IServiceProvider, IDataConsumer> factory)
    {
        _ = manager ?? throw new ArgumentNullException(nameof(manager));
        _ = factory ?? throw new ArgumentNullException(nameof(factory));

        if (manager is not ITestHostControllerDataConsumerManager dataConsumerManager)
        {
            throw new NotSupportedException(
                string.Format(CultureInfo.InvariantCulture, PlatformResources.TestHostControllersManagerDoesNotSupportDataConsumersErrorMessage, manager.GetType()));
        }

        dataConsumerManager.AddDataConsumerWhenControllerStarts(factory);
    }

    /// <summary>
    /// Adds a composite data consumer to the test host controller when a controller is required for another reason.
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
    /// <param name="manager">The test host controllers manager.</param>
    /// <param name="compositeServiceFactory">The factory method that creates the composite service.</param>
    /// <exception cref="ArgumentNullException"><paramref name="manager"/> or <paramref name="compositeServiceFactory"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException"><paramref name="manager"/> does not support controller data consumers.</exception>
    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    public static void AddDataConsumerWhenControllerStarts<T>(
        this ITestHostControllersManager manager,
        CompositeExtensionFactory<T> compositeServiceFactory)
        where T : class, IDataConsumer
    {
        _ = manager ?? throw new ArgumentNullException(nameof(manager));
        _ = compositeServiceFactory ?? throw new ArgumentNullException(nameof(compositeServiceFactory));

        if (manager is not ITestHostControllerDataConsumerManager dataConsumerManager)
        {
            throw new NotSupportedException(
                string.Format(CultureInfo.InvariantCulture, PlatformResources.TestHostControllersManagerDoesNotSupportDataConsumersErrorMessage, manager.GetType()));
        }

        dataConsumerManager.AddDataConsumerWhenControllerStarts(compositeServiceFactory);
    }
}
