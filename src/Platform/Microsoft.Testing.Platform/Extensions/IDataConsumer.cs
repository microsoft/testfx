// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Extensions;

/// <summary>
/// Represents a data consumer that can consume data produced by a data producer.
/// </summary>
/// <remarks>
/// The message bus does not dispatch data to a consumer when the consumer and producer have the same
/// <see cref="IExtension.Uid"/>. Producer and consumer registrations that communicate through the message bus
/// must use different UIDs. An extension that processes its own data should invoke that processing directly.
/// </remarks>
public interface IDataConsumer : IExtension
{
    /// <summary>
    /// Gets the types of data consumed by the data consumer.
    /// </summary>
    // NOTE: We don't use IReadOnlyCollection because we don't have cross api(like Contains) that are good in every tfm.
    // Internally we use Array.IndexOf to verify if the data type is supported, it's a hot path.
    Type[] DataTypesConsumed { get; }

    /// <summary>
    /// Consumes the specified data produced by a data producer.
    /// </summary>
    /// <param name="dataProducer">The data producer.</param>
    /// <param name="value">The data to be consumed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken);
}
