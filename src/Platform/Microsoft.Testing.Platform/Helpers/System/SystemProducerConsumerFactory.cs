// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
using System.Threading.Channels;
#endif

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class SystemProducerConsumerFactory<T> : IProducerConsumerFactory<T>
{
#if NETCOREAPP
    public IChannel<T> Create(UnboundedChannelOptions options)
        => new SystemChannel<T>(options);
#else
    public IBlockingCollection<T> Create()
        => new SystemBlockingCollection<T>();
#endif
}
