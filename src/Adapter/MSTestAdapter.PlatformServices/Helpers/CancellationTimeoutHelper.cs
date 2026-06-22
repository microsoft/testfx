// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal static class CancellationTimeoutHelper
{
    internal static async SynchronizationContextPreservingTask<TResult> RunWithCooperativeCancellationAsync<TResult>(
        Func<CancellationTokenSource, SynchronizationContextPreservingTask<TResult>> action,
        CancellationTokenSource outerCancellationTokenSource,
        int timeout,
        Func<bool, TResult> failureFactory)
    {
        using CancellationTokenSource timeoutTokenSource = new(timeout);
        using CancellationTokenRegistration registration = timeoutTokenSource.Token.Register(outerCancellationTokenSource.Cancel);

        // A timeout of 0 means "no time to run", so fail immediately without invoking the action. We can't rely on
        // timeoutTokenSource.Token.IsCancellationRequested here: new CancellationTokenSource(0) schedules the
        // cancellation on a timer rather than cancelling synchronously, so the token may not be cancelled yet at
        // this point (a race that intermittently let the action run).
        if (timeout == 0 || timeoutTokenSource.Token.IsCancellationRequested)
        {
            return failureFactory(true);
        }

        try
        {
            return await action(timeoutTokenSource).ConfigureAwait(false);
        }

        // Only OCEs originating from outerCancellationTokenSource.Token are converted to
        // Timeout/Cancelled results. Unrelated OCEs (e.g. from user cleanup code that captures
        // an independent token) are intentionally allowed to propagate; ExecuteInternalAsync
        // handles OCEs from user test code internally.
        catch (Exception ex) when (ex.IsOperationCanceledExceptionFromToken(outerCancellationTokenSource.Token))
        {
            return failureFactory(timeoutTokenSource.Token.IsCancellationRequested);
        }
    }
}
