// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;

namespace Microsoft.Testing.Platform.Browser;

internal sealed class HostProcess : IAsyncDisposable
{
    private static readonly Regex ListeningUrlRegex = new(
        @"^\s*(?<kind>Now listening on:|App url:)\s+(?<url>https?://\S+)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly Process _process;
    private readonly DiagnosticBuffer _diagnostics;
    private readonly string _launchInfoDirectory;
    private readonly string _launchInfoPath;
    private readonly CancellationTokenSource _disposeCancellationTokenSource = new();
    private readonly TaskCompletionSource<Uri> _stdoutReadiness = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task _stdoutTask;
    private readonly Task _stderrTask;

    private HostProcess(
        Process process,
        DiagnosticBuffer diagnostics,
        string launchInfoDirectory,
        string launchInfoPath)
    {
        _process = process;
        _diagnostics = diagnostics;
        _launchInfoDirectory = launchInfoDirectory;
        _launchInfoPath = launchInfoPath;
        _stdoutTask = CaptureAsync(process.StandardOutput, "host stdout", inspectReadiness: true, _disposeCancellationTokenSource.Token);
        _stderrTask = CaptureAsync(process.StandardError, "host stderr", inspectReadiness: true, _disposeCancellationTokenSource.Token);
    }

    public Task WaitForExitAsync(CancellationToken cancellationToken)
        => _process.WaitForExitAsync(cancellationToken);

    public static HostProcess Start(BrowserLauncherOptions options, DiagnosticBuffer diagnostics)
    {
        string launchInfoDirectory = CreateLaunchInfoDirectory();
        string launchInfoPath = Path.Combine(launchInfoDirectory, "launch-info.json");
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
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or BrowserLauncherException)
        {
            TryDeleteLaunchInfoDirectory(launchInfoDirectory, diagnostics);
            throw new BrowserLauncherException($"Unable to start the browser host command '{options.HostCommand}'.", ex);
        }

        return new HostProcess(process, diagnostics, launchInfoDirectory, launchInfoPath);
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

                if (TryReadLaunchInfo(_launchInfoPath) is { } launchInfoUri)
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
            Directory.Delete(_launchInfoDirectory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _diagnostics.Add("launcher", $"Unable to delete the browser host launch-info directory: {ex.Message}");
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
                if (!inspectReadiness)
                {
                    continue;
                }

                try
                {
                    if (TryParseListeningUri(line) is { } uri)
                    {
                        _stdoutReadiness.TrySetResult(uri);
                    }
                }
                catch (BrowserLauncherException ex)
                {
                    _stdoutReadiness.TrySetException(ex);
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    internal static string CreateLaunchInfoDirectory()
    {
        string baseDirectory = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Path.GetTempPath();
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            throw new BrowserLauncherException(
                "Unable to determine a private directory for the browser host launch-info file.");
        }

        string directoryPath = Path.Combine(
            baseDirectory,
            "Microsoft.Testing.Platform.Browser",
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        if (!OperatingSystem.IsWindows())
        {
            return Directory.CreateDirectory(
                directoryPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute).FullName;
        }

        SecurityIdentifier currentUser = WindowsIdentity.GetCurrent().User
            ?? throw new BrowserLauncherException("Unable to determine the current Windows security identifier.");
        var directorySecurity = new DirectorySecurity();
        directorySecurity.SetOwner(currentUser);
        directorySecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        directorySecurity.AddAccessRule(
            new FileSystemAccessRule(
                currentUser,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
        var directory = new DirectoryInfo(directoryPath);
        directory.Create(directorySecurity);
        return directory.FullName;
    }

    internal static Uri? TryReadLaunchInfo(string launchInfoPath)
    {
        try
        {
            if ((File.GetAttributes(launchInfoPath) & FileAttributes.ReparsePoint) != 0)
            {
                throw new BrowserLauncherException(
                    "The browser host launch-info path must not be a symbolic link or reparse point.");
            }
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException)
        {
            return null;
        }

        FileStream stream;
        try
        {
            stream = new FileStream(
                launchInfoPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        using (stream)
        {
            try
            {
                ValidateLaunchInfoPermissions(stream);
                BrowserHostLaunchInfo? launchInfo = JsonSerializer.Deserialize<BrowserHostLaunchInfo>(
                    stream,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return launchInfo is { Version: 1 }
                    && Uri.TryCreate(launchInfo.Url, UriKind.Absolute, out Uri? uri)
                    && IsLoopbackHttpUri(uri)
                        ? uri
                        : throw new BrowserLauncherException("The browser host launch-info file is invalid.");
            }
            catch (JsonException ex)
            {
                throw new BrowserLauncherException("The browser host launch-info file is invalid.", ex);
            }
        }
    }

    internal static Uri? TryParseListeningUri(string line)
        => ListeningUrlRegex.Match(line) is not { Success: true } match
            ? null
            : Uri.TryCreate(match.Groups["url"].Value, UriKind.Absolute, out Uri? uri)
                && IsLoopbackHttpUri(uri)
                    ? match.Groups["kind"].Value.Equals("App url:", StringComparison.OrdinalIgnoreCase)
                        && uri.Scheme == Uri.UriSchemeHttps
                            ? null
                            : uri
                    : throw new BrowserLauncherException("The browser host reported a non-loopback URL.");

    private static bool IsLoopbackHttpUri(Uri uri)
        => (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
                || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
            && (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                || (IPAddress.TryParse(uri.Host, out IPAddress? address) && IPAddress.IsLoopback(address)));

    private static void ValidateLaunchInfoPermissions(FileStream stream)
    {
        if (OperatingSystem.IsWindows())
        {
            // The containing directory has a protected current-user-only DACL. The host creates the
            // launch-info file inside that directory, so it inherits the same access boundary.
            return;
        }

        UnixFileMode mode = File.GetUnixFileMode(stream.SafeFileHandle);
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

    private static void TryDeleteLaunchInfoDirectory(string directory, DiagnosticBuffer diagnostics)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add("launcher", $"Unable to delete the browser host launch-info directory: {ex.Message}");
        }
    }

    private sealed record BrowserHostLaunchInfo(int Version, string Url);
}
