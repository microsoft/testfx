// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed class TestHostControlledHost(NamedPipeClient testHostControllerPipeClient, ITestHost innerTestHost, CancellationToken cancellationToken) : ITestHost, IDisposable
#if NETCOREAPP
#pragma warning disable SA1001 // Commas should be spaced correctly
    , IAsyncDisposable
#pragma warning restore SA1001 // Commas should be spaced correctly
#endif
{
    private readonly NamedPipeClient _namedPipeClient = testHostControllerPipeClient;
    private readonly ITestHost _innerTestHost = innerTestHost;
    private readonly CancellationToken _cancellationToken = cancellationToken;

    public async Task<int> RunAsync()
    {
        int exitCode = await _innerTestHost.RunAsync().ConfigureAwait(false);
        try
        {
            await _namedPipeClient.RequestReplyAsync<TestHostProcessExitRequest, VoidResponse>(new TestHostProcessExitRequest(exitCode), _cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException oc) when (oc.CancellationToken == _cancellationToken)
        {
            // We do nothing we're canceling
        }
        finally
        {
            await DisposeHelper.DisposeAsync(_namedPipeClient).ConfigureAwait(false);
        }

        return exitCode;
    }

    public void Dispose()
    {
        (_innerTestHost as IDisposable)?.Dispose();
        _namedPipeClient.Dispose();
    }

#if NETCOREAPP
    public async ValueTask DisposeAsync()
    {
        await DisposeHelper.DisposeAsync(_innerTestHost).ConfigureAwait(false);
        await _namedPipeClient.DisposeAsync().ConfigureAwait(false);
    }
#endif
}
