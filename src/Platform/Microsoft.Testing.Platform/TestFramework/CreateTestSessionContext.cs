// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Extensions.TestFramework;

/// <summary>
/// Context passed to a test framework adapter when <see cref="ITestFramework.CreateTestSessionAsync(CreateTestSessionContext)"/> is called.
/// </summary>
public sealed class CreateTestSessionContext : TestSessionContext
{
    internal CreateTestSessionContext(SessionUid sessionUid, ClientInfo client, CancellationToken cancellationToken)
        : base(sessionUid, client)
    {
        CancellationToken = cancellationToken;
    }

    public CancellationToken CancellationToken { get; }
}
