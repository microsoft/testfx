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

    public ActivatedAppTestHostHandle(uint processId)
        => _process = Process.GetProcessById((int)processId);

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

    public void Dispose() => _process.Dispose();
}

#endif
