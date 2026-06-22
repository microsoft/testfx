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
        if (timeoutTokenSource.Token.IsCancellationRequested)
        {
            return failureFactory(true);
        }

        try
        {
            return await action(timeoutTokenSource).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex.IsOperationCanceledExceptionFromToken(outerCancellationTokenSource.Token))
        {
            // Timeout cancellation is propagated through the outer cancellation token source, so only cancellations
            // that originate from that token are converted into timeout/cancelled failures.
            return failureFactory(timeoutTokenSource.Token.IsCancellationRequested);
        }
    }
}
