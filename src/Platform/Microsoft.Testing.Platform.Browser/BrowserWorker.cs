// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Browser.Resources;

namespace Microsoft.Testing.Platform.Browser;

internal static class BrowserWorker
{
    private const string BrowserHostPath = "/_mtp/browser-host.html";

    public static async Task<int> RunAsync(string[] args)
    {
        BrowserLauncherOptions? options = null;
        DiagnosticBuffer? diagnostics = null;

        try
        {
            WorkerProcessContainment.InitializeWorkerProcessGroup();
            await Console.Out.WriteLineAsync(BrowserSupervisor.WorkerReadyMarker).ConfigureAwait(false);
            await Console.Out.FlushAsync().ConfigureAwait(false);

            string? startCommand = await Console.In.ReadLineAsync().ConfigureAwait(false);
            if (startCommand != BrowserSupervisor.StartWorkerCommand)
            {
                throw new BrowserLauncherException(ExtensionResources.BrowserWorkerAuthorizationMissing);
            }

            using var runCancellationTokenSource = new CancellationTokenSource();
            WorkerParentMonitor.Start(runCancellationTokenSource);
            ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                _ = runCancellationTokenSource.CancelAsync();
            };
            Console.CancelKeyPress += cancelHandler;

            try
            {
                options = BrowserLauncherOptions.Parse(args);
                diagnostics = new DiagnosticBuffer(
                    options.Bootstrap.Token,
                    options.Bootstrap.Endpoint.AbsoluteUri);

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
                }
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }
        catch (Exception ex)
        {
            string error = DiagnosticBuffer.FormatException(ex, diagnostics);
            await Console.Error.WriteLineAsync(
                string.Format(
                    CultureInfo.CurrentCulture,
                    ExtensionResources.ErrorPrefix,
                    error)).ConfigureAwait(false);
            if (diagnostics is not null)
            {
                await Console.Error.WriteLineAsync(diagnostics.Format()).ConfigureAwait(false);
            }

            return 1;
        }
    }
}
