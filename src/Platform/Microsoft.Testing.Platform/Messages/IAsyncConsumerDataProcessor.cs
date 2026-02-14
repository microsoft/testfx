// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Messages;

internal interface IAsyncConsumerDataProcessor : IDisposable
{
    IDataConsumer DataConsumer { get; }

    Task CompleteAddingAsync();

    Task<long> DrainDataAsync();

    Task PublishAsync(IDataProducer dataProducer, IData data);
}
