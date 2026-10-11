// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Browser;

internal static class Program
{
    private const string BrowserHostPath = "/_mtp/browser-host.html";

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

            HostProcess? host = null;
            try
            {
                host = HostProcess.Start(options, diagnostics);
                Uri hostUri = await host.WaitUntilReadyAsync(
                    options.StartupTimeout,
                    runCancellationTokenSource.Token).ConfigureAwait(false);
                var browserUri = new Uri(hostUri, BrowserHostPath);
                options = options with
                {
                    TestApplicationArguments =
                    [
                        .. BrowserHostArguments.Parse(hostUri),
                        .. options.TestApplicationArguments,
                    ],
                };
                ChromiumBrowser browser = await ChromiumBrowser.LaunchAsync(
                    options,
                    browserUri,
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
                if (host is not null)
                {
                    await host.DisposeAsync().ConfigureAwait(false);
                }

                Console.CancelKeyPress -= cancelHandler;
            }
        }
        catch (Exception ex)
        {
            string error = diagnostics?.Redact(ex.ToString()) ?? ex.ToString();
            await Console.Error.WriteLineAsync($"Microsoft.Testing.Platform.Browser: {error}").ConfigureAwait(false);
            if (diagnostics is not null)
            {
                await Console.Error.WriteLineAsync(diagnostics.Format()).ConfigureAwait(false);
            }

            return 1;
        }
    }
}
