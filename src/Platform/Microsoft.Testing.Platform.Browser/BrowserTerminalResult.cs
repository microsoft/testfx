// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        throw new BrowserLauncherException(
            "The browser supervisor reported a fatal error.");
    }
}
