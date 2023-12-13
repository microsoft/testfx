// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
using System.Threading.Channels;

namespace Microsoft.Testing.Platform.Helpers;

internal interface IChannelFactory<T>
{
    IChannel<T> CreateUnbounded(UnboundedChannelOptions options);
}
#endif
