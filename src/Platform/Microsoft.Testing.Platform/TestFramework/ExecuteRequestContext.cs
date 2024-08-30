// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable RS0016 // Add public types and members to the declared API

using System.Diagnostics.CodeAnalysis;

using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;

namespace Microsoft.Testing.Platform.Extensions.TestFramework;

/// <summary>
/// This class represents the context that is passed to a test framework adapter when the <see cref="ITestFramework.ExecuteRequestAsync(ExecuteRequestContext)"/> method is called.
/// </summary>
/// <remarks>
/// It contains information about the request, message bus, semaphore, and cancellation token.
/// </remarks>
public sealed class ExecuteRequestContext
{
    private readonly IExecuteRequestCompletionNotifier _executeRequestCompletionNotifier;

    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    public ExecuteRequestContext(IRequest request, IMessageBus messageBus, IExecuteRequestCompletionNotifier executeRequestCompletionNotifier,
        CancellationToken cancellationToken)
    {
        Request = request;
        MessageBus = messageBus;
        _executeRequestCompletionNotifier = executeRequestCompletionNotifier;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Gets the request associated with the execution.
    /// </summary>
    public IRequest Request { get; }

    /// <summary>
    /// Gets the message bus used for reporting test execution events.
    /// </summary>
    public IMessageBus MessageBus { get; }

    /// <summary>
    /// Gets the cancellation token that can be used to cancel the execution.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Completes the execution request.
    /// </summary>
    public void Complete()
        => _executeRequestCompletionNotifier.Complete();
}
