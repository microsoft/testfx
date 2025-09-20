// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Extensions.TestFramework;

/// <summary>
/// Context passed to a test framework adapter when <see cref="ITestFramework.CloseTestSessionAsync(CloseTestSessionContext)"/> is called.
/// </summary>
public sealed class CloseTestSessionContext
{
    internal CloseTestSessionContext(SessionUid sessionUid, CancellationToken cancellationToken)
    {
        SessionUid = sessionUid;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Gets the unique identifier of the test session.
    /// </summary>
    public SessionUid SessionUid { get; }

    /// <summary>
    /// Gets the cancellation token used to cancel the operation.
    /// </summary>
    public CancellationToken CancellationToken { get; }
}
