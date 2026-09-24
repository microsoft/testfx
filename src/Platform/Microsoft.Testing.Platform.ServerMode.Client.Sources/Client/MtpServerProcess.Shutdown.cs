// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Net.Sockets;

namespace Microsoft.Testing.Platform.ServerMode.Client;

internal sealed partial class MtpServerProcess
{
    private static bool SafeKill(Process process, IMtpClientLogger logger)
    {
        bool killed = false;
        try
        {
            if (!process.HasExited)
            {
#if NETCOREAPP
                process.Kill(entireProcessTree: true);
#else
                // .NET Framework's Process.Kill cannot kill the whole process tree; any child processes the
                // server spawned are left to the OS. This is best-effort teardown on that platform.
                process.Kill();
#endif
                killed = true;

                // Block (bounded) for the OS to finish tearing the process down so a caller can immediately
                // delete the application directory without racing a file lock on the still-exiting executable.
                process.WaitForExit(ProcessKillTimeoutMs);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or Win32Exception)
        {
            logger.SafeLog(MtpClientLogLevel.Debug, $"Killing the MTP server process threw: {ex}");
        }

        return killed;
    }

    private Task StartShutdownAsync()
    {
        lock (_shutdownLock)
        {
            return _shutdown ??= Task.Run(ShutdownCore);
        }
    }

    private void ShutdownCore()
    {
        try
        {
            // Dispose the connection first (cancels the read loop, disposes the handler -> socket/streams).
            Connection.Dispose();

            try
            {
                _client.Dispose();
            }
            catch (SocketException ex)
            {
                _logger.SafeLog(MtpClientLogLevel.Debug, $"Disposing the accepted client socket threw: {ex}");
            }

            MtpServerConnector.SafeStop(_listener, _logger);

            // Capture before killing, so an application that already exited on its own reports its real exit
            // code rather than the kill's, and before Dispose(), after which the Process cannot be read.
            int? exitCode = TryReadExitCode();
            Volatile.Write(ref _capturedExitCode, exitCode is int captured ? captured : NoExitCode);
            bool killed = SafeKill(_process, _logger);

            // Preserve the race where the process exits naturally between the first read and SafeKill's
            // HasExited check, but never publish the operating system's forced-termination status as though
            // it were an application-returned exit code.
            if (exitCode is null && !killed && TryReadExitCode() is int racedExitCode)
            {
                Volatile.Write(ref _capturedExitCode, racedExitCode);
            }

            _process.Dispose();
        }
        catch (Exception ex)
        {
            // The shared teardown task must never fault: every current and future Dispose/ShutdownAsync
            // caller awaits this one task, so a fault here would throw from every subsequent disposal.
            _logger.SafeLog(MtpClientLogLevel.Error, $"Tearing down the MTP server process threw: {ex}");
        }
    }
}
