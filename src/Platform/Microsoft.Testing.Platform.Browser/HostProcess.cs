// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Browser;

internal sealed class HostProcess : IAsyncDisposable
{
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(5);
    private static readonly Regex AppUrlRegex = new(
        @"^\s*App url:\s+(?<url>http://\S+)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly Process _process;
    private readonly DiagnosticBuffer _diagnostics;
    private readonly CancellationTokenSource _disposeCancellationTokenSource = new();
    private readonly TaskCompletionSource<Uri> _readiness = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly Task _stdoutTask;
    private readonly Task _stderrTask;

    private HostProcess(Process process, DiagnosticBuffer diagnostics)
    {
        _process = process;
        _diagnostics = diagnostics;
        _stdoutTask = CaptureAsync(
            process.StandardOutput,
            "host stdout",
            inspectReadiness: true,
            _disposeCancellationTokenSource.Token);
        _stderrTask = CaptureAsync(
            process.StandardError,
            "host stderr",
            inspectReadiness: false,
            _disposeCancellationTokenSource.Token);
    }

    public Task WaitForExitAsync(CancellationToken cancellationToken)
        => _process.WaitForExitAsync(cancellationToken);

    public static HostProcess Start(
        BrowserLauncherOptions options,
        DiagnosticBuffer diagnostics)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = options.HostCommand,
            Arguments = options.HostArguments,
            WorkingDirectory = options.HostWorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            Process process = Process.Start(startInfo)
                ?? throw new BrowserLauncherException(
                    $"The browser host command '{options.HostCommand}' did not start.");
            return new HostProcess(process, diagnostics);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw new BrowserLauncherException(
                $"Unable to start the browser host command '{options.HostCommand}'.",
                ex);
        }
    }

    public async Task<Uri> WaitUntilReadyAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
        using var linkedCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCancellationTokenSource.Token,
                _disposeCancellationTokenSource.Token);

        try
        {
            Task processExit = _process.WaitForExitAsync(linkedCancellationTokenSource.Token);
            Task completed = await Task.WhenAny(_readiness.Task, processExit).ConfigureAwait(false);
            if (completed == _readiness.Task)
            {
                return await _readiness.Task.ConfigureAwait(false);
            }

            linkedCancellationTokenSource.Token.ThrowIfCancellationRequested();
            throw new BrowserLauncherException(
                $"The browser host exited with code {_process.ExitCode} before reporting readiness.");
        }
        catch (OperationCanceledException) when (timeoutCancellationTokenSource.IsCancellationRequested)
        {
            throw new BrowserLauncherException(
                $"The browser host did not become ready within {timeout.TotalSeconds} seconds.");
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
        catch (Exception ex) when (
            ex is InvalidOperationException
                or System.ComponentModel.Win32Exception
                or NotSupportedException)
        {
            _diagnostics.Add(
                "launcher cleanup",
                $"Unable to terminate the browser host process: {ex.Message}");
        }

        try
        {
            await _process.WaitForExitAsync().WaitAsync(CleanupTimeout).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
        {
            _diagnostics.Add(
                "launcher cleanup",
                $"Unable to confirm browser host process exit: {ex.Message}");
        }

        try
        {
            await Task.WhenAll(_stdoutTask, _stderrTask)
                .WaitAsync(CleanupTimeout).ConfigureAwait(false);
        }
        catch (Exception ex) when (
            ex is IOException
                or ObjectDisposedException
                or TimeoutException)
        {
            _diagnostics.Add(
                "launcher cleanup",
                $"Unable to finish reading browser host diagnostics: {ex.Message}");
        }

        _process.Dispose();
        _disposeCancellationTokenSource.Dispose();
    }

    internal static Uri? TryParseAppUrl(string line)
        => AppUrlRegex.Match(line) is not { Success: true } match
            ? null
            : Uri.TryCreate(match.Groups["url"].Value, UriKind.Absolute, out Uri? uri)
                && uri.Scheme == Uri.UriSchemeHttp
                && uri.IsLoopback
                    ? uri
                    : throw new BrowserLauncherException(
                        "The browser host reported a non-loopback application URL.");

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
                if (!inspectReadiness)
                {
                    continue;
                }

                try
                {
                    if (TryParseAppUrl(line) is { } uri)
                    {
                        _readiness.TrySetResult(uri);
                    }
                }
                catch (BrowserLauncherException ex)
                {
                    _readiness.TrySetException(ex);
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
