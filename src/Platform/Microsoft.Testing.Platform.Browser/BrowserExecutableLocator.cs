// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Browser;

internal static class BrowserExecutableLocator
{
    public static string Locate(string? configuredPath)
    {
        string? environmentPath = Environment.GetEnvironmentVariable("MTP_BROWSER_EXECUTABLE");
        foreach (string? candidate in EnumerateCandidates(configuredPath, environmentPath))
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        foreach (string executableName in GetExecutableNames())
        {
            if (FindOnPath(executableName) is { } path)
            {
                return path;
            }
        }

        throw new BrowserLauncherException(
            "No Chromium-family browser was found. Set TestingPlatformBrowserExecutable or MTP_BROWSER_EXECUTABLE.");
    }

    internal static IEnumerable<string?> EnumerateCandidates(string? configuredPath, string? environmentPath)
    {
        yield return configuredPath;
        yield return environmentPath;

        if (OperatingSystem.IsWindows())
        {
            foreach (string root in new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            })
            {
                yield return Path.Combine(root, "Microsoft", "Edge", "Application", "msedge.exe");
                yield return Path.Combine(root, "Google", "Chrome", "Application", "chrome.exe");
                yield return Path.Combine(root, "Chromium", "Application", "chrome.exe");
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            yield return "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge";
            yield return "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
            yield return "/Applications/Chromium.app/Contents/MacOS/Chromium";
        }
    }

    private static IEnumerable<string> GetExecutableNames()
        => OperatingSystem.IsWindows()
            ? ["msedge.exe", "chrome.exe", "chromium.exe"]
            : ["microsoft-edge", "microsoft-edge-stable", "google-chrome", "google-chrome-stable", "chromium", "chromium-browser"];

    private static string? FindOnPath(string executableName)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (path is null)
        {
            return null;
        }

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory, executableName);
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }
}
