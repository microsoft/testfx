// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;

namespace Microsoft.Testing.Extensions.TestFramework;

/// <summary>
/// Context passed to a test framework adapter when <see cref="ITestFramework.ExecuteRequestAsync(ExecuteRequestContext)"/> is called.
/// </summary>
public sealed class ExecuteRequestContext
{
    private readonly SemaphoreSlim _semaphore;

    internal ExecuteRequestContext(IRequest request, IMessageBus messageBus, SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        Request = request;
        MessageBus = messageBus;
        _semaphore = semaphore;
        CancellationToken = cancellationToken;
    }

    public IRequest Request { get; }

    public IMessageBus MessageBus { get; }

    public CancellationToken CancellationToken { get; }

    public void Complete()
        => _semaphore.Release();
}
