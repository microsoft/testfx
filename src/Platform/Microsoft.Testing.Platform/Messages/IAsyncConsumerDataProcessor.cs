// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Messages;

internal interface IAsyncConsumerDataProcessor : IDisposable
{
    IDataConsumer DataConsumer { get; }

    Task CompleteAddingAsync();

    /// <summary>
    /// Drains any items that have been enqueued before this call so that the caller can synchronize with the consumer.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when at least one data payload was processed during the drain; otherwise <see langword="false"/>.
    /// The message bus uses this to detect publisher/consumer cycles.
    /// </returns>
    Task<bool> DrainDataAsync();

    Task PublishAsync(IDataProducer dataProducer, IData data);
}
