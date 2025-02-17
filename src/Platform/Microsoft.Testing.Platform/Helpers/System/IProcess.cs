// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal interface IProcess : IDisposable
{
    event EventHandler Exited;

    /// <inheritdoc cref="System.Diagnostics.Process.Id" />
    [UnsupportedOSPlatform("browser")]
    int Id { get; }

    /// <inheritdoc cref="System.Diagnostics.Process.ProcessName" />
    [UnsupportedOSPlatform("browser")]
    string Name { get; }

    /// <inheritdoc cref="System.Diagnostics.Process.ExitCode" />
    [UnsupportedOSPlatform("browser")]
    int ExitCode { get; }

    /// <inheritdoc cref="System.Diagnostics.Process.HasExited" />
    [UnsupportedOSPlatform("browser")]
    bool HasExited { get; }

#if NETCOREAPP
    /// <inheritdoc cref="System.Diagnostics.Process.MainModule" />
    [UnsupportedOSPlatform("browser")]
    IMainModule? MainModule { get; }
#else
    /// <inheritdoc cref="System.Diagnostics.Process.MainModule" />
    IMainModule MainModule { get; }
#endif

    /// <summary>
    /// Instructs the Process component to wait for the associated process to exit, or for the cancellationToken to be canceled.
    /// </summary>
    [UnsupportedOSPlatform("browser")]
    Task WaitForExitAsync();

    /// <inheritdoc cref="System.Diagnostics.Process.WaitForExit()" />
    [UnsupportedOSPlatform("browser")]
    void WaitForExit();

    /// <inheritdoc cref="System.Diagnostics.Process.Kill()" />
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    void Kill();
}
