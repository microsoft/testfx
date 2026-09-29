// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Extensions.TestFramework;

/// <summary>
/// Represents one canonical test execution occurrence.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public sealed class TestExecution : IDisposable
{
    private const int Active = 0;
    private const int Completing = 1;
    private const int Completed = 2;
    private const int Disposed = 3;

    private readonly IMessageBus _messageBus;
    private readonly IDataProducer _dataProducer;
    private readonly SessionUid _sessionUid;
    private readonly TestNodeUid _testNodeUid;
    private readonly string _testNodeDisplayName;
    private readonly TestNodeUid? _parentTestNodeUid;
    private readonly TestExecutionActivityReservation? _reservation;
    private int _state;

    internal TestExecution(
        IMessageBus messageBus,
        IDataProducer dataProducer,
        SessionUid sessionUid,
        TestNodeUid testNodeUid,
        string testNodeDisplayName,
        TestNodeUid? parentTestNodeUid,
        TestExecutionActivityReservation? reservation)
    {
        _messageBus = messageBus;
        _dataProducer = dataProducer;
        _sessionUid = sessionUid;
        _testNodeUid = testNodeUid;
        _testNodeDisplayName = testNodeDisplayName;
        _parentTestNodeUid = parentTestNodeUid;
        _reservation = reservation;
    }

    /// <summary>
    /// Runs a callback in the canonical context of this test execution.
    /// </summary>
    /// <param name="callback">The callback to run.</param>
    public void Run(Action callback)
    {
        _ = callback ?? throw new ArgumentNullException(nameof(callback));
        ThrowIfNotActive();

        using IDisposable? scope = _reservation?.Enter();
        callback();
    }

    /// <summary>
    /// Runs a callback in the canonical context of this test execution and returns its result.
    /// </summary>
    /// <typeparam name="T">The callback result type.</typeparam>
    /// <param name="callback">The callback to run.</param>
    /// <returns>The callback result.</returns>
    public T Run<T>(Func<T> callback)
    {
        _ = callback ?? throw new ArgumentNullException(nameof(callback));
        ThrowIfNotActive();

        using IDisposable? scope = _reservation?.Enter();
        return callback();
    }

    /// <summary>
    /// Runs an asynchronous callback in the canonical context of this test execution.
    /// </summary>
    /// <param name="callback">The callback to run.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RunAsync(Func<Task> callback)
    {
        _ = callback ?? throw new ArgumentNullException(nameof(callback));
        ThrowIfNotActive();

        using IDisposable? scope = _reservation?.Enter();
        await callback().ConfigureAwait(false);
    }

    /// <summary>
    /// Runs an asynchronous callback in the canonical context of this test execution and returns its result.
    /// </summary>
    /// <typeparam name="T">The callback result type.</typeparam>
    /// <param name="callback">The callback to run.</param>
    /// <returns>A task containing the callback result.</returns>
    public async Task<T> RunAsync<T>(Func<Task<T>> callback)
    {
        _ = callback ?? throw new ArgumentNullException(nameof(callback));
        ThrowIfNotActive();

        using IDisposable? scope = _reservation?.Enter();
        return await callback().ConfigureAwait(false);
    }

    /// <summary>
    /// Completes this test execution by publishing its ordered result messages.
    /// </summary>
    /// <param name="resultMessages">
    /// The result messages to publish in order. The last message is the final result. An empty collection
    /// publishes an execution-completed update without inventing an outcome.
    /// </param>
    /// <param name="executionEndTime">The producer-observed time at which test execution ended.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task CompleteAsync(
        IReadOnlyList<TestNodeUpdateMessage> resultMessages,
        DateTimeOffset executionEndTime)
    {
        _ = resultMessages ?? throw new ArgumentNullException(nameof(resultMessages));

        TestNodeUpdateMessage[] messages = resultMessages.Count == 0
            ?
            [
                new TestNodeUpdateMessage(
                    _sessionUid,
                    new TestNode
                    {
                        Uid = _testNodeUid,
                        DisplayName = _testNodeDisplayName,
                        Properties = new PropertyBag(TestNodeExecutionCompletedProperty.CachedInstance),
                    },
                    _parentTestNodeUid),
            ]
            : new TestNodeUpdateMessage[resultMessages.Count];
        for (int i = 0; i < resultMessages.Count; i++)
        {
            TestNodeUpdateMessage message = resultMessages[i]
                ?? throw new ArgumentException("The result messages cannot contain null items.", nameof(resultMessages));
            if (!string.Equals(message.SessionUid.Value, _sessionUid.Value, StringComparison.Ordinal)
                || !message.TestNode.Uid.Equals(_testNodeUid)
                || !string.Equals(message.ParentTestNodeUid?.Value, _parentTestNodeUid?.Value, StringComparison.Ordinal))
            {
                throw new ArgumentException("Every result message must belong to the started test execution.", nameof(resultMessages));
            }

            if (!IsResult(message.TestNode))
            {
                throw new ArgumentException("Every result message must contain a completed test result state.", nameof(resultMessages));
            }

            messages[i] = message;
        }

        int previousState = Interlocked.CompareExchange(ref _state, Completing, Active);
        if (previousState == Disposed)
        {
            throw new ObjectDisposedException(nameof(TestExecution));
        }

        if (previousState != Active)
        {
            throw new InvalidOperationException("The test execution has already been completed.");
        }

        _reservation?.RecordExecutionEnd(executionEndTime);

        try
        {
            for (int i = 0; i < messages.Length; i++)
            {
                if (_reservation is not null)
                {
                    messages[i].TestNode.Properties.Add(
                        new TestExecutionActivityProperty(_reservation, isFinalResult: i == messages.Length - 1));
                }

                await _messageBus.PublishAsync(_dataProducer, messages[i]).ConfigureAwait(false);
            }

            if (resultMessages.Count == 0)
            {
                _reservation?.CompleteWithoutResult();
            }
        }
        catch
        {
            Abandon(_reservation, executionEndTime);
            throw;
        }
        finally
        {
            Volatile.Write(ref _state, Completed);
        }
    }

    /// <summary>
    /// Abandons an uncompleted test execution without publishing or inventing a result.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _state, Disposed, Active) == Active)
        {
            Abandon(_reservation, DateTimeOffset.UtcNow);
        }
    }

    internal static void Abandon(TestExecutionActivityReservation? reservation, DateTimeOffset endTime)
    {
        if (reservation is null)
        {
            return;
        }

        try
        {
            reservation.RecordExecutionEnd(endTime);
            reservation.CompleteWithoutResult();
        }
        catch
        {
            // Dispose and start-failure cleanup must never mask the caller's exception.
        }
    }

    private static bool IsResult(TestNode testNode)
        => testNode.Properties.Any<TestNodeExecutionCompletedProperty>()
        || testNode.Properties.SingleOrDefault<TestNodeStateProperty>() is
            PassedTestNodeStateProperty
            or FailedTestNodeStateProperty
            or ErrorTestNodeStateProperty
            or TimeoutTestNodeStateProperty
#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
            or CancelledTestNodeStateProperty
#pragma warning restore CS0618, MTP0001 // Type or member is obsolete
            or SkippedTestNodeStateProperty;

    private void ThrowIfNotActive()
    {
        int state = Volatile.Read(ref _state);
        if (state == Disposed)
        {
            throw new ObjectDisposedException(nameof(TestExecution));
        }

        if (state != Active)
        {
            throw new InvalidOperationException("The test execution has already been completed.");
        }
    }
}
