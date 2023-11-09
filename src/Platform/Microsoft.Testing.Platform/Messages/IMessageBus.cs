// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Messages;

public interface IMessageBus
{
    Task PublishAsync(IDataProducer dataProducer, IData data);
}
