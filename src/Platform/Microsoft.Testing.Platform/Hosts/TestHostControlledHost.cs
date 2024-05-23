// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.Hosts;

internal class TestHostControlledHost(NamedPipeClient namedPipeClient, ITestHost innerTestHost, CancellationToken cancellationToken) : ITestHost, IDisposable
#if NETCOREAPP
#pragma warning disable SA1001 // Commas should be spaced correctly
    , IAsyncDisposable
#pragma warning restore SA1001 // Commas should be spaced correctly
#endif
{
    public async Task<int> RunAsync()
    {
        int exitCode = await innerTestHost.RunAsync();
        try
        {
            await namedPipeClient.RequestReplyAsync<TestHostProcessExitRequest, VoidResponse>(new TestHostProcessExitRequest(exitCode), cancellationToken);
        }
        catch (OperationCanceledException oc) when (oc.CancellationToken == cancellationToken)
        {
            // We do nothing we're cancelling
        }
        finally
        {
            await DisposeHelper.DisposeAsync(namedPipeClient);
        }

        return exitCode;
    }

    public void Dispose()
    {
        (innerTestHost as IDisposable)?.Dispose();
        namedPipeClient.Dispose();
    }

#if NETCOREAPP
    public async ValueTask DisposeAsync()
    {
        await DisposeHelper.DisposeAsync(innerTestHost);
        await namedPipeClient.DisposeAsync();
    }
#endif
}
