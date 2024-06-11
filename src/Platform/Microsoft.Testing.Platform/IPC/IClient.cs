// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC;

internal interface IClient :
#if NETCOREAPP
    IAsyncDisposable,
#endif
    IDisposable
{
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken cancellationToken);

    Task<TResponse> RequestReplyAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken)
        where TRequest : IRequest
        where TResponse : IResponse;
}
