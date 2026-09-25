// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Telemetry;

namespace Microsoft.Testing.Platform.Messages;

internal interface IAsyncConsumerDataProcessorWithActivityContext : IAsyncConsumerDataProcessor
{
    Task PublishAsync(IDataProducer dataProducer, IData data, PlatformActivityContext? executionActivityContext);
}
