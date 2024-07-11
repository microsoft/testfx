// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class SystemProcess : IProcess, IDisposable
{
    private readonly Process _process;

    public SystemProcess(Process process)
    {
        _process = process;
        _process.Exited += OnProcessExited;
    }

    public event EventHandler? Exited;

    public bool HasExited => _process.HasExited;

    public int Id => _process.Id;

    public int ExitCode => _process.ExitCode;

#if NETCOREAPP
    public IMainModule? MainModule
        => _process.MainModule is null
            ? null
            : (IMainModule)new SystemMainModule(_process.MainModule);
#else
    public IMainModule MainModule
        => new SystemMainModule(_process.MainModule);
#endif

    private void OnProcessExited(object? sender, EventArgs e)
        => Exited?.Invoke(sender, e);

    public void WaitForExit()
        => _process.WaitForExit();

#if NETCOREAPP
    public Task WaitForExitAsync()
        => _process.WaitForExitAsync();
#endif

#if NETCOREAPP
    public void Kill()
        => _process.Kill(true);
#else
    public void Kill()
        => _process.Kill();
#endif

    public void Dispose() => _process.Dispose();
}
