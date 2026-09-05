// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Hosts;

[UnsupportedOSPlatform("browser")]
[StackTraceHidden]
internal sealed class TestHostControlledHost : IHost, IDisposable
#if NETCOREAPP
#pragma warning disable SA1001 // Commas should be spaced correctly
    , IAsyncDisposable
#pragma warning restore SA1001 // Commas should be spaced correctly
#endif
{
    private readonly NamedPipeClient _namedPipeClient;
    private readonly IHost _innerHost;
    private readonly CancellationToken _cancellationToken;
    private readonly TestApplicationResult? _testApplicationResult;
    private TestHostControllerCancellationListener? _testHostControllerCancellationListener;

    public TestHostControlledHost(
        NamedPipeClient testHostControllerPipeClient,
        IHost innerHost,
        CancellationToken cancellationToken)
        : this(testHostControllerPipeClient, innerHost, cancellationToken, testApplicationResult: null)
    {
    }

    public TestHostControlledHost(
        NamedPipeClient testHostControllerPipeClient,
        IHost innerHost,
        CancellationToken cancellationToken,
        TestApplicationResult? testApplicationResult = null)
    {
        _namedPipeClient = testHostControllerPipeClient;
        _innerHost = innerHost;
        _cancellationToken = cancellationToken;
        _testApplicationResult = testApplicationResult;
    }

    public void SetCancellationListener(TestHostControllerCancellationListener? testHostControllerCancellationListener)
        => _testHostControllerCancellationListener = testHostControllerCancellationListener;

    public async Task<int> RunAsync()
    {
        int exitCode = await _innerHost.RunAsync().ConfigureAwait(false);
        using CancellationTokenSource? completionCancellationTokenSource =
            _testHostControllerCancellationListener?.WasCancellationRequestedByController == true
            ? new(ShutdownTimeouts.DefaultControllerFinalization)
            : null;
        try
        {
            int unfilteredExitCode = _testApplicationResult?.GetProcessExitCode() == exitCode
                ? _testApplicationResult.GetProcessExitCodeWithoutIgnore()
                : exitCode;
            await _namedPipeClient.RequestReplyAsync<TestHostCompletedRequest, VoidResponse>(
                new TestHostCompletedRequest(exitCode, unfilteredExitCode),
                completionCancellationTokenSource?.Token ?? _cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException oc) when (
            oc.CancellationToken == _cancellationToken
            || oc.CancellationToken == completionCancellationTokenSource?.Token)
        {
            // We do nothing we're canceling
        }
        finally
        {
            await DisposeHelper.DisposeAsync(_testHostControllerCancellationListener).ConfigureAwait(false);
            await DisposeHelper.DisposeAsync(_namedPipeClient).ConfigureAwait(false);
        }

        return exitCode;
    }

    public void Dispose()
    {
        (_innerHost as IDisposable)?.Dispose();
        _testHostControllerCancellationListener?.Dispose();
        _namedPipeClient.Dispose();
    }

#if NETCOREAPP
    public async ValueTask DisposeAsync()
    {
        await DisposeHelper.DisposeAsync(_innerHost).ConfigureAwait(false);
        await DisposeHelper.DisposeAsync(_testHostControllerCancellationListener).ConfigureAwait(false);
        _namedPipeClient.Dispose();
    }
#endif
}
