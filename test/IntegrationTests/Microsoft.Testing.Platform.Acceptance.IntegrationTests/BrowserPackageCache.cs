// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

internal sealed class BrowserPackageCache : IDisposable
{
    private bool _disposed;

    private BrowserPackageCache(string path)
        => Path = path;

    public string Path { get; }

    public static BrowserPackageCache Create(string projectPath)
    {
        string basePath =
            Environment.GetEnvironmentVariable("AGENT_TEMPDIRECTORY")
            ?? System.IO.Path.GetTempPath();
        string path = System.IO.Path.Combine(
            basePath,
            $"tbp-{Guid.NewGuid():N}");
        string fullPath = System.IO.Path.GetFullPath(path);
        string fullProjectPath = System.IO.Path.GetFullPath(projectPath);
        if (fullPath.StartsWith(
            fullProjectPath + System.IO.Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The browser package cache must be outside the generated project directory.");
        }

        Directory.CreateDirectory(fullPath);
        return new BrowserPackageCache(fullPath);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }

        _disposed = true;
    }
}
