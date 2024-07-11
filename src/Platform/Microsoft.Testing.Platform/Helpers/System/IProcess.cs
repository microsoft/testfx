// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal interface IProcess : IDisposable
{
    public event EventHandler Exited;

    /// <inheritdoc cref="System.Diagnostics.Process.Id" />
    int Id { get; }

    /// <inheritdoc cref="System.Diagnostics.Process.ExitCode" />
    int ExitCode { get; }

    /// <inheritdoc cref="System.Diagnostics.Process.HasExited" />
    bool HasExited { get; }

#if NETCOREAPP
    /// <inheritdoc cref="System.Diagnostics.Process.MainModule" />
    IMainModule? MainModule { get; }
#else
    /// <inheritdoc cref="System.Diagnostics.Process.MainModule" />
    IMainModule MainModule { get; }
#endif

#if NETCOREAPP
    /// <inheritdoc cref="System.Diagnostics.Process.WaitForExitAsync(CancellationToken)" />
    Task WaitForExitAsync();

#endif

    /// <inheritdoc cref="System.Diagnostics.Process.WaitForExit()" />
    void WaitForExit();

    /// <inheritdoc cref="System.Diagnostics.Process.Kill()" />
    void Kill();
}
