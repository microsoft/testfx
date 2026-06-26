// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostControllers;

namespace Microsoft.Testing.Platform.Helpers;

/// <summary>
/// Adapts a public <see cref="ITestHostHandle"/> returned by an <see cref="ITestHostLauncher"/> to
/// the internal <see cref="IProcess"/> monitoring contract used by the test host controller host.
/// Only the members the platform consumes after launch are backed by the handle; the rest are not
/// used in this flow.
/// </summary>
[UnsupportedOSPlatform("browser")]
internal sealed class TestHostHandleToProcessAdapter : IProcess
{
    private readonly ITestHostHandle _handle;

    public TestHostHandleToProcessAdapter(ITestHostHandle handle)
    {
        _handle = handle;

        // The public handle deliberately has no Exited event (consumers use WaitForExitAsync). The
        // internal IProcess contract still exposes one for the in-process logging hook, so synthesize
        // it from the exit task. It is informational only, hence best-effort and fire-and-forget.
        _ = RaiseExitedWhenDoneAsync();
    }

    public event EventHandler? Exited;

    // A custom launcher does not necessarily back a local OS process, so there is no numeric PID to
    // return. The controller host only reads Id for logging and tolerates this exception.
    public int Id => throw new InvalidOperationException("The test host launcher does not expose a numeric process id; use 'ITestHostHandle.Identifier' for diagnostics.");

    public string Name => string.Empty;

    public int ExitCode => _handle.ExitCode;

    public bool HasExited => _handle.HasExited;

    public IMainModule? MainModule => null;

    public DateTime StartTime => default;

    public Task WaitForExitAsync() => _handle.WaitForExitAsync();

    public void WaitForExit() => _handle.WaitForExitAsync().GetAwaiter().GetResult();

    public void Kill() => _handle.Terminate();

    public void Dispose() => (_handle as IDisposable)?.Dispose();

    private async Task RaiseExitedWhenDoneAsync()
    {
        try
        {
            await _handle.WaitForExitAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The Exited event is informational only; never surface failures from this path.
            Debug.WriteLine($"Ignoring failure while awaiting test host exit for the informational Exited event: {ex}");
        }

        Exited?.Invoke(this, EventArgs.Empty);
    }
}
