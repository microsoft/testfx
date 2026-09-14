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
    private readonly IAsyncDisposable _completionBinding;
    private readonly IAsyncDisposable _fatalErrorBinding;
    private readonly TaskCompletionSource<int> _completion;
    private readonly DiagnosticBuffer _diagnostics;
    private readonly TaskCompletionSource _disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private ChromiumBrowser(
        IPlaywright playwright,
        IBrowser browser,
        IBrowserContext context,
        IPage page,
        IAsyncDisposable completionBinding,
        IAsyncDisposable fatalErrorBinding,
        TaskCompletionSource<int> completion,
        DiagnosticBuffer diagnostics)
    {
        _playwright = playwright;
        _browser = browser;
        _context = context;
        _page = page;
        _completionBinding = completionBinding;
        _fatalErrorBinding = fatalErrorBinding;
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
        using var startupTimeoutCancellationTokenSource = new CancellationTokenSource(options.StartupTimeout);
        using var startupCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            startupTimeoutCancellationTokenSource.Token);
        CancellationToken startupCancellationToken = startupCancellationTokenSource.Token;

        string executable = BrowserExecutableLocator.Locate(options.BrowserExecutable);
        IPlaywright? playwright = null;
        IBrowser? browser = null;
        IBrowserContext? context = null;
        IAsyncDisposable? completionBinding = null;
        IAsyncDisposable? fatalErrorBinding = null;
        Task<IBrowser>? browserLaunchTask = null;

        try
        {
            // Playwright's DEBUG=pw:channel:send / pw:protocol output contains the complete
            // AddInitScript request, including the SDK bearer token. This is a dedicated launcher
            // process, and its browser host child has already started, so keep DEBUG disabled for
            // the remainder of the launcher lifetime.
            Environment.SetEnvironmentVariable("DEBUG", null);
            PlaywrightNodeExecutable.EnsureExecutable();
            playwright = await Microsoft.Playwright.Playwright.CreateAsync().ConfigureAwait(false);

            browserLaunchTask = playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions
                {
                    ExecutablePath = executable,
                    Headless = true,
                    Args = [.. options.BrowserArguments],
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
            var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            string expectedOrigin = browserUri.GetLeftPart(UriPartial.Authority);
            completionBinding = await page.ExposeBindingAsync<JsonElement>(
                "__mtpBrowserCompleteV1",
                (source, message) => CompleteBrowserRun(source, page, expectedOrigin, message, completion))
                .WaitAsync(startupCancellationToken).ConfigureAwait(false);
            fatalErrorBinding = await page.ExposeBindingAsync<JsonElement>(
                "__mtpBrowserFatalErrorV1",
                (source, message) => ReportBrowserFatalError(
                    source,
                    page,
                    expectedOrigin,
                    message,
                    completion,
                    diagnostics))
                .WaitAsync(startupCancellationToken).ConfigureAwait(false);
            var result = new ChromiumBrowser(
                playwright,
                browser,
                context,
                page,
                completionBinding,
                fatalErrorBinding,
                completion,
                diagnostics);
            await result.InitializePageAsync(
                options.TestApplicationArguments,
                browserUri,
                startupCancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            if (browser is null && browserLaunchTask is not null)
            {
                browser = await TryObserveBrowserLaunchAsync(browserLaunchTask, diagnostics).ConfigureAwait(false);
            }

            await DisposeBrowserAsync(
                completionBinding,
                fatalErrorBinding,
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
            return await _completion.Task
                .WaitAsync(linkedCancellationTokenSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCancellationTokenSource.IsCancellationRequested)
        {
            throw new BrowserLauncherException(
                $"The browser test application did not complete within {timeout.TotalSeconds} seconds.");
        }
    }

    public async ValueTask DisposeAsync()
        => await DisposeBrowserAsync(
            _completionBinding,
            _fatalErrorBinding,
            _context,
            _browser,
            _playwright,
            _diagnostics).ConfigureAwait(false);

    private async Task InitializePageAsync(
        IReadOnlyList<string> testApplicationArguments,
        Uri browserUri,
        CancellationToken cancellationToken)
    {
        string expectedOrigin = browserUri.GetLeftPart(UriPartial.Authority);
        string serializedArguments = JsonSerializer.Serialize(testApplicationArguments);
        string serializedExpectedOrigin = JsonSerializer.Serialize(expectedOrigin);
        await _page.AddInitScriptAsync(
            $$"""
            if (globalThis.self === globalThis.top && globalThis.location.origin === {{serializedExpectedOrigin}}) {
                const argumentsFromLauncher = Object.freeze({{serializedArguments}});
                const completeTransport = globalThis.__mtpBrowserCompleteV1;
                const fatalErrorTransport = globalThis.__mtpBrowserFatalErrorV1;
                let completed = false;
                const api = Object.freeze({
                    contractVersion: 1,
                    getArguments() {
                        return Object.freeze([...argumentsFromLauncher]);
                    },
                    complete(exitCode) {
                        if (completed) {
                            throw new Error('testingPlatformBrowser.complete can only be called once.');
                        }
                        if (!Number.isInteger(exitCode)) {
                            throw new TypeError('testingPlatformBrowser.complete requires an integer exitCode.');
                        }

                        completed = true;
                        void completeTransport({
                            contractVersion: 1,
                            exitCode,
                        });
                    },
                    reportFatalError(error) {
                        if (completed) {
                            throw new Error('testingPlatformBrowser has already completed.');
                        }
                        if (typeof error !== 'string' || error.length === 0) {
                            throw new TypeError('testingPlatformBrowser.reportFatalError requires a non-empty error string.');
                        }

                        completed = true;
                        void fatalErrorTransport({
                            contractVersion: 1,
                            error,
                        });
                    },
                });
                Object.defineProperty(globalThis, 'testingPlatformBrowser', { value: api, configurable: false, enumerable: true, writable: false });
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

    private static async Task DisposeBrowserAsync(
        IAsyncDisposable? completionBinding,
        IAsyncDisposable? fatalErrorBinding,
        IBrowserContext? context,
        IBrowser? browser,
        IPlaywright? playwright,
        DiagnosticBuffer diagnostics)
    {
        if (fatalErrorBinding is not null)
        {
            try
            {
                await fatalErrorBinding.DisposeAsync().AsTask().WaitAsync(CleanupTimeout).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                diagnostics.Add("launcher cleanup", $"Unable to remove the browser fatal-error binding: {ex.Message}");
            }
        }

        if (completionBinding is not null)
        {
            try
            {
                await completionBinding.DisposeAsync().AsTask().WaitAsync(CleanupTimeout).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                diagnostics.Add("launcher cleanup", $"Unable to remove the browser completion binding: {ex.Message}");
            }
        }

        if (context is not null)
        {
            try
            {
                await context.CloseAsync().WaitAsync(CleanupTimeout).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                diagnostics.Add("launcher cleanup", $"Unable to close the browser context: {ex.Message}");
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
                diagnostics.Add("launcher cleanup", $"Unable to close the browser: {ex.Message}");
            }
        }

        try
        {
            playwright?.Dispose();
        }
        catch (Exception ex)
        {
            diagnostics.Add("launcher cleanup", $"Unable to dispose Playwright: {ex.Message}");
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
            diagnostics.Add("launcher cleanup", $"Unable to observe the interrupted browser launch: {ex.Message}");
            return null;
        }
    }

    private static bool CompleteBrowserRun(
        BindingSource source,
        IPage expectedPage,
        string expectedOrigin,
        JsonElement message,
        TaskCompletionSource<int> completion)
    {
        ValidateBrowserApiSource(source, expectedPage, expectedOrigin);

        return message.ValueKind == JsonValueKind.Object
            && message.TryGetProperty("contractVersion", out JsonElement contractVersion)
            && contractVersion.ValueKind == JsonValueKind.Number
            && contractVersion.GetInt32() == 1
            && message.TryGetProperty("exitCode", out JsonElement exitCode)
            && exitCode.ValueKind == JsonValueKind.Number
            && exitCode.TryGetInt32(out int exitCodeValue)
                ? completion.TrySetResult(exitCodeValue)
                : throw new BrowserLauncherException(
                    "The browser completion API received an invalid version 1 payload.");
    }

    private static bool ReportBrowserFatalError(
        BindingSource source,
        IPage expectedPage,
        string expectedOrigin,
        JsonElement message,
        TaskCompletionSource<int> completion,
        DiagnosticBuffer diagnostics)
    {
        ValidateBrowserApiSource(source, expectedPage, expectedOrigin);

        if (message.ValueKind != JsonValueKind.Object
            || !message.TryGetProperty("contractVersion", out JsonElement contractVersion)
            || contractVersion.ValueKind != JsonValueKind.Number
            || contractVersion.GetInt32() != 1
            || !message.TryGetProperty("error", out JsonElement error)
            || error.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(error.GetString()))
        {
            throw new BrowserLauncherException(
                "The browser fatal-error API received an invalid version 1 payload.");
        }

        diagnostics.Add("browser fatal", error.GetString()!);
        return completion.TrySetException(
            new BrowserLauncherException("The browser page reported a fatal integration error."));
    }

    private static void ValidateBrowserApiSource(
        BindingSource source,
        IPage expectedPage,
        string expectedOrigin)
        => _ = ReferenceEquals(source.Page, expectedPage)
            && source.Frame.ParentFrame is null
            && Uri.TryCreate(source.Frame.Url, UriKind.Absolute, out Uri? sourceUri)
            && string.Equals(
                sourceUri.GetLeftPart(UriPartial.Authority),
                expectedOrigin,
                StringComparison.Ordinal)
                ? true
                : throw new BrowserLauncherException(
                    "The browser page API was called outside the expected top-level loopback origin.");
}
