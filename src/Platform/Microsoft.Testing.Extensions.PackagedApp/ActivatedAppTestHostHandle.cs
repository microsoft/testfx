// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if PACKAGEDAPP_WINRT

using Microsoft.Testing.Platform.Extensions.TestHostControllers;

namespace Microsoft.Testing.Extensions.PackagedApp;

/// <summary>
/// An <see cref="ITestHostHandle"/> over a packaged (MSIX) test host that was activated by Application
/// User Model ID. Unlike the loose-layout handle, a real, queryable process id is available — it is
/// returned by <c>IApplicationActivationManager::ActivateApplication</c> — so the handle monitors and
/// terminates the actual activated process and surfaces its id as the identifier.
/// </summary>
internal sealed class ActivatedAppTestHostHandle : ITestHostHandle
{
    private readonly Process _process;
    private readonly string? _handshakePath;

    public ActivatedAppTestHostHandle(uint processId, string? handshakePath)
    {
        _process = Process.GetProcessById((int)processId);
        _handshakePath = handshakePath;
    }

    public string? Identifier => _process.Id.ToString(CultureInfo.InvariantCulture);

    public int ExitCode => _process.ExitCode;

    public bool HasExited => _process.HasExited;

    public Task WaitForExitAsync(CancellationToken cancellationToken) => _process.WaitForExitAsync(cancellationToken);

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

        // The activated host normally consumes and deletes the connect-back hand-off itself, but if it
        // exited before reading it (for example a crash on startup) the file would otherwise be left
        // behind with the pipe name/correlation data. Remove it here as a best-effort backstop.
        if (_handshakePath is not null)
        {
            PackagedAppConnectBackHandshake.TryDelete(_handshakePath);
        }
    }
}

#endif
