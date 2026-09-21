// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

using Microsoft.Playwright;

namespace Microsoft.Testing.Platform.Browser;

internal sealed class ChromiumBrowser : IAsyncDisposable
{
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(5);

    private readonly IPlaywright _playwright;
    private readonly IBrowser _browser;
    private readonly IBrowserContext _context;
    private readonly IPage _page;
    private readonly IAsyncDisposable _getArgumentsBinding;
    private readonly IAsyncDisposable _terminalResultBinding;
    private readonly TaskCompletionSource<BrowserTerminalResult> _completion;
    private readonly DiagnosticBuffer _diagnostics;
    private readonly TaskCompletionSource _disconnected = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    private ChromiumBrowser(
        IPlaywright playwright,
        IBrowser browser,
        IBrowserContext context,
        IPage page,
        IAsyncDisposable getArgumentsBinding,
        IAsyncDisposable terminalResultBinding,
        TaskCompletionSource<BrowserTerminalResult> completion,
        DiagnosticBuffer diagnostics)
    {
        _playwright = playwright;
        _browser = browser;
        _context = context;
        _page = page;
        _getArgumentsBinding = getArgumentsBinding;
        _terminalResultBinding = terminalResultBinding;
        _completion = completion;
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
        using var startupTimeoutCancellationTokenSource =
            new CancellationTokenSource(options.StartupTimeout);
        using var startupCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                startupTimeoutCancellationTokenSource.Token);
        CancellationToken startupCancellationToken = startupCancellationTokenSource.Token;

        IPlaywright? playwright = null;
        IBrowser? browser = null;
        IBrowserContext? context = null;
        IAsyncDisposable? getArgumentsBinding = null;
        IAsyncDisposable? terminalResultBinding = null;
        Task<IPlaywright>? playwrightCreationTask = null;
        Task<IBrowser>? browserLaunchTask = null;

        try
        {
            // Playwright's DEBUG channel logs can contain binding payloads with the SDK bearer token.
            Environment.SetEnvironmentVariable("DEBUG", null);
            PlaywrightNodeExecutable.EnsureExecutable();
            playwrightCreationTask = Microsoft.Playwright.Playwright.CreateAsync();
            playwright = await playwrightCreationTask
                .WaitAsync(startupCancellationToken).ConfigureAwait(false);

            browserLaunchTask = playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions
                {
                    ExecutablePath = options.BrowserExecutable,
                    Headless = true,
                    Timeout = (float)options.StartupTimeout.TotalMilliseconds,
                });
            browser = await browserLaunchTask.WaitAsync(startupCancellationToken).ConfigureAwait(false);
            context = await browser.NewContextAsync(
                new BrowserNewContextOptions
                {
                    Locale = "en-US",
                }).WaitAsync(startupCancellationToken).ConfigureAwait(false);
            IPage page = await context.NewPageAsync()
                .WaitAsync(startupCancellationToken).ConfigureAwait(false);

            string expectedOrigin = browserUri.GetLeftPart(UriPartial.Authority);
            var completion = new TaskCompletionSource<BrowserTerminalResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            getArgumentsBinding = await page.ExposeBindingAsync<string[]>(
                "__mtpBrowserGetArguments",
                source => BrowserBindingCallback.Invoke(
                    completion,
                    () =>
                    {
                        ValidateBindingSource(source, page, expectedOrigin);
                        return options.TestApplicationArguments.ToArray();
                    })).WaitAsync(startupCancellationToken).ConfigureAwait(false);
            terminalResultBinding = await page.ExposeBindingAsync<JsonElement>(
                "__mtpBrowserComplete",
                (source, result) => BrowserBindingCallback.Invoke(
                    completion,
                    () =>
                    {
                        ValidateBindingSource(source, page, expectedOrigin);
                        Complete(result, completion);
                    })).WaitAsync(startupCancellationToken).ConfigureAwait(false);

            var chromiumBrowser = new ChromiumBrowser(
                playwright,
                browser,
                context,
                page,
                getArgumentsBinding,
                terminalResultBinding,
                completion,
                diagnostics);
            await chromiumBrowser.NavigateAsync(browserUri, startupCancellationToken)
                .ConfigureAwait(false);
            return chromiumBrowser;
        }
        catch (Exception ex)
        {
            if (playwright is null && playwrightCreationTask is not null)
            {
                playwright = await BoundedResourceCleanup.ObserveCreationAsync(
                    playwrightCreationTask,
                    CleanupTimeout,
                    static instance => instance.Dispose(),
                    message => diagnostics.Add("launcher cleanup", $"Playwright: {message}"))
                    .ConfigureAwait(false);
            }

            if (browser is null && browserLaunchTask is not null)
            {
                browser = await TryObserveBrowserLaunchAsync(browserLaunchTask, diagnostics)
                    .ConfigureAwait(false);
            }

            await DisposeBrowserAsync(
                terminalResultBinding,
                getArgumentsBinding,
                context,
                browser,
                playwright,
                diagnostics).ConfigureAwait(false);

            if (startupTimeoutCancellationTokenSource.IsCancellationRequested
                && !cancellationToken.IsCancellationRequested)
            {
                throw new BrowserLauncherException(
                    $"The Chromium browser did not become ready within {options.StartupTimeout.TotalSeconds} seconds.",
                    ex);
            }

            if (ex is OperationCanceledException or BrowserLauncherException)
            {
                throw;
            }

            throw new BrowserLauncherException(
                $"Unable to launch the Chromium browser '{options.BrowserExecutable}'.",
                ex);
        }
    }

    public async Task<int> WaitForCompletionAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
        using var linkedCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCancellationTokenSource.Token);

        try
        {
            BrowserTerminalResult result = await _completion.Task
                .WaitAsync(linkedCancellationTokenSource.Token).ConfigureAwait(false);
            return result.GetExitCode(_diagnostics);
        }
        catch (OperationCanceledException) when (timeoutCancellationTokenSource.IsCancellationRequested)
        {
            throw new BrowserLauncherException(
                $"The browser test application did not complete within {timeout.TotalSeconds} seconds.");
        }
    }

    public async ValueTask DisposeAsync()
        => await DisposeBrowserAsync(
            _terminalResultBinding,
            _getArgumentsBinding,
            _context,
            _browser,
            _playwright,
            _diagnostics).ConfigureAwait(false);

    private static void Complete(
        JsonElement message,
        TaskCompletionSource<BrowserTerminalResult> completion)
    {
        if (message.ValueKind != JsonValueKind.Object
            || !message.TryGetProperty("exitCode", out JsonElement exitCode)
            || !exitCode.TryGetInt32(out int exitCodeValue))
        {
            throw new BrowserLauncherException(
                "The browser supervisor returned an invalid terminal result.");
        }

        string? error = null;
        if (message.TryGetProperty("error", out JsonElement errorElement)
            && errorElement.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            if (errorElement.ValueKind != JsonValueKind.String)
            {
                throw new BrowserLauncherException(
                    "The browser supervisor returned an invalid error value.");
            }

            error = errorElement.GetString();
        }

        completion.TrySetResult(new BrowserTerminalResult(exitCodeValue, error));
    }

    private static void ValidateBindingSource(
        BindingSource source,
        IPage expectedPage,
        string expectedOrigin)
    {
        if (!ReferenceEquals(source.Page, expectedPage)
            || source.Frame.ParentFrame is not null
            || !Uri.TryCreate(source.Frame.Url, UriKind.Absolute, out Uri? sourceUri)
            || !string.Equals(
                sourceUri.GetLeftPart(UriPartial.Authority),
                expectedOrigin,
                StringComparison.Ordinal))
        {
            throw new BrowserLauncherException(
                "The browser supervisor binding was called outside the expected top-level loopback origin.");
        }
    }

    private async Task NavigateAsync(Uri browserUri, CancellationToken cancellationToken)
    {
        await _page.GotoAsync(
            browserUri.AbsoluteUri,
            new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
            }).WaitAsync(cancellationToken).ConfigureAwait(false);

        if (!Uri.TryCreate(_page.Url, UriKind.Absolute, out Uri? finalUri)
            || !string.Equals(
                finalUri.GetLeftPart(UriPartial.Authority),
                browserUri.GetLeftPart(UriPartial.Authority),
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
        _page.Crash += (_, _) =>
        {
            _diagnostics.Add("browser", "The browser page crashed.");
            _completion.TrySetException(
                new BrowserLauncherException("The browser page crashed before completing."));
        };
    }

    private static async Task DisposeBrowserAsync(
        IAsyncDisposable? terminalResultBinding,
        IAsyncDisposable? getArgumentsBinding,
        IBrowserContext? context,
        IBrowser? browser,
        IPlaywright? playwright,
        DiagnosticBuffer diagnostics)
    {
        await DisposeBindingAsync(terminalResultBinding, "terminal-result", diagnostics)
            .ConfigureAwait(false);
        await DisposeBindingAsync(getArgumentsBinding, "get-arguments", diagnostics)
            .ConfigureAwait(false);

        if (context is not null)
        {
            try
            {
                await context.CloseAsync().WaitAsync(CleanupTimeout).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                diagnostics.Add(
                    "launcher cleanup",
                    $"Unable to close the browser context: {ex.Message}");
            }
        }

        if (browser is not null)
        {
            try
            {
                await browser.CloseAsync().WaitAsync(CleanupTimeout).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                diagnostics.Add(
                    "launcher cleanup",
                    $"Unable to close the browser: {ex.Message}");
            }
        }

        if (playwright is not null)
        {
            await BoundedResourceCleanup.DisposeAsync(
                playwright.Dispose,
                CleanupTimeout,
                message => diagnostics.Add("launcher cleanup", $"Playwright: {message}"))
                .ConfigureAwait(false);
        }
    }

    private static async Task DisposeBindingAsync(
        IAsyncDisposable? binding,
        string name,
        DiagnosticBuffer diagnostics)
    {
        if (binding is null)
        {
            return;
        }

        try
        {
            await binding.DisposeAsync().AsTask().WaitAsync(CleanupTimeout).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            diagnostics.Add(
                "launcher cleanup",
                $"Unable to remove the browser {name} binding: {ex.Message}");
        }
    }

    private static async Task<IBrowser?> TryObserveBrowserLaunchAsync(
        Task<IBrowser> browserLaunchTask,
        DiagnosticBuffer diagnostics)
    {
        try
        {
            return await browserLaunchTask.WaitAsync(CleanupTimeout).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            diagnostics.Add(
                "launcher cleanup",
                $"Unable to observe the interrupted browser launch: {ex.Message}");
            return null;
        }
    }
}
