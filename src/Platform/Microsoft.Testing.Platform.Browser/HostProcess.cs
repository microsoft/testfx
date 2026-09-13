// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Text.Json;

namespace Microsoft.Testing.Platform.Browser;

internal sealed class HostProcess : IAsyncDisposable
{
    private static readonly Regex ListeningUrlRegex = new(
        @"Now listening on:\s+(?<url>https?://\S+)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly Process _process;
    private readonly DiagnosticBuffer _diagnostics;
    private readonly string _launchInfoPath;
    private readonly CancellationTokenSource _disposeCancellationTokenSource = new();
    private readonly TaskCompletionSource<Uri> _stdoutReadiness = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task _stdoutTask;
    private readonly Task _stderrTask;

    private HostProcess(Process process, DiagnosticBuffer diagnostics, string launchInfoPath)
    {
        _process = process;
        _diagnostics = diagnostics;
        _launchInfoPath = launchInfoPath;
        _stdoutTask = CaptureAsync(process.StandardOutput, "host stdout", inspectReadiness: true, _disposeCancellationTokenSource.Token);
        _stderrTask = CaptureAsync(process.StandardError, "host stderr", inspectReadiness: true, _disposeCancellationTokenSource.Token);
    }

    public Task WaitForExitAsync(CancellationToken cancellationToken)
        => _process.WaitForExitAsync(cancellationToken);

    public static HostProcess Start(BrowserLauncherOptions options, DiagnosticBuffer diagnostics)
    {
        string launchInfoPath = Path.Combine(Path.GetTempPath(), $"mtp-browser-host-{Guid.NewGuid():N}.json");
        var startInfo = new ProcessStartInfo
        {
            FileName = options.HostCommand,
            WorkingDirectory = options.HostWorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (string argument in options.HostArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["TESTINGPLATFORM_BROWSER_LAUNCH_INFO_FILE"] = launchInfoPath;

        Process process;
        try
        {
            process = Process.Start(startInfo)
                ?? throw new BrowserLauncherException($"The browser host command '{options.HostCommand}' did not start.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw new BrowserLauncherException($"Unable to start the browser host command '{options.HostCommand}'.", ex);
        }

        return new HostProcess(process, diagnostics, launchInfoPath);
    }

    public async Task<Uri> WaitUntilReadyAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCancellationTokenSource.Token,
            _disposeCancellationTokenSource.Token);

        try
        {
            while (true)
            {
                if (_process.HasExited)
                {
                    throw new BrowserLauncherException(
                        $"The browser host exited with code {_process.ExitCode} before reporting readiness.");
                }

                if (TryReadLaunchInfo() is { } launchInfoUri)
                {
                    return launchInfoUri;
                }

                var delay = Task.Delay(TimeSpan.FromMilliseconds(100), linkedCancellationTokenSource.Token);
                Task completed = await Task.WhenAny(_stdoutReadiness.Task, delay).ConfigureAwait(false);
                if (completed == _stdoutReadiness.Task)
                {
                    return await _stdoutReadiness.Task.ConfigureAwait(false);
                }

                linkedCancellationTokenSource.Token.ThrowIfCancellationRequested();
            }
        }
        catch (OperationCanceledException) when (timeoutCancellationTokenSource.IsCancellationRequested)
        {
            throw new BrowserLauncherException($"The browser host did not become ready within {timeout.TotalSeconds} seconds.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _disposeCancellationTokenSource.CancelAsync().ConfigureAwait(false);

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }

        try
        {
            await _process.WaitForExitAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
        }

        await Task.WhenAll(_stdoutTask, _stderrTask).ConfigureAwait(false);
        _process.Dispose();
        _disposeCancellationTokenSource.Dispose();

        try
        {
            File.Delete(_launchInfoPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _diagnostics.Add("launcher", $"Unable to delete the browser host launch-info file: {ex.Message}");
        }
    }

    private async Task CaptureAsync(
        StreamReader reader,
        string source,
        bool inspectReadiness,
        CancellationToken cancellationToken)
    {
        try
        {
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                _diagnostics.Add(source, line);
                if (inspectReadiness
                    && ListeningUrlRegex.Match(line) is { Success: true } match
                    && Uri.TryCreate(match.Groups["url"].Value, UriKind.Absolute, out Uri? uri))
                {
                    if (IsLoopbackHttpUri(uri))
                    {
                        _stdoutReadiness.TrySetResult(uri);
                    }
                    else
                    {
                        _stdoutReadiness.TrySetException(
                            new BrowserLauncherException("The browser host reported a non-loopback URL."));
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private Uri? TryReadLaunchInfo()
    {
        if (!File.Exists(_launchInfoPath))
        {
            return null;
        }

        try
        {
            ValidateLaunchInfoPermissions();
            using FileStream stream = File.OpenRead(_launchInfoPath);
            BrowserHostLaunchInfo? launchInfo = JsonSerializer.Deserialize<BrowserHostLaunchInfo>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return launchInfo is { Version: 1 }
                && Uri.TryCreate(launchInfo.Url, UriKind.Absolute, out Uri? uri)
                && IsLoopbackHttpUri(uri)
                    ? uri
                    : throw new BrowserLauncherException("The browser host launch-info file is invalid.");
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException ex)
        {
            throw new BrowserLauncherException("The browser host launch-info file is invalid.", ex);
        }
    }

    private static bool IsLoopbackHttpUri(Uri uri)
        => (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
                || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
            && (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                || (IPAddress.TryParse(uri.Host, out IPAddress? address) && IPAddress.IsLoopback(address)));

    private void ValidateLaunchInfoPermissions()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        UnixFileMode mode = File.GetUnixFileMode(_launchInfoPath);
        const UnixFileMode disallowed =
            UnixFileMode.GroupRead
            | UnixFileMode.GroupWrite
            | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead
            | UnixFileMode.OtherWrite
            | UnixFileMode.OtherExecute;
        if ((mode & disallowed) != 0)
        {
            throw new BrowserLauncherException("The browser host launch-info file is accessible by users other than its owner.");
        }
    }

    private sealed record BrowserHostLaunchInfo(int Version, string Url);
}
