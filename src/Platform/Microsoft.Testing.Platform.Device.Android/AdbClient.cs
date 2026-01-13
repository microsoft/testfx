// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;

namespace Microsoft.Testing.Platform.Device.Android;

/// <summary>
/// Client wrapper for Android Debug Bridge (ADB) command-line tool.
/// </summary>
internal sealed class AdbClient
{
    private readonly string _adbPath;

    public AdbClient()
    {
        _adbPath = FindAdbPath();
    }

    /// <summary>
    /// Executes an ADB command and returns the output.
    /// </summary>
    public async Task<AdbResult> ExecuteAsync(string arguments, CancellationToken cancellationToken)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = _adbPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = processStartInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                errorBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        return new AdbResult(
            process.ExitCode,
            outputBuilder.ToString(),
            errorBuilder.ToString());
    }

    private static string FindAdbPath()
    {
        // Try to find adb in PATH
        string? adbFromPath = FindInPath("adb");
        if (adbFromPath is not null)
        {
            return adbFromPath;
        }

        // Try ANDROID_HOME environment variable
        string? androidHome = Environment.GetEnvironmentVariable("ANDROID_HOME");
        if (androidHome is not null)
        {
            string adbPath = Path.Combine(androidHome, "platform-tools", "adb");
            if (OperatingSystem.IsWindows())
            {
                adbPath += ".exe";
            }

            if (File.Exists(adbPath))
            {
                return adbPath;
            }
        }

        throw new InvalidOperationException(
            "ADB not found. Please ensure Android SDK is installed and ANDROID_HOME is set, or adb is in PATH.");
    }

    private static string? FindInPath(string fileName)
    {
        if (OperatingSystem.IsWindows())
        {
            fileName += ".exe";
        }

        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv is null)
        {
            return null;
        }

        char separator = OperatingSystem.IsWindows() ? ';' : ':';
        string[] paths = pathEnv.Split(separator, StringSplitOptions.RemoveEmptyEntries);

        foreach (string path in paths)
        {
            string fullPath = Path.Combine(path, fileName);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }
}

/// <summary>
/// Result of an ADB command execution.
/// </summary>
internal record AdbResult(
    int ExitCode,
    string Output,
    string Error)
{
    public bool Success => ExitCode == 0;
}
