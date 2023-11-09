// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Services;

internal sealed class TestSessionContext : ITestSessionContext
{
    public TestSessionContext(CancellationToken cancellationToken)
    {
        CancellationToken = cancellationToken;
    }

    public SessionUid SessionId { get; } = new(Guid.NewGuid().ToString());

    public CancellationToken CancellationToken { get; }
}
