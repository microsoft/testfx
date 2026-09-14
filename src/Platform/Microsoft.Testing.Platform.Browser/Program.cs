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
                Uri browserUri = BrowserLauncherOptions.ResolveBrowserUri(hostUri, options.UrlPath);
                ChromiumBrowser browser = await ChromiumBrowser.LaunchAsync(
                    options,
                    browserUri,
                    diagnostics,
                    runCancellationTokenSource.Token).ConfigureAwait(false);
                try
                {
                    using var completionCancellationTokenSource = new CancellationTokenSource();
                    Task<int> browserCompletion = browser.WaitForCompletionAsync(
                        options.CompletionTimeout,
                        completionCancellationTokenSource.Token);
                    Task hostExit = host.WaitForExitAsync(completionCancellationTokenSource.Token);
                    Task browserExit = browser.WaitForExitAsync(completionCancellationTokenSource.Token);

                    Task completed = await Task.WhenAny(browserCompletion, hostExit, browserExit).ConfigureAwait(false);
                    if (completed == browserCompletion)
                    {
                        int exitCode = await browserCompletion.ConfigureAwait(false);
                        await completionCancellationTokenSource.CancelAsync().ConfigureAwait(false);
                        return exitCode;
                    }

                    await completionCancellationTokenSource.CancelAsync().ConfigureAwait(false);
                    if (completed == hostExit)
                    {
                        throw new BrowserLauncherException("The browser host exited before the test application completed.");
                    }

                    throw new BrowserLauncherException("The browser exited before the test application completed.");
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
