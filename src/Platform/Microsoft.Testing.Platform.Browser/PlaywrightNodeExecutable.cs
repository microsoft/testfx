// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Browser;

internal static class PlaywrightNodeExecutable
{
    public static void EnsureExecutable()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string nodePath = GetPath(
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
            _ => throw new BrowserLauncherException(
                $"Microsoft.Testing.Platform.Browser does not support Playwright on {operatingSystem}/{architecture}."),
        };

        return Path.Combine(baseDirectory, ".playwright", "node", platformDirectory, "node");
    }
}
