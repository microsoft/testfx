// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Browser.Resources;

namespace Microsoft.Testing.Platform.Browser;

internal sealed record BrowserTerminalResult(int ExitCode, string? Error)
{
    public int GetExitCode(DiagnosticBuffer diagnostics)
    {
        if (Error is null)
        {
            return ExitCode;
        }

        diagnostics.Add("browser", Error);
        throw new BrowserLauncherException(ExtensionResources.BrowserSupervisorFatalError);
    }
}

internal static class BrowserBindingCallback
{
    public static T Invoke<T>(
        TaskCompletionSource<BrowserTerminalResult> completion,
        Func<T> callback)
    {
        try
        {
            return callback();
        }
        catch (Exception ex)
        {
            completion.TrySetException(
                new BrowserLauncherException(
                    ExtensionResources.BrowserSupervisorBindingRequestRejected,
                    ex));
            throw;
        }
    }

    public static void Invoke(
        TaskCompletionSource<BrowserTerminalResult> completion,
        Action callback)
        => Invoke(
            completion,
            () =>
            {
                callback();
                return true;
            });
}
