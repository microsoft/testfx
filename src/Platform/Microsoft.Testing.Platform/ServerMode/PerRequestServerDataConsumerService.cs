// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using static Microsoft.Testing.Platform.Hosts.ServerTestHost;

namespace Microsoft.Testing.Platform.ServerMode;

/// <summary>
/// This class converts the events send to the message bus and sends these back to the client.
/// </summary>
internal sealed class PerRequestServerDataConsumer(IServiceProvider serviceProvider, IServerTestHost serverTestHost, Guid runId, ITask task) : IDataConsumer, ITestSessionLifetimeHandler, IDisposable
{
    private const int TestNodeUpdateDelayInMs = 200;

    private readonly ConcurrentDictionary<TestNodeUid, TestNodeStateStatistics> _testNodeUidToStateStatistics = new();
    private readonly ConcurrentDictionary<TestNodeUid, byte> _discoveredTestNodeUids = new();
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IServerTestHost _serverTestHost = serverTestHost;
    private readonly ITask _task = task;
    private readonly SemaphoreSlim _nodeAggregatorSemaphore = new(1);
    private readonly SemaphoreSlim _nodeUpdateSemaphore = new(1);
    private readonly ITestSessionContext _testSessionContext = serviceProvider.GetTestSessionContext();
    private readonly TaskCompletionSource<bool> _testSessionEnd = new();
    private Task? _idleUpdateTask;
    private TestNodeStateChangeAggregator _nodeUpdatesAggregator = new(runId);
    private bool _isDisposed;

    public Type[] DataTypesConsumed { get; }
        =
        [
            typeof(TestNodeUpdateMessage),
            typeof(FileArtifact),
            typeof(SessionFileArtifact),
            typeof(TestNodeFileArtifact),
        ];

    /// <inheritdoc />
    public string Uid => nameof(PerRequestServerDataConsumer);

    /// <inheritdoc />
    public string Version => AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName => string.Empty;

    /// <inheritdoc />
    public string Description => string.Empty;

    public List<Artifact> Artifacts { get; } = [];

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public TestNodeStatistics GetTestNodeStatistics()
    {
        var nodeStatistics = new TestNodeStatistics(_discoveredTestNodeUids.Count, 0, 0, 0, 0);
        foreach (KeyValuePair<TestNodeUid, TestNodeStateStatistics> entry in _testNodeUidToStateStatistics)
        {
            TestNodeStateStatistics statistics = entry.Value;

            nodeStatistics.TotalPassedRetries += statistics.TotalPassedRetries;
            nodeStatistics.TotalFailedRetries += statistics.TotalFailedRetries;

            if (statistics.HasPassed)
            {
                nodeStatistics.TotalPassedTests++;
            }
            else
            {
                nodeStatistics.TotalFailedTests++;
            }
        }

        return nodeStatistics;
    }

    internal /* for testing */ Task GetIdleUpdateTaskAsync() => _idleUpdateTask is not null ? _idleUpdateTask : Task.CompletedTask;

    private async Task ProcessTestNodeUpdateAsync(TestNodeUpdateMessage update, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            await _nodeAggregatorSemaphore.WaitAsync(cancellationToken);
            try
            {
                // Note: If there's no changes to aggregate kick off a background task,
                //       that will send the updates on idle.
                // Note: It's ok to do this before we aggregate the change, since the
                //       SendTestNodeUpdatesIfNecessaryAsync will have to grab the semaphore
                //       to complete and that will only happen if this method releases the semaphore.
                if (!_nodeUpdatesAggregator.HasChanges)
                {
                    // If idle task is not null observe it to throw in case of failed task.
                    if (_idleUpdateTask is not null)
                    {
                        // Observe possible exceptions
                        try
                        {
                            await _idleUpdateTask.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
                        }
                        catch (OperationCanceledException)
                        {
                            // We cannot check the token because it's possible that we're cancelled during the
                            // send of the information and that the current cancellation token is a combined one.
                        }
                    }

                    _idleUpdateTask = SendTestNodeUpdatesOnIdleAsync(_nodeUpdatesAggregator.RunId);
                }

                _nodeUpdatesAggregator.OnStateChange(update);
            }
            finally
            {
                _nodeAggregatorSemaphore.Release();
            }
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            // We do nothing we've been cancelled.
        }
    }

    private async Task SendTestNodeUpdatesOnIdleAsync(Guid runId)
    {
        // We get the PerRequestTestApplicationCooperativeLifetime that in server mode is linked to the per-request+global cancellation token.
        CancellationToken cancellationToken = _testSessionContext.CancellationToken;
        try
        {
            // We subscribe to the per request application lifetime
            using CancellationTokenRegistration registration = cancellationToken.Register(() => _testSessionEnd.SetCanceled());

            // When batch timer expire or we're at the end of the session we can unblock the message drain
            await Task.WhenAny(_task.Delay(TimeSpan.FromMilliseconds(TestNodeUpdateDelayInMs), cancellationToken), _testSessionEnd.Task);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await SendTestNodeUpdatesIfNecessaryAsync(runId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // We do nothing we've been cancelled.
        }
    }

    private async Task SendTestNodeUpdatesIfNecessaryAsync(Guid runId, CancellationToken cancellationToken)
    {
        // Note: We synchronize the entire method, so that if a caller awaits this method
        //       and the Task completes, all of the pending updates have been sent.
        //       We synchronize the aggregator access with a separate lock, so that sending
        //       the update message will not block the producers.
        await _nodeUpdateSemaphore.WaitAsync(cancellationToken);
        try
        {
            TestNodeStateChangedEventArgs? change = null;
            await _nodeAggregatorSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (_nodeUpdatesAggregator.HasChanges)
                {
                    change = _nodeUpdatesAggregator.BuildAggregatedChange();
                    _nodeUpdatesAggregator = new TestNodeStateChangeAggregator(runId);
                }
            }
            finally
            {
                _nodeAggregatorSemaphore.Release();
            }

            if (change is not null)
            {
                await _serverTestHost.SendTestUpdateAsync(change);
            }
        }
        finally
        {
            _nodeUpdateSemaphore.Release();
        }
    }

    public async Task OnTestSessionFinishingAsync(SessionUid sessionUid, CancellationToken cancellationToken)
    {
        try
        {
            // We signal the test session end so we can complete the flush.
            _testSessionEnd.SetResult(true);
            await GetIdleUpdateTaskAsync().TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // We do nothing we've been cancelled.
        }
    }

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (value is TestNodeUpdateMessage update)
        {
            await ProcessTestNodeUpdateAsync(update, cancellationToken);
            PopulateTestNodeStatistics(update);
        }
        else if (value is SessionFileArtifact sessionFileArtifact)
        {
            Artifacts.Add(new Artifact(sessionFileArtifact.FileInfo.FullName, dataProducer.Uid, "file", sessionFileArtifact.DisplayName, sessionFileArtifact.Description));
        }
        else if (value is FileArtifact file)
        {
            Artifacts.Add(new Artifact(file.FileInfo.FullName, dataProducer.Uid, "file", file.DisplayName, file.Description));
        }
        else if (value is TestNodeFileArtifact testNodeFileArtifact)
        {
            Artifacts.Add(new Artifact(testNodeFileArtifact.FileInfo.FullName, dataProducer.Uid, "file", testNodeFileArtifact.DisplayName, testNodeFileArtifact.Description));
        }
    }

    internal void PopulateTestNodeStatistics(TestNodeUpdateMessage message)
    {
        if (message.TestNode is null
            || message.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>() is not { } state)
        {
            return;
        }

        TestNodeUid testNodeUid = new(message.TestNode.Uid.Value);

        switch (state)
        {
            case DiscoveredTestNodeStateProperty:
                // We don't use the value for anything, we want a ConcurrentHashSet, but no such type is available.
                // We also ignore the result, if it is false, the key already existed, which is fine for us.
                _ = _discoveredTestNodeUids.TryAdd(testNodeUid, 0);
                break;

            case PassedTestNodeStateProperty:
                AddOrUpdateTestNodeStateStatistics(testNodeUid, hasPassed: true);
                break;

            case FailedTestNodeStateProperty:
            case ErrorTestNodeStateProperty:
            case CancelledTestNodeStateProperty:
            case TimeoutTestNodeStateProperty:
                AddOrUpdateTestNodeStateStatistics(testNodeUid, hasPassed: false);
                break;
        }
    }

    private void AddOrUpdateTestNodeStateStatistics(TestNodeUid testNodeUid, bool hasPassed)
    {
        if (!_testNodeUidToStateStatistics.TryGetValue(testNodeUid, out TestNodeStateStatistics existingStatistics))
        {
            _testNodeUidToStateStatistics.TryAdd(testNodeUid, new TestNodeStateStatistics { HasPassed = hasPassed });
            return;
        }

        if (hasPassed)
        {
            existingStatistics.TotalPassedRetries++;
        }
        else
        {
            existingStatistics.TotalFailedRetries++;
        }

        existingStatistics.HasPassed = hasPassed;
        _testNodeUidToStateStatistics[testNodeUid] = existingStatistics;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _nodeAggregatorSemaphore.Dispose();
            _nodeUpdateSemaphore.Dispose();
            _isDisposed = true;
        }
    }

    public Task OnTestSessionStartingAsync(SessionUid sessionUid, CancellationToken cancellationToken) => Task.CompletedTask;
}
