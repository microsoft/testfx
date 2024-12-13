// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.ServerMode;

internal sealed class PerRequestTestSessionContext(CancellationToken rpcCancellationToken, CancellationToken testApplicationcancellationToken) : ITestSessionContext, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(rpcCancellationToken, testApplicationcancellationToken);

    public SessionUid SessionId { get; } = new(Guid.NewGuid().ToString());

    public CancellationToken CancellationToken => _cancellationTokenSource.Token;

    public void Dispose()
        => _cancellationTokenSource.Dispose();
}
