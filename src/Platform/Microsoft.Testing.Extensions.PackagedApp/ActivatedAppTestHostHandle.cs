// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if PACKAGEDAPP_WINRT

using Microsoft.Testing.Platform.Extensions.TestHostControllers;

namespace Microsoft.Testing.Extensions.PackagedApp;

/// <summary>
/// An <see cref="ITestHostHandle"/> over a packaged (MSIX) test host that was activated by Application
/// User Model ID. Unlike the loose-layout handle, a real, queryable process id is available — it is
/// returned by <c>IApplicationActivationManager::ActivateApplication</c> — so the handle monitors and
/// terminates the actual activated process and surfaces its id as the identifier.
/// </summary>
internal sealed class ActivatedAppTestHostHandle : ILocalTestHostHandle, ITestHostHandleExitCodePolicy
{
    private static readonly TimeSpan RetryActivationTeardownDelay = TimeSpan.FromSeconds(5);

    private readonly Process _process;
    private readonly string? _handshakePath;
    private readonly string? _activationPayloadPath;
    private readonly string? _diagnosticRecoveryDirectory;
    private readonly string? _diagnosticScratchDirectory;
    private readonly string? _resultsRecoveryDirectory;
    private readonly string? _resultsScratchDirectory;
    private readonly string? _retryArtifactManifestDestinationPath;
    private readonly string? _retryArtifactManifestPath;
    private readonly string? _scratchDirectory;

    public ActivatedAppTestHostHandle(
        uint processId,
        string? handshakePath,
        string? activationPayloadPath,
        string? scratchDirectory,
        string? resultsScratchDirectory,
        string? resultsRecoveryDirectory,
        string? diagnosticScratchDirectory,
        string? diagnosticRecoveryDirectory,
        string? retryArtifactManifestPath,
        string? retryArtifactManifestDestinationPath)
    {
        _process = Process.GetProcessById((int)processId);
        _handshakePath = handshakePath;
        _activationPayloadPath = activationPayloadPath;
        _scratchDirectory = scratchDirectory;
        _resultsScratchDirectory = resultsScratchDirectory;
        _resultsRecoveryDirectory = resultsRecoveryDirectory;
        _diagnosticScratchDirectory = diagnosticScratchDirectory;
        _diagnosticRecoveryDirectory = diagnosticRecoveryDirectory;
        _retryArtifactManifestPath = retryArtifactManifestPath;
        _retryArtifactManifestDestinationPath = retryArtifactManifestDestinationPath;
    }

    public string? Identifier => _process.Id.ToString(CultureInfo.InvariantCulture);

    public int ProcessId => _process.Id;

    public int ExitCode => _process.ExitCode;

    public bool HasExited => _process.HasExited;

    public bool IsExitCodeAuthoritative => _scratchDirectory is null;

    public async Task WaitForExitAsync(CancellationToken cancellationToken)
    {
        await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (_retryArtifactManifestDestinationPath is not null)
        {
            // Windows can report the AppContainer process exit before activation/PLM has released that
            // application instance. A retry activated immediately afterward can start but remain unable to
            // resolve the newly created LOCAL\ pipe. Keep the delay bounded to retry attempts, and long enough
            // for the same AUMID to become independently activatable on loaded Windows agents.
            await Task.Delay(RetryActivationTeardownDelay, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Terminate()
    {
        try
        {
            _process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process has already exited.
        }
    }

    public void Dispose()
    {
        _process.Dispose();

        // The activated host normally consumes and deletes the connect-back hand-off itself, but if it
        // exited before reading it (for example a crash on startup) the file would otherwise be left
        // behind with the pipe name/correlation data. Remove it here as a best-effort backstop.
        if (_handshakePath is not null)
        {
            PackagedAppConnectBackHandshake.TryDelete(_handshakePath);
        }

        PackagedAppActivationArguments.TryDeletePayload(_activationPayloadPath);
        RecoverScratchArtifacts();
        RecoverRetryArtifactManifest();
        TryDeleteScratchDirectory(_scratchDirectory);
    }

    private void RecoverScratchArtifacts()
    {
        RecoverScratchArtifacts(_resultsScratchDirectory, _resultsRecoveryDirectory);
        RecoverScratchArtifacts(_diagnosticScratchDirectory, _diagnosticRecoveryDirectory);
    }

    private static void RecoverScratchArtifacts(string? scratchDirectory, string? recoveryDirectory)
    {
        if (scratchDirectory is null
            || recoveryDirectory is null
            || !Directory.Exists(scratchDirectory))
        {
            return;
        }

        try
        {
            PackagedAppScratchArtifactRecovery.Recover(scratchDirectory, recoveryDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Best-effort recovery of packaged test-host scratch directory '{scratchDirectory}' failed: {ex}");
        }
    }

    private void RecoverRetryArtifactManifest()
    {
        const long MaxManifestBytes = 16L * 1024 * 1024;
        const int MaxManifestLineChars = 64 * 1024;
        const int MaxManifestRecords = 10_000;

        if (_retryArtifactManifestPath is null
            || _retryArtifactManifestDestinationPath is null
            || _scratchDirectory is null)
        {
            return;
        }

        try
        {
            if (!File.Exists(_retryArtifactManifestPath)
                || new FileInfo(_retryArtifactManifestPath).Length > MaxManifestBytes)
            {
                return;
            }

            var recoveredLines = new List<string>();
            foreach (string line in File.ReadLines(_retryArtifactManifestPath).Take(MaxManifestRecords))
            {
                if (line.Length > MaxManifestLineChars)
                {
                    continue;
                }

                int separatorIndex = line.IndexOf('\t');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                string sourcePath = Path.GetFullPath(
                    Encoding.UTF8.GetString(Convert.FromBase64String(line.Substring(0, separatorIndex))));
                string? recoveredPath = TryGetRecoveredArtifactPath(
                    sourcePath,
                    _resultsScratchDirectory,
                    _resultsRecoveryDirectory)
                    ?? TryGetRecoveredArtifactPath(
                        sourcePath,
                        _diagnosticScratchDirectory,
                        _diagnosticRecoveryDirectory);
                if (recoveredPath is null || !File.Exists(recoveredPath))
                {
                    continue;
                }

                recoveredLines.Add(
                    $"{Convert.ToBase64String(Encoding.UTF8.GetBytes(Path.GetFullPath(recoveredPath)))}{line[separatorIndex..]}");
            }

            if (recoveredLines.Count == 0)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_retryArtifactManifestDestinationPath)!);
            File.WriteAllLines(
                _retryArtifactManifestDestinationPath,
                recoveredLines,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or FormatException or ArgumentException or NotSupportedException or PathTooLongException)
        {
            Debug.WriteLine($"Best-effort recovery of retry artifact manifest '{_retryArtifactManifestPath}' failed: {ex}");
        }
    }

    private static string? TryGetRecoveredArtifactPath(
        string sourcePath,
        string? scratchDirectory,
        string? recoveryDirectory)
    {
        if (scratchDirectory is null || recoveryDirectory is null)
        {
            return null;
        }

        string scratchPrefix = Path.GetFullPath(scratchDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return sourcePath.StartsWith(scratchPrefix, StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(recoveryDirectory, Path.GetRelativePath(scratchDirectory, sourcePath))
            : null;
    }

    private static void TryDeleteScratchDirectory(string? scratchDirectory)
    {
        if (scratchDirectory is null)
        {
            return;
        }

        try
        {
            if (Directory.Exists(scratchDirectory))
            {
                Directory.Delete(scratchDirectory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Best-effort delete of packaged test-host scratch directory '{scratchDirectory}' failed: {ex}");
        }
    }
}

#endif
