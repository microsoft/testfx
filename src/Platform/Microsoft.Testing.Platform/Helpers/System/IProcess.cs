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

    /// <inheritdoc cref="Process.MainModule" />
    IMainModule? MainModule { get; }

    /// <inheritdoc cref="Process.StartTime" />
    DateTime StartTime { get; }

    /// <summary>
    /// Instructs the Process component to wait indefinitely for the associated process to exit.
    /// </summary>
    /// <remarks>
    /// This overload is kept for binary compatibility with previously shipped extensions (for example
    /// the Retry extension) that were compiled against it; new in-box callers use the
    /// <see cref="WaitForExitAsync(CancellationToken)"/> overload.
    /// </remarks>
    Task WaitForExitAsync();

    /// <summary>
    /// Instructs the Process component to wait for the associated process to exit, or for the cancellationToken to be canceled.
    /// </summary>
    Task WaitForExitAsync(CancellationToken cancellationToken);

    /// <inheritdoc cref="Process.WaitForExit()" />
    void WaitForExit();

    /// <summary>
    /// Kills the process and try to kill all child processes.
    /// </summary>
    void Kill();
}
