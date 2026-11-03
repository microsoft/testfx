// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Browser.Resources;

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

        try
        {
            Task completed = await Task.WhenAny(browserCompletion, hostExit, browserExit).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return completed == browserCompletion
                ? await browserCompletion.ConfigureAwait(false)
                : throw new BrowserLauncherException(
                    completed == hostExit
                        ? ExtensionResources.BrowserHostExitedBeforeCompletion
                        : ExtensionResources.BrowserExitedBeforeCompletion);
        }
        finally
        {
            await completionCancellationTokenSource.CancelAsync().ConfigureAwait(false);
        }
    }
}
