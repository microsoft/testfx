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
        Task<IBrowser>? browserLaunchTask = null;

        try
        {
            // Playwright's DEBUG=pw:channel:send / pw:protocol output contains the complete
            // AddInitScript request, including the SDK bearer token. This is a dedicated launcher
            // process, and its browser host child has already started, so keep DEBUG disabled for
            // the remainder of the launcher lifetime.
            Environment.SetEnvironmentVariable("DEBUG", null);
            EnsurePlaywrightNodeExecutable();
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
            var result = new ChromiumBrowser(playwright, browser, context, page, diagnostics);
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

            await DisposeBrowserAsync(context, browser, playwright, diagnostics).ConfigureAwait(false);
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
            while (true)
            {
                if (!_browser.IsConnected)
                {
                    throw new BrowserLauncherException(
                        "The browser disconnected before the test application completed.");
                }

                string? json = await _page.EvaluateAsync<string?>(
                    "() => JSON.stringify(globalThis.__mtpBrowserResult ?? null)")
                    .WaitAsync(linkedCancellationTokenSource.Token).ConfigureAwait(false);
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
        => await DisposeBrowserAsync(_context, _browser, _playwright, _diagnostics).ConfigureAwait(false);

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

    private static async Task DisposeBrowserAsync(
        IBrowserContext? context,
        IBrowser? browser,
        IPlaywright? playwright,
        DiagnosticBuffer diagnostics)
    {
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

    private static void EnsurePlaywrightNodeExecutable()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string nodePath = GetPlaywrightNodeExecutablePath(
            AppContext.BaseDirectory,
            OperatingSystem.IsLinux() ? OSPlatform.Linux : OSPlatform.OSX,
            RuntimeInformation.ProcessArchitecture);
        if (!File.Exists(nodePath))
        {
            throw new BrowserLauncherException(
                $"The Playwright Node.js driver was not found at '{nodePath}'.");
        }

        UnixFileMode mode = File.GetUnixFileMode(nodePath);
        const UnixFileMode executeMode =
            UnixFileMode.UserExecute
            | UnixFileMode.GroupExecute
            | UnixFileMode.OtherExecute;
        if ((mode & executeMode) != executeMode)
        {
            File.SetUnixFileMode(nodePath, mode | executeMode);
        }
    }

    internal static string GetPlaywrightNodeExecutablePath(
        string baseDirectory,
        OSPlatform operatingSystem,
        Architecture architecture)
    {
        string platformDirectory = (operatingSystem, architecture) switch
        {
            ({ } os, Architecture.X64) when os == OSPlatform.Linux => "linux-x64",
            ({ } os, Architecture.Arm64) when os == OSPlatform.Linux => "linux-arm64",
            ({ } os, Architecture.X64) when os == OSPlatform.OSX => "darwin-x64",
            ({ } os, Architecture.Arm64) when os == OSPlatform.OSX => "darwin-arm64",
            _ => throw new BrowserLauncherException(
                $"Microsoft.Testing.Platform.Browser does not support Playwright on {operatingSystem}/{architecture}."),
        };

        return Path.Combine(baseDirectory, ".playwright", "node", platformDirectory, "node");
    }
}
