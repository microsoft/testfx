// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostControllers;

namespace Microsoft.Testing.Extensions.Msix;

/// <summary>
/// An <see cref="ITestHostHandle"/> over a deployed Msix (UWP/WinUI) test host process that
/// deliberately hides the underlying process id, modelling a launch where no local, query-able PID
/// is available.
/// </summary>
internal sealed class MsixTestHostHandle : ITestHostHandle, IDisposable
{
    private readonly Process _process;

    public MsixTestHostHandle(Process process)
    {
        _process = process;
        _process.EnableRaisingEvents = true;
        _process.Exited += OnProcessExited;
    }

    public event EventHandler? Exited;

    // Intentionally null: the platform must not depend on a local process id. A packaged UWP/WinUI
    // implementation could surface the AUMID-activated PID here instead.
    public int? ProcessId => null;

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
        _process.Exited -= OnProcessExited;
        _process.Dispose();
    }

    private void OnProcessExited(object? sender, EventArgs e)
        => Exited?.Invoke(this, e);
}
