// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

internal static class TestRunCancellationTokenExtensions
{
    /// <summary>
    /// Wraps the cancellation token into a task that will complete when cancellation is requested, so we can combine it with Task.WhenAny, to abandon other tasks in the background.
    /// </summary>
    // Returns Task but is not doing any async work, and we should not await getting the Task.
#pragma warning disable VSTHRD200 // Use "Async" suffix for async methods
    public static Task AsTask(this TestRunCancellationToken? testRunCancellationToken)
#pragma warning restore VSTHRD200 // Use "Async" suffix for async methods
    {
        var cancellationSource = new TaskCompletionSource<object>();
        testRunCancellationToken?.Register(() => cancellationSource.TrySetCanceled());

        return cancellationSource.Task;
    }
}
