// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Browser;

internal static class PlaywrightNodeExecutable
{
    public static void EnsureExecutable()
        => Configure(explicitPath: null);

    public static void Configure(string? explicitPath)
    {
        string? configuredPath = explicitPath
            ?? Environment.GetEnvironmentVariable("PLAYWRIGHT_NODEJS_PATH");
        string nodePath = configuredPath ?? GetPath(
            AppContext.BaseDirectory,
            OperatingSystem.IsLinux() ? OSPlatform.Linux : OperatingSystem.IsMacOS() ? OSPlatform.OSX : OSPlatform.Windows,
            RuntimeInformation.ProcessArchitecture);
        if (!File.Exists(nodePath))
        {
            throw new BrowserLauncherException(
                $"The Playwright Node.js driver was not found at '{nodePath}'.");
        }

        if (configuredPath is not null)
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_NODEJS_PATH", nodePath);
        }

        if (OperatingSystem.IsWindows())
        {
            return;
        }

        UnixFileMode mode = File.GetUnixFileMode(nodePath);
        if ((mode & UnixFileMode.UserExecute) == 0)
        {
            File.SetUnixFileMode(nodePath, mode | UnixFileMode.UserExecute);
        }
    }

    internal static string GetPath(
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
            ({ } os, _) when os == OSPlatform.Windows => "win32_x64",
            _ => throw new BrowserLauncherException(
                $"Microsoft.Testing.Platform.Browser does not support Playwright on {operatingSystem}/{architecture}."),
        };

        return Path.Combine(
            baseDirectory,
            ".playwright",
            "node",
            platformDirectory,
            operatingSystem == OSPlatform.Windows ? "node.exe" : "node");
    }
}
