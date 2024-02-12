// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Messages;

/// <summary>
/// Represents a message bus for publishing data.
/// </summary>
public interface IMessageBus
{
    /// <summary>
    /// Publishes the specified data using the provided data producer.
    /// </summary>
    /// <param name="dataProducer">The data producer.</param>
    /// <param name="data">The data to be published.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PublishAsync(IDataProducer dataProducer, IData data);
}
