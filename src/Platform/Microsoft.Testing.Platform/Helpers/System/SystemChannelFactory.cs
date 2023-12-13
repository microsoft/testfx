// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
using System.Threading.Channels;

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class SystemChannelFactory<T> : IChannelFactory<T>
{
    public IChannel<T> CreateUnbounded(UnboundedChannelOptions options)
    {
        return new SystemChannel<T>(options);
    }
}
#endif
