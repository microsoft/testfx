// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Telemetry;

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
    private readonly TestExecutionActivityBroker? _testExecutionActivityBroker;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecuteRequestContext"/> class.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="messageBus">The message bus.</param>
    /// <param name="executeRequestCompletionNotifier">The request completion notifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <remarks>
    /// This API is experimental. It may change, break, or be removed at any time without notice.
    /// </remarks>
    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    public ExecuteRequestContext(IRequest request, IMessageBus messageBus, IExecuteRequestCompletionNotifier executeRequestCompletionNotifier,
        CancellationToken cancellationToken)
        : this(request, messageBus, executeRequestCompletionNotifier, cancellationToken, testExecutionActivityBroker: null)
    {
    }

    internal ExecuteRequestContext(
        IRequest request,
        IMessageBus messageBus,
        IExecuteRequestCompletionNotifier executeRequestCompletionNotifier,
        CancellationToken cancellationToken,
        TestExecutionActivityBroker? testExecutionActivityBroker)
    {
        Request = request;
        MessageBus = messageBus;
        _executeRequestCompletionNotifier = executeRequestCompletionNotifier;
        CancellationToken = cancellationToken;
        _testExecutionActivityBroker = testExecutionActivityBroker;
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
    /// Starts a canonical test execution and publishes its in-progress message.
    /// </summary>
    /// <param name="dataProducer">The data producer reporting the test execution.</param>
    /// <param name="inProgressMessage">The in-progress test node update.</param>
    /// <returns>A task containing the started test execution.</returns>
    /// <remarks>
    /// This API is experimental. It may change, break, or be removed at any time without notice.
    /// </remarks>
    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    public async Task<TestExecution> StartTestExecutionAsync(
        IDataProducer dataProducer,
        TestNodeUpdateMessage inProgressMessage)
    {
        _ = dataProducer ?? throw new ArgumentNullException(nameof(dataProducer));
        _ = inProgressMessage ?? throw new ArgumentNullException(nameof(inProgressMessage));
        CancellationToken.ThrowIfCancellationRequested();

        if (!inProgressMessage.TestNode.Properties.Any<InProgressTestNodeStateProperty>())
        {
            throw new ArgumentException("The test execution start message must contain an in-progress state.", nameof(inProgressMessage));
        }

        TestExecutionActivityReservation? reservation = _testExecutionActivityBroker?.Reserve(
            inProgressMessage.TestNode,
            inProgressMessage.ParentTestNodeUid);
        if (reservation is not null)
        {
            inProgressMessage.TestNode.Properties.Add(new TestExecutionActivityProperty(reservation, isFinalResult: false));
        }

        try
        {
            await MessageBus.PublishAsync(dataProducer, inProgressMessage).ConfigureAwait(false);
            CancellationToken.ThrowIfCancellationRequested();
            reservation?.Activate();
        }
        catch
        {
            TestExecution.Abandon(reservation, DateTimeOffset.UtcNow);
            throw;
        }

        return new TestExecution(
            MessageBus,
            dataProducer,
            inProgressMessage.SessionUid,
            inProgressMessage.TestNode.Uid,
            inProgressMessage.TestNode.DisplayName,
            inProgressMessage.ParentTestNodeUid,
            reservation);
    }

    /// <summary>
    /// Completes the execution request.
    /// </summary>
    public void Complete()
        => _executeRequestCompletionNotifier.Complete();
}
