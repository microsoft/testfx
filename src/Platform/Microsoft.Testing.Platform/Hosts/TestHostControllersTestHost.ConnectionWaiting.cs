// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class TestHostControllersTestHost
{
    internal static async Task<bool> WaitForTestHostControllerConnectionAsync(
        Func<CancellationToken, Task> waitConnectionAsync,
        double timeoutSeconds,
        CancellationToken applicationCancellationToken,
        Func<Task> onTimeoutAsync)
        => await WaitForTestHostControllerConnectionOrProcessExitAsync(
            waitConnectionAsync,
            timeoutSeconds,
            applicationCancellationToken,
            onTimeoutAsync,
            CancellationToken.None,
            onTestHostExitAsync: null).ConfigureAwait(false);

    internal static async Task<bool> WaitForTestHostControllerConnectionOrProcessExitAsync(
        Func<CancellationToken, Task> waitConnectionAsync,
        double timeoutSeconds,
        CancellationToken applicationCancellationToken,
        Func<Task> onTimeoutAsync,
        CancellationToken testHostExitCancellationToken,
        Func<Task>? onTestHostExitAsync)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(
            timeout.Token,
            applicationCancellationToken,
            testHostExitCancellationToken);
        try
        {
            await waitConnectionAsync(linkedToken.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !applicationCancellationToken.IsCancellationRequested)
        {
            await onTimeoutAsync().ConfigureAwait(false);
            return false;
        }
        catch (OperationCanceledException) when (testHostExitCancellationToken.IsCancellationRequested
            && !timeout.IsCancellationRequested
            && !applicationCancellationToken.IsCancellationRequested)
        {
            if (onTestHostExitAsync is not null)
            {
                await onTestHostExitAsync().ConfigureAwait(false);
            }

            return false;
        }
    }
}
