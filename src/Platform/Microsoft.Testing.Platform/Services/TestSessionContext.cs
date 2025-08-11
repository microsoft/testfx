// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Services;

/// <summary>
/// Represents the context of a test session.
/// </summary>
public sealed class TestSessionContext : ITestSessionContext
{
    internal TestSessionContext(SessionUid sessionUid, CancellationToken cancellationToken)
    {
        SessionUid = sessionUid;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Gets the unique identifier of the test session.
    /// </summary>
    public SessionUid SessionUid { get; }

    /// <summary>
    /// Gets the cancellation token associated with the test session.
    /// </summary>
    public CancellationToken CancellationToken { get; }
}
