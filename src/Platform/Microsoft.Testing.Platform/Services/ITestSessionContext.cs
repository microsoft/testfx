// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Services;

/// <summary>
/// Interface representing the context of a test session.
/// It's used in ITestSessionLifetimeHandler to provide information about the session.
/// </summary>
public interface ITestSessionContext
{
    /// <summary>
    /// Gets a unique identifier for the test session.
    /// </summary>
    SessionUid SessionId { get; } // TODO: Should this rename to SessionUid?

    /// <summary>
    /// Gets the cancellation token for the test session.
    /// </summary>
    CancellationToken CancellationToken { get; }
}
