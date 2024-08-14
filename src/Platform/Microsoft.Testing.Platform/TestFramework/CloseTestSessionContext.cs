// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Extensions.TestFramework;

/// <summary>
/// Context passed to a test framework adapter when <see cref="ITestFramework.CloseTestSessionAsync(CloseTestSessionContext)"/> is called.
/// </summary>
public sealed class CloseTestSessionContext : TestSessionContext
{
    internal CloseTestSessionContext(SessionUid sessionUid, ClientInfo client, CancellationToken cancellationToken)
        : base(sessionUid, client) => CancellationToken = cancellationToken;

    /// <summary>
    /// Gets the cancellation token used to cancel the operation.
    /// </summary>
    public CancellationToken CancellationToken { get; }
}
