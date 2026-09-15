// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Browser;

internal static class BrowserRunMonitor
{
    public static async Task<int> WaitAsync(
        Func<CancellationToken, Task<int>> waitForBrowserCompletion,
        Func<CancellationToken, Task> waitForHostExit,
        Func<CancellationToken, Task> waitForBrowserExit,
        CancellationToken cancellationToken)
    {
        using var completionCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        Task<int> browserCompletion = waitForBrowserCompletion(completionCancellationTokenSource.Token);
        Task hostExit = waitForHostExit(completionCancellationTokenSource.Token);
        Task browserExit = waitForBrowserExit(completionCancellationTokenSource.Token);

        Task completed = await Task.WhenAny(browserCompletion, hostExit, browserExit).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (completed == browserCompletion)
        {
            int exitCode = await browserCompletion.ConfigureAwait(false);
            await completionCancellationTokenSource.CancelAsync().ConfigureAwait(false);
            return exitCode;
        }

        await completionCancellationTokenSource.CancelAsync().ConfigureAwait(false);
        throw new BrowserLauncherException(
            completed == hostExit
                ? "The browser host exited before the test application completed."
                : "The browser exited before the test application completed.");
    }
}
