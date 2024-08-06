// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Microsoft.Testing.TestInfrastructure;

public sealed class ProcessHandle : IProcessHandle, IDisposable
{
    private readonly ProcessHandleInfo _processHandleInfo;
    private readonly Process _process;
    private bool _disposed;
    private int _exitCode;

    internal ProcessHandle(Process process, ProcessHandleInfo processHandleInfo)
    {
        _processHandleInfo = processHandleInfo;
        _process = process;
    }

    public string ProcessName => _processHandleInfo.ProcessName ?? "<unknown>";

    public int Id => _processHandleInfo.Id;

    public TextWriter StandardInput => _process.StandardInput;

    public TextReader StandardOutput => _process.StandardOutput;

    public int ExitCode => _process.ExitCode;

    public async Task<int> WaitForExitAsync()
    {
        if (_disposed)
        {
            return _exitCode;
        }

        await _process.WaitForExitAsync();
        return _process.ExitCode;
    }

    public async Task<int> StopAsync()
    {
        if (_disposed)
        {
            return _exitCode;
        }

        KillSafe(_process);
        return await WaitForExitAsync();
    }

    public void Kill()
    {
        if (_disposed)
        {
            return;
        }

        KillSafe(_process);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_process)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        KillSafe(_process);
        _process.WaitForExit();
        _exitCode = _process.ExitCode;
        _process.Dispose();
    }

    public async Task WriteInputAsync(string input)
    {
        await _process.StandardInput.WriteLineAsync(input);
        await _process.StandardInput.FlushAsync();
    }

    private static void KillSafe(Process process)
    {
        try
        {
#if NETCOREAPP
            process.Kill(true);
#else
            process.Kill();
#endif
        }
        catch (InvalidOperationException)
        {
        }
        catch (NotSupportedException)
        {
        }
    }
}
