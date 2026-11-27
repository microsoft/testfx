// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.ServerMode;

/// <summary>
/// The channel through which the process that <c>dotnet test</c> accepted as invocation-lifetime owner coordinates the
/// invocation setup and cleanup with the other test modules of the same <c>dotnet test</c> invocation (protocol 1.5.0).
/// </summary>
internal interface IInvocationLifetimeCoordinationChannel
{
    /// <summary>
    /// Gets a value indicating whether <c>dotnet test</c> accepted this process as the coordinated owner during the
    /// latest handshake.
    /// </summary>
    bool IsInvocationLifetimeCoordinationAccepted { get; }

    Task SendInvocationSetupCompletedAsync(bool succeeded, string? errorMessage, IReadOnlyList<InvocationLifetimeOutput> outputs);

    Task SendModuleTestsCompletedAsync(int exitCode);

    /// <summary>
    /// Waits until <c>dotnet test</c> requests the invocation cleanup, after every other module completed.
    /// </summary>
    /// <returns>
    /// The aggregate verdict of the invocation, or <see langword="null"/> when the request can no longer arrive
    /// (for example because <c>dotnet test</c> went away).
    /// </returns>
    Task<InvocationCleanupRequest?> WaitForInvocationCleanupRequestAsync(CancellationToken cancellationToken);

    Task SendInvocationCleanupCompletedAsync(bool succeeded, string? errorMessage);
}

internal sealed record InvocationCleanupRequest(int AggregateExitCode, bool IsCanceled);
