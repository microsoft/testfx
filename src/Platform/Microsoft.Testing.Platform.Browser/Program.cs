// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Browser;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        BrowserLauncherOptions? options = null;
        DiagnosticBuffer? diagnostics = null;

        try
        {
            options = BrowserLauncherOptions.Parse(args);
            diagnostics = new DiagnosticBuffer(
                options.Bootstrap.Token,
                options.Bootstrap.Endpoint.AbsoluteUri);

            using var runCancellationTokenSource = new CancellationTokenSource();
            ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                _ = runCancellationTokenSource.CancelAsync();
            };
            Console.CancelKeyPress += cancelHandler;

            var host = HostProcess.Start(options, diagnostics);
            try
            {
                Uri hostUri = await host.WaitUntilReadyAsync(
                    options.StartupTimeout,
                    runCancellationTokenSource.Token).ConfigureAwait(false);
                ChromiumBrowser browser = await ChromiumBrowser.LaunchAsync(
                    options,
                    hostUri,
                    diagnostics,
                    runCancellationTokenSource.Token).ConfigureAwait(false);
                try
                {
                    return await BrowserRunMonitor.WaitAsync(
                        token => browser.WaitForCompletionAsync(options.CompletionTimeout, token),
                        host.WaitForExitAsync,
                        browser.WaitForExitAsync,
                        runCancellationTokenSource.Token).ConfigureAwait(false);
                }
                finally
                {
                    await browser.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                await host.DisposeAsync().ConfigureAwait(false);
                Console.CancelKeyPress -= cancelHandler;
            }
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Microsoft.Testing.Platform.Browser: {ex.Message}").ConfigureAwait(false);
            if (diagnostics is not null)
            {
                await Console.Error.WriteLineAsync(diagnostics.Format()).ConfigureAwait(false);
            }

            return 1;
        }
    }
}
