// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Services;

/// <summary>
/// Source of the platform's lifetime cancellation tokens. Exposes a two-phase
/// shutdown model (see Issue #5345 / RFC "Phased graceful shutdown for MTP"):
/// <list type="bullet">
///   <item><description><see cref="DrainingToken"/> — set when the platform
///     enters the Draining phase (Ctrl+C, programmatic <see cref="Cancel"/>,
///     test session abort). Consumers should stop dispatching new work and
///     flush in-flight state.</description></item>
///   <item><description><see cref="AbortingToken"/> — set when the platform
///     enters the Aborting phase (2nd Ctrl+C, grace period elapsed,
///     programmatic <see cref="Abort"/>). Consumers should bail out of
///     long-running work as fast as possible.</description></item>
/// </list>
/// <see cref="CancellationToken"/> is kept as the back-compat alias for
/// <see cref="DrainingToken"/>; existing consumers do not need to change.
/// </summary>
internal interface ITestApplicationCancellationTokenSource
{
    /// <summary>
    /// Gets the back-compat alias for <see cref="DrainingToken"/>.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets the token that is signalled when the platform enters the Draining phase.
    /// </summary>
    CancellationToken DrainingToken { get; }

    /// <summary>
    /// Gets the token that is signalled when the platform enters the Aborting phase.
    /// </summary>
    CancellationToken AbortingToken { get; }

    /// <summary>
    /// Request the Draining phase. Idempotent.
    /// </summary>
    void Cancel();

    /// <summary>
    /// Request the Aborting phase. Idempotent. Equivalent to a second Ctrl+C.
    /// </summary>
    void Abort();
}
