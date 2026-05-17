// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Messages;

/// <summary>
/// Represents an item enqueued in an <see cref="AsyncConsumerDataProcessor"/>'s channel.
/// It is either a regular data payload to be consumed, or a drain marker that the consumer
/// completes once it is dequeued so that the corresponding <see cref="AsyncConsumerDataProcessor.DrainDataAsync"/>
/// call can know that every item enqueued before the marker has been processed.
/// </summary>
internal readonly struct AsyncConsumerDataProcessorMessage
{
    private AsyncConsumerDataProcessorMessage(IDataProducer? dataProducer, IData? data, TaskCompletionSource<bool>? drainMarker)
    {
        DataProducer = dataProducer;
        Data = data;
        DrainMarker = drainMarker;
    }

    public IDataProducer? DataProducer { get; }

    public IData? Data { get; }

    public TaskCompletionSource<bool>? DrainMarker { get; }

    public static AsyncConsumerDataProcessorMessage CreateData(IDataProducer dataProducer, IData data)
        => new(dataProducer, data, drainMarker: null);

    public static AsyncConsumerDataProcessorMessage CreateDrainMarker(TaskCompletionSource<bool> drainMarker)
        => new(dataProducer: null, data: null, drainMarker);
}
