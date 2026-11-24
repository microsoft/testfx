// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

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
                if (!process.WaitForExit(ProcessKillTimeoutMs))
                {
                    logger.SafeLog(
                        MtpClientLogLevel.Debug,
                        $"The MTP server process did not exit within {ProcessKillTimeoutMs}ms after it was killed.");
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or Win32Exception)
        {
            logger.SafeLog(MtpClientLogLevel.Debug, $"Killing the MTP server process threw: {ex}");
        }

        return killed;
    }

    private Task StartShutdownAsync()
        => _shutdown.StartAsync(ShutdownCore);

    private void ShutdownCore()
    {
        try
        {
            // Dispose the connection first (cancels the read loop, disposes the handler -> socket/streams).
            Connection.Dispose();

            MtpServerConnector.SafeStop(_listener, _logger);

            bool killed = SafeKill(_process, _logger);

            // SafeKill reports whether it initiated forced termination. Only read the exit code when the
            // application had already exited naturally (including the race immediately before its HasExited
            // check), so an operating-system kill status is never exposed as an application result.
            int? exitCode = killed ? null : TryReadExitCode();
            Volatile.Write(ref _capturedExitCode, exitCode is int captured ? captured : NoExitCode);
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
