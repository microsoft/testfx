// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Extensions.TestHost;

public interface IDataConsumer : ITestHostExtension
{
    Type[] DataTypesConsumed { get; }

    Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken);
}
