// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Extensions.TestHost;

/// <summary>
/// Represents an interface for handling the lifetime of a test session.
/// </summary>
public interface ITestSessionLifetimeHandler : ITestHostExtension
{
    /// <summary>
    /// Called when a test session is starting.
    /// </summary>
    /// <param name="sessionUid">The unique identifier of the session.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task OnTestSessionStartingAsync(SessionUid sessionUid, CancellationToken cancellationToken);

    /// <summary>
    /// Called when a test session is finishing.
    /// </summary>
    /// <param name="sessionUid">The unique identifier of the session.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task OnTestSessionFinishingAsync(SessionUid sessionUid, CancellationToken cancellationToken);
}
