// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

using Microsoft.Testing.Platform.ServerMode.IntegrationTests.Messages.V100;

namespace MSTest.Acceptance.IntegrationTests.Messages.V100;

public class TestNodeUpdateCollector
{
    private readonly TaskCompletionSource _taskCompletionSource = new();
    private readonly Func<TestNodeUpdate, bool>? _completeCollector;

    public ConcurrentBag<TestNodeUpdate> TestNodeUpdates { get; } = new();

    public TestNodeUpdateCollector(Func<TestNodeUpdate, bool>? completeCollectorWhen = null) => _completeCollector = completeCollectorWhen;

    public Task CollectNodeUpdatesAsync(TestNodeUpdate[] messages)
    {
        foreach (TestNodeUpdate nodeUpdate in messages)
        {
            if (_completeCollector is not null && _completeCollector(nodeUpdate))
            {
                if (!_taskCompletionSource.Task.IsCompleted)
                {
                    _taskCompletionSource.SetResult();
                }
            }

            TestNodeUpdates.Add(nodeUpdate);
        }

        return Task.CompletedTask;
    }

    public Task IsComplete => _taskCompletionSource.Task;
}
