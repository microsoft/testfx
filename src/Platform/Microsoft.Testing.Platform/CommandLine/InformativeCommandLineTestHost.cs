// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.IPC;

namespace Microsoft.Testing.Platform.CommandLine;

internal sealed class InformativeCommandLineTestHost(int returnValue, NamedPipeClient? dotnetTestPipe = null) : ITestHost, IDisposable
#if NETCOREAPP
#pragma warning disable SA1001 // Commas should be spaced correctly
    , IAsyncDisposable
#pragma warning restore SA1001 // Commas should be spaced correctly
#endif
{
    private readonly int _returnValue = returnValue;
    private readonly NamedPipeClient? _dotnetTestPipeClient = dotnetTestPipe;

    public Task<int> RunAsync() => Task.FromResult(_returnValue);

    public void Dispose() => _dotnetTestPipeClient?.Dispose();

#if NETCOREAPP
    public async ValueTask DisposeAsync()
    {
        if (_dotnetTestPipeClient is not null)
        {
            await _dotnetTestPipeClient.DisposeAsync();
        }
    }
#endif
}
