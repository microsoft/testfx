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
        _handle.Exited += OnHandleExited;
    }

    public event EventHandler? Exited;

    public int Id => _handle.ProcessId ?? throw new InvalidOperationException();

    public string Name => string.Empty;

    public int ExitCode => _handle.ExitCode;

    public bool HasExited => _handle.HasExited;

    public IMainModule? MainModule => null;

    public DateTime StartTime => default;

    private void OnHandleExited(object? sender, EventArgs e)
        => Exited?.Invoke(this, e);

    public Task WaitForExitAsync() => _handle.WaitForExitAsync();

    public void WaitForExit() => _handle.WaitForExitAsync().GetAwaiter().GetResult();

    public void Kill() => _handle.Terminate();

    public void Dispose()
    {
        _handle.Exited -= OnHandleExited;
        (_handle as IDisposable)?.Dispose();
    }
}
