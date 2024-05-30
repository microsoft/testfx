// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.IPC;

namespace Microsoft.Testing.Platform.CommandLine;

internal sealed class InformativeCommandLineTestHost(int returnValue, NamedPipeClient? namedPipeClient = null) : ITestHost, IDisposable
#if NETCOREAPP
#pragma warning disable SA1001 // Commas should be spaced correctly
    , IAsyncDisposable
#pragma warning restore SA1001 // Commas should be spaced correctly
#endif
{
    private readonly int _returnValue = returnValue;
    private readonly NamedPipeClient? _namedPipeClient = namedPipeClient;

    public Task<int> RunAsync() => Task.FromResult(_returnValue);

    public void Dispose() => _namedPipeClient?.Dispose();

#if NETCOREAPP
    public async ValueTask DisposeAsync()
    {
        if (_namedPipeClient is not null)
        {
            await _namedPipeClient.DisposeAsync();
        }
    }
#endif
}
