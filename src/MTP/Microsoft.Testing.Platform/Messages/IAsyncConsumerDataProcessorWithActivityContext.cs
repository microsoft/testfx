// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Telemetry;

namespace Microsoft.Testing.Platform.Messages;

internal interface IAsyncConsumerDataProcessorWithActivityContext : IAsyncConsumerDataProcessor
{
    Task PublishAsync(IDataProducer dataProducer, IData data, PlatformActivityContext? executionActivityContext);
}

internal static class DataConsumerActivityContextExtensions
{
    /// <summary>
    /// Dispatches to <see cref="ITestExecutionActivityContextConsumer.ConsumeAsync"/> when the consumer opted into
    /// receiving the execution activity context, falling back to the plain <see cref="IDataConsumer.ConsumeAsync"/>
    /// otherwise.
    /// </summary>
    public static Task ConsumeWithActivityContextAsync(
        this IDataConsumer dataConsumer,
        IDataProducer dataProducer,
        IData data,
        PlatformActivityContext? executionActivityContext,
        CancellationToken cancellationToken)
        => dataConsumer is ITestExecutionActivityContextConsumer activityContextConsumer
            ? activityContextConsumer.ConsumeAsync(dataProducer, data, executionActivityContext, cancellationToken)
            : dataConsumer.ConsumeAsync(dataProducer, data, cancellationToken);
}
