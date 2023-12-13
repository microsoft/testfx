// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
using System.Threading.Channels;

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class SystemChannel<T> : IChannel<T>
{
    private readonly Channel<T> _channel;

    public SystemChannel(UnboundedChannelOptions options)
    {
        _channel = Channel.CreateUnbounded<T>(options);
    }

    public ValueTask<T> ReadAsync(CancellationToken cancellationToken = default)
        => _channel.Reader.ReadAsync(cancellationToken);

    public bool TryComplete(Exception? ex = null)
        => _channel.Writer.TryComplete(ex);

    public bool TryWrite(T item)
        => _channel.Writer.TryWrite(item);

    public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
        => _channel.Reader.WaitToReadAsync(cancellationToken);
}
#endif
