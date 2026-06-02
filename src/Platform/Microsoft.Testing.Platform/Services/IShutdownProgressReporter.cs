// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Services;

/// <summary>
/// Tracks units of work that the platform is awaiting during shutdown so that, after
/// cancellation has been requested, the user can be told which extensions are still
/// running and how long each one has been blocking.
/// </summary>
/// <remarks>
/// Implementations must be thread-safe. Callers wrap shutdown-relevant awaits with
/// <c>using (reporter.Track(uid, displayName, phase)) { await ... }</c>; on the
/// happy path (no cancellation) this is effectively a no-op beyond bookkeeping.
/// </remarks>
internal interface IShutdownProgressReporter
{
    /// <summary>
    /// Registers a unit of in-flight work and returns a disposable that removes it.
    /// </summary>
    /// <param name="uid">Stable identifier of the extension / consumer being awaited.</param>
    /// <param name="displayName">Human-readable name surfaced to the user.</param>
    /// <param name="phase">Short label describing what we are awaiting (e.g. <c>OnTestSessionFinishingAsync</c>).</param>
    /// <returns>A disposable that removes the tracker when the awaited work completes.</returns>
    IDisposable Track(string uid, string displayName, string phase);
}
