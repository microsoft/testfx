// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal interface IProcess : IDisposable
{
    event EventHandler Exited;

    /// <inheritdoc cref="Process.Id" />
    int Id { get; }

    /// <inheritdoc cref="Process.ProcessName" />
    string Name { get; }

    /// <inheritdoc cref="Process.ExitCode" />
    int ExitCode { get; }

    /// <inheritdoc cref="Process.HasExited" />
    bool HasExited { get; }

#if NETCOREAPP
    /// <inheritdoc cref="Process.MainModule" />
    IMainModule? MainModule { get; }
#else
    /// <inheritdoc cref="Process.MainModule" />
    IMainModule MainModule { get; }
#endif

    /// <summary>
    /// Instructs the Process component to wait for the associated process to exit, or for the cancellationToken to be canceled.
    /// </summary>
    Task WaitForExitAsync();

    /// <inheritdoc cref="Process.WaitForExit()" />
    void WaitForExit();

    /// <inheritdoc cref="Process.Kill()" />
    void Kill();
}
