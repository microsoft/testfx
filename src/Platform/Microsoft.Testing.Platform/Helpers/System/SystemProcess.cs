// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class SystemProcess : IProcess, IDisposable
{
    private readonly Process _process;

    public SystemProcess(Process process)
    {
        _process = process;
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Create("browser")))
        {
            _process.Exited += OnProcessExited;
        }
    }

    public event EventHandler? Exited;

    [UnsupportedOSPlatform("browser")]
    public bool HasExited => _process.HasExited;

    [UnsupportedOSPlatform("browser")]
    public int Id => _process.Id;

    [UnsupportedOSPlatform("browser")]
    public string Name => _process.ProcessName;

    [UnsupportedOSPlatform("browser")]
    public int ExitCode => _process.ExitCode;

#if NETCOREAPP
    [UnsupportedOSPlatform("browser")]
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

    [UnsupportedOSPlatform("browser")]
    public void WaitForExit()
        => _process.WaitForExit();

    [UnsupportedOSPlatform("browser")]
    public Task WaitForExitAsync()
        => _process.WaitForExitAsync();

#if NETCOREAPP
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    public void Kill()
        => _process.Kill(true);
#else
    [UnsupportedOSPlatform("browser")]
    public void Kill()
        => _process.Kill();
#endif

    public void Dispose() => _process.Dispose();
}
