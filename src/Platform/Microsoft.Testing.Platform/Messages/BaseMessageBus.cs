// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Messages;

internal abstract class BaseMessageBus : IMessageBus, IDisposable
{
    public abstract IDataConsumer[] DataConsumerServices { get; }

    /// <summary>
    /// Gets the consumers that may still be executing <see cref="IDataConsumer.ConsumeAsync"/> and must
    /// therefore not be disposed yet.
    /// </summary>
    /// <remarks>
    /// This is normally empty once <see cref="DisableAsync"/> has completed. It is non-empty only when a
    /// consumer outlived the shutdown budget of an aborted run, in which case disposing it would recreate
    /// the very race the handshake exists to prevent, so the platform leaves it to the process exit.
    /// </remarks>
    public virtual IEnumerable<IDataConsumer> ConsumersStillRunning => [];

    public abstract Task InitAsync();

    public abstract Task DrainDataAsync();

    public abstract Task DisableAsync();

    public abstract Task PublishAsync(IDataProducer dataProducer, IData data);

    public abstract void Dispose();
}
