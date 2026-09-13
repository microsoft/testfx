// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

using Microsoft.Playwright;

namespace Microsoft.Testing.Platform.Browser;

internal sealed class ChromiumBrowser : IAsyncDisposable
{
    private readonly IPlaywright _playwright;
    private readonly IBrowser _browser;
    private readonly IBrowserContext _context;
    private readonly IPage _page;
    private readonly DiagnosticBuffer _diagnostics;
    private readonly TaskCompletionSource _disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private ChromiumBrowser(
        IPlaywright playwright,
        IBrowser browser,
        IBrowserContext context,
        IPage page,
        DiagnosticBuffer diagnostics)
    {
        _playwright = playwright;
        _browser = browser;
        _context = context;
        _page = page;
        _diagnostics = diagnostics;
        _browser.Disconnected += (_, _) => _disconnected.TrySetResult();
        SubscribeToDiagnostics();
    }

    public Task WaitForExitAsync(CancellationToken cancellationToken)
        => _disconnected.Task.WaitAsync(cancellationToken);

    public static async Task<ChromiumBrowser> LaunchAsync(
        BrowserLauncherOptions options,
        Uri browserUri,
        DiagnosticBuffer diagnostics,
        CancellationToken cancellationToken)
    {
        using var startupTimeoutCancellationTokenSource = new CancellationTokenSource(options.StartupTimeout);
        using var startupCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            startupTimeoutCancellationTokenSource.Token);
        CancellationToken startupCancellationToken = startupCancellationTokenSource.Token;

        string executable = BrowserExecutableLocator.Locate(options.BrowserExecutable);
        IPlaywright? playwright = null;
        IBrowser? browser = null;
        IBrowserContext? context = null;

        try
        {
            // Playwright's DEBUG=pw:channel:send / pw:protocol output contains the complete
            // AddInitScript request, including the SDK bearer token. This is a dedicated launcher
            // process, and its browser host child has already started, so keep DEBUG disabled for
            // the remainder of the launcher lifetime.
            Environment.SetEnvironmentVariable("DEBUG", null);
            playwright = await Microsoft.Playwright.Playwright.CreateAsync().ConfigureAwait(false);

            browser = await playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions
                {
                    ExecutablePath = executable,
                    Headless = true,
                    Args = [.. options.BrowserArguments],
                    Timeout = (float)options.StartupTimeout.TotalMilliseconds,
                }).ConfigureAwait(false);
            context = await browser.NewContextAsync(
                new BrowserNewContextOptions
                {
                    Locale = "en-US",
                }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            var result = new ChromiumBrowser(playwright, browser, context, page, diagnostics);
            await result.InitializePageAsync(
                options.TestApplicationArguments,
                browserUri,
                startupCancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
        {
            if (context is not null)
            {
                await context.CloseAsync().ConfigureAwait(false);
            }

            if (browser is not null)
            {
                await browser.CloseAsync().ConfigureAwait(false);
            }

            playwright?.Dispose();
            throw new BrowserLauncherException(
                $"Unable to launch the Chromium browser '{executable}'.",
                ex);
        }
    }

    public async Task<int> WaitForCompletionAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCancellationTokenSource.Token);

        try
        {
            while (true)
            {
                if (!_browser.IsConnected)
                {
                    throw new BrowserLauncherException(
                        "The browser disconnected before the test application completed.");
                }

                string? json = await _page.EvaluateAsync<string?>(
                    "() => JSON.stringify(globalThis.__mtpBrowserResult ?? null)").ConfigureAwait(false);
                if (json is not null and not "null")
                {
                    using var result = JsonDocument.Parse(json);
                    JsonElement root = result.RootElement;
                    if (root.TryGetProperty("completed", out JsonElement completed)
                        && completed.GetBoolean())
                    {
                        if (root.TryGetProperty("error", out JsonElement error))
                        {
                            _diagnostics.Add("browser", error.GetString() ?? error.GetRawText());
                        }

                        return root.GetProperty("exitCode").GetInt32();
                    }
                }

                await Task.Delay(
                    TimeSpan.FromMilliseconds(100),
                    linkedCancellationTokenSource.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (timeoutCancellationTokenSource.IsCancellationRequested)
        {
            throw new BrowserLauncherException(
                $"The browser test application did not complete within {timeout.TotalSeconds} seconds.");
        }
        catch (PlaywrightException ex)
        {
            throw new BrowserLauncherException(
                "Unable to read the browser test application result.",
                ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _context.CloseAsync().ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
        }

        try
        {
            await _browser.CloseAsync().ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
        }

        _playwright.Dispose();
    }

    private async Task InitializePageAsync(
        IReadOnlyList<string> testApplicationArguments,
        Uri browserUri,
        CancellationToken cancellationToken)
    {
        string serializedArguments = JsonSerializer.Serialize(testApplicationArguments);
        string expectedOrigin = browserUri.GetLeftPart(UriPartial.Authority);
        string serializedExpectedOrigin = JsonSerializer.Serialize(expectedOrigin);
        await _page.AddInitScriptAsync(
            $$"""
            if (globalThis.self === globalThis.top && globalThis.location.origin === {{serializedExpectedOrigin}}) {
                Object.defineProperty(globalThis, '__mtpBrowserArguments', { value: Object.freeze({{serializedArguments}}), configurable: false, enumerable: false, writable: false });
            }
            """)
            .WaitAsync(cancellationToken).ConfigureAwait(false);
        await _page.GotoAsync(
            browserUri.AbsoluteUri,
            new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
            }).WaitAsync(cancellationToken).ConfigureAwait(false);

        if (!Uri.TryCreate(_page.Url, UriKind.Absolute, out Uri? finalUri)
            || !string.Equals(
                finalUri.GetLeftPart(UriPartial.Authority),
                expectedOrigin,
                StringComparison.Ordinal))
        {
            throw new BrowserLauncherException(
                "The browser navigated outside the expected loopback origin before the test application started.");
        }
    }

    private void SubscribeToDiagnostics()
    {
        _page.Console += (_, message)
            => _diagnostics.Add($"browser {message.Type}", message.Text);
        _page.PageError += (_, error)
            => _diagnostics.Add("browser exception", error);
        _page.RequestFailed += (_, request)
            => _diagnostics.Add(
                "browser request",
                $"{request.Method} {request.Url}: {request.Failure}");
        _page.Crash += (_, _)
            => _diagnostics.Add("browser", "The browser page crashed.");
    }
}
