// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using System.Threading;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
internal static class TaskHelper
{
    // See https://devblogs.microsoft.com/pfxteam/how-do-i-cancel-non-cancelable-async-operations/
    public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();
        using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
        {
            if (task != await Task.WhenAny(task, tcs.Task))
                throw new OperationCanceledException(cancellationToken);
        }

        return await task;
    }

    public static async Task WithCancellation(this Task task, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();
        using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
        {
            if (task != await Task.WhenAny(task, tcs.Task))
                throw new OperationCanceledException(cancellationToken);
        }

        await task;
    }
}
