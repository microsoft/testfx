// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Messages;

internal interface IAsyncConsumerDataProcessor : IDisposable
{
    IDataConsumer DataConsumer { get; }

    /// <summary>
    /// Gets the total number of data payloads enqueued through <see cref="PublishAsync"/>.
    /// The message bus uses this to detect publisher/consumer cycles across drain rounds:
    /// if a round of drains increases this counter on any processor, another round is needed.
    /// </summary>
    long ReceivedCount { get; }

    Task CompleteAddingAsync();

    /// <summary>
    /// Waits for the consumer to finish processing every item that was already enqueued in the
    /// processor's channel at the moment this method is invoked.
    /// </summary>
    Task DrainDataAsync();

    Task PublishAsync(IDataProducer dataProducer, IData data);
}
