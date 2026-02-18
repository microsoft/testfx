// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
using Microsoft.Testing.Platform.Helpers;
#endif

namespace Microsoft.Testing.Platform.Hosts;

// Currently this only wraps the inner host without doing anything extra.
// Should we simply remove this class and use the inner host directly?
internal sealed class TestHostControlledHost(IHost innerHost) : IHost, IDisposable
#if NETCOREAPP
#pragma warning disable SA1001 // Commas should be spaced correctly
    , IAsyncDisposable
#pragma warning restore SA1001 // Commas should be spaced correctly
#endif
{
    private readonly IHost _innerHost = innerHost;

    public async Task<int> RunAsync()
    {
        int exitCode = await _innerHost.RunAsync().ConfigureAwait(false);
        return exitCode;
    }

    public void Dispose()
        => (_innerHost as IDisposable)?.Dispose();

#if NETCOREAPP
    public async ValueTask DisposeAsync()
        => await DisposeHelper.DisposeAsync(_innerHost).ConfigureAwait(false);
#endif
}
