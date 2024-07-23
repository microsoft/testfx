// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;

namespace Microsoft.Testing.Platform.Messages;

internal abstract class BaseMessageBus : IMessageBus, IDisposable
{
    public abstract IReadOnlyList<IDataConsumer> DataConsumerServices { get; }

    public abstract Task InitAsync();

    public abstract Task DrainDataAsync();

    public abstract Task DisableAsync();

    public abstract Task PublishAsync(IDataProducer dataProducer, IData data);

    public abstract void Dispose();
}
