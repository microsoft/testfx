// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostControllers;

namespace Microsoft.Testing.Extensions.PackagedApp;

/// <summary>
/// An <see cref="ITestHostHandle"/> over a deployed packaged Windows (UWP/WinUI) test host process
/// that deliberately exposes no identifier, modelling a launch where no local, query-able PID is
/// available.
/// </summary>
internal sealed class PackagedAppTestHostHandle : ITestHostHandle, IDisposable
{
    private readonly Process _process;
    private readonly string _deploymentDirectory;

    public PackagedAppTestHostHandle(Process process, string deploymentDirectory)
    {
        _process = process;
        _deploymentDirectory = deploymentDirectory;
    }

    // Intentionally null: the platform must not depend on any identifier. A packaged UWP/WinUI
    // implementation could surface the AUMID-activated PID (as a string) here instead.
    public string? Identifier => null;

    public int ExitCode => _process.ExitCode;

    public bool HasExited => _process.HasExited;

    public Task WaitForExitAsync() => _process.WaitForExitAsync();

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

        // The platform disposes the handle once the host has exited, so it is safe to remove the
        // staged deployment now. Best-effort: never let cleanup failures surface as run failures.
        try
        {
            if (Directory.Exists(_deploymentDirectory))
            {
                Directory.Delete(_deploymentDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
