// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
using System.Threading.Channels;
#endif

namespace Microsoft.Testing.Platform.Helpers;

internal interface IProducerConsumerFactory<T>
{
#if NETCOREAPP
    IChannel<T> Create(UnboundedChannelOptions options);
#else
    IBlockingCollection<T> Create();
#endif
}
