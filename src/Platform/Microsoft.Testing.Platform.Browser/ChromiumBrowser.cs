// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Testing.Platform.Browser;

internal sealed class ChromiumBrowser : IAsyncDisposable
{
    private readonly Process _process;
    private readonly string _profileDirectory;
    private readonly DiagnosticBuffer _diagnostics;
    private readonly DevToolsConnection _devToolsConnection;
    private readonly CancellationTokenSource _captureCancellationTokenSource;
    private readonly Task _stdoutTask;
    private readonly Task _stderrTask;

    private ChromiumBrowser(
        Process process,
        string profileDirectory,
        DiagnosticBuffer diagnostics,
        DevToolsConnection devToolsConnection,
        CancellationTokenSource captureCancellationTokenSource,
        Task stdoutTask,
        Task stderrTask)
    {
        _process = process;
        _profileDirectory = profileDirectory;
        _diagnostics = diagnostics;
        _devToolsConnection = devToolsConnection;
        _captureCancellationTokenSource = captureCancellationTokenSource;
        _stdoutTask = stdoutTask;
        _stderrTask = stderrTask;
    }

    public Task WaitForExitAsync(CancellationToken cancellationToken)
        => _process.WaitForExitAsync(cancellationToken);

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
        string profileDirectory = Path.Combine(Path.GetTempPath(), $"mtp-browser-profile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(profileDirectory);

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (string argument in new[]
        {
            "--headless=new",
            "--disable-background-networking",
            "--disable-component-update",
            "--disable-default-apps",
            "--disable-dev-shm-usage",
            "--disable-extensions",
            "--disable-features=Translate",
            "--disable-sync",
            "--metrics-recording-only",
            "--no-first-run",
            "--no-default-browser-check",
            "--remote-debugging-port=0",
            $"--user-data-dir={profileDirectory}",
            "about:blank",
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (!OperatingSystem.IsWindows() && IsRunningInContainer())
        {
            startInfo.ArgumentList.Add("--no-sandbox");
        }

        foreach (string argument in options.BrowserArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        Process process;
        try
        {
            process = Process.Start(startInfo)
                ?? throw new BrowserLauncherException($"The browser executable '{executable}' did not start.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw new BrowserLauncherException($"Unable to start the browser executable '{executable}'.", ex);
        }

        var captureCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task stdoutTask = CaptureAsync(
            process.StandardOutput,
            "browser stdout",
            diagnostics,
            captureCancellationTokenSource.Token);
        Task stderrTask = CaptureAsync(
            process.StandardError,
            "browser stderr",
            diagnostics,
            captureCancellationTokenSource.Token);

        try
        {
            Uri devToolsEndpoint = await WaitForPageEndpointAsync(
                process,
                profileDirectory,
                options.StartupTimeout,
                startupCancellationToken).ConfigureAwait(false);

            var connection = new DevToolsConnection();
            await connection.ConnectAsync(devToolsEndpoint, startupCancellationToken).ConfigureAwait(false);
            var browser = new ChromiumBrowser(
                process,
                profileDirectory,
                diagnostics,
                connection,
                captureCancellationTokenSource,
                stdoutTask,
                stderrTask);
            browser.SubscribeToDiagnostics();
            await browser.InitializePageAsync(
                options.TestApplicationArguments,
                browserUri,
                startupCancellationToken).ConfigureAwait(false);
            return browser;
        }
        catch
        {
            await captureCancellationTokenSource.CancelAsync().ConfigureAwait(false);
            KillProcess(process);
            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
            captureCancellationTokenSource.Dispose();
            process.Dispose();
            TryDeleteDirectory(profileDirectory, diagnostics);
            throw;
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
                if (_process.HasExited)
                {
                    throw new BrowserLauncherException(
                        $"The browser exited with code {_process.ExitCode} before the test application completed.");
                }

                JsonElement result = await _devToolsConnection.SendCommandAsync(
                    "Runtime.evaluate",
                    new
                    {
                        expression = "globalThis.__mtpBrowserResult ?? null",
                        returnByValue = true,
                        awaitPromise = true,
                    },
                    linkedCancellationTokenSource.Token).ConfigureAwait(false);

                JsonElement remoteObject = result.GetProperty("result");
                if (remoteObject.TryGetProperty("value", out JsonElement value)
                    && value.ValueKind == JsonValueKind.Object
                    && value.TryGetProperty("completed", out JsonElement completed)
                    && completed.GetBoolean())
                {
                    if (value.TryGetProperty("error", out JsonElement error))
                    {
                        _diagnostics.Add("browser", error.GetString() ?? error.GetRawText());
                    }

                    return value.GetProperty("exitCode").GetInt32();
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100), linkedCancellationTokenSource.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (timeoutCancellationTokenSource.IsCancellationRequested)
        {
            throw new BrowserLauncherException($"The browser test application did not complete within {timeout.TotalSeconds} seconds.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _devToolsConnection.DisposeAsync().ConfigureAwait(false);
        KillProcess(_process);
        try
        {
            await _process.WaitForExitAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
        }

        await _captureCancellationTokenSource.CancelAsync().ConfigureAwait(false);
        await Task.WhenAll(_stdoutTask, _stderrTask).ConfigureAwait(false);
        _captureCancellationTokenSource.Dispose();
        _process.Dispose();
        TryDeleteDirectory(_profileDirectory, _diagnostics);
    }

    private static async Task CaptureAsync(
        StreamReader reader,
        string source,
        DiagnosticBuffer diagnostics,
        CancellationToken cancellationToken)
    {
        try
        {
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                diagnostics.Add(source, line);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static async Task<Uri> WaitForPageEndpointAsync(
        Process process,
        string profileDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        string activePortPath = Path.Combine(profileDirectory, "DevToolsActivePort");
        using var timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCancellationTokenSource.Token);

        try
        {
            while (true)
            {
                if (process.HasExited)
                {
                    throw new BrowserLauncherException(
                        $"The browser exited with code {process.ExitCode} before DevTools became available.");
                }

                if (File.Exists(activePortPath))
                {
                    string[] lines;
                    try
                    {
                        lines = await File.ReadAllLinesAsync(activePortPath, linkedCancellationTokenSource.Token).ConfigureAwait(false);
                    }
                    catch (IOException)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(50), linkedCancellationTokenSource.Token).ConfigureAwait(false);
                        continue;
                    }

                    if (lines.Length > 0
                        && int.TryParse(lines[0], NumberStyles.None, CultureInfo.InvariantCulture, out int port))
                    {
                        using var httpClient = new HttpClient();
                        BrowserTarget[]? targets;
                        try
                        {
                            targets = await httpClient.GetFromJsonAsync<BrowserTarget[]>(
                                $"http://127.0.0.1:{port}/json/list",
                                linkedCancellationTokenSource.Token).ConfigureAwait(false);
                        }
                        catch (Exception ex) when (ex is HttpRequestException or JsonException)
                        {
                            await Task.Delay(
                                TimeSpan.FromMilliseconds(50),
                                linkedCancellationTokenSource.Token).ConfigureAwait(false);
                            continue;
                        }

                        string? webSocketDebuggerUrl = targets?
                            .FirstOrDefault(static target => target.Type == "page")
                            ?.WebSocketDebuggerUrl;
                        if (Uri.TryCreate(webSocketDebuggerUrl, UriKind.Absolute, out Uri? endpoint))
                        {
                            return endpoint;
                        }
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(50), linkedCancellationTokenSource.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (timeoutCancellationTokenSource.IsCancellationRequested)
        {
            throw new BrowserLauncherException($"Chromium DevTools did not become ready within {timeout.TotalSeconds} seconds.");
        }
    }

    private async Task InitializePageAsync(
        IReadOnlyList<string> testApplicationArguments,
        Uri browserUri,
        CancellationToken cancellationToken)
    {
        await _devToolsConnection.SendCommandAsync("Runtime.enable", parameters: null, cancellationToken).ConfigureAwait(false);
        await _devToolsConnection.SendCommandAsync("Page.enable", parameters: null, cancellationToken).ConfigureAwait(false);
        await _devToolsConnection.SendCommandAsync("Log.enable", parameters: null, cancellationToken).ConfigureAwait(false);

        string serializedArguments = JsonSerializer.Serialize(testApplicationArguments);
        await _devToolsConnection.SendCommandAsync(
            "Page.addScriptToEvaluateOnNewDocument",
            new
            {
                source = $"Object.defineProperty(globalThis, '__mtpBrowserArguments', {{ value: Object.freeze({serializedArguments}), configurable: false, enumerable: false, writable: false }});",
            },
            cancellationToken).ConfigureAwait(false);
        await _devToolsConnection.SendCommandAsync(
            "Page.navigate",
            new { url = browserUri.AbsoluteUri },
            cancellationToken).ConfigureAwait(false);
    }

    private void SubscribeToDiagnostics()
        => _devToolsConnection.EventReceived += (method, parameters) =>
        {
            switch (method)
            {
                case "Runtime.consoleAPICalled":
                    string type = parameters.TryGetProperty("type", out JsonElement typeElement)
                        ? typeElement.GetString() ?? "console"
                        : "console";
                    string message = parameters.TryGetProperty("args", out JsonElement arguments)
                        ? string.Join(
                            " ",
                            arguments.EnumerateArray().Select(static argument =>
                                argument.TryGetProperty("value", out JsonElement value)
                                    ? value.ToString()
                                    : argument.TryGetProperty("description", out JsonElement description)
                                        ? description.GetString()
                                        : argument.GetRawText()))
                        : parameters.GetRawText();
                    _diagnostics.Add($"browser {type}", message);
                    break;

                case "Runtime.exceptionThrown":
                    _diagnostics.Add("browser exception", parameters.GetRawText());
                    break;

                case "Log.entryAdded":
                    _diagnostics.Add("browser log", parameters.GetRawText());
                    break;
            }
        };

    private static void KillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static bool IsRunningInContainer()
        => string.Equals(
                Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
                "true",
                StringComparison.OrdinalIgnoreCase)
            || File.Exists("/.dockerenv");

    private static void TryDeleteDirectory(string path, DiagnosticBuffer diagnostics)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add("launcher", $"Unable to delete the isolated browser profile: {ex.Message}");
        }
    }

    private sealed record BrowserTarget(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("webSocketDebuggerUrl")] string WebSocketDebuggerUrl);
}
