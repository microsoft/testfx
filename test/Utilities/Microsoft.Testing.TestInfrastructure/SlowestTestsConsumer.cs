// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.TestInfrastructure;

public sealed class SlowestTestsConsumer : IDataConsumer, ITestSessionLifetimeHandler
{
    private readonly List<(string TestId, double Milliseconds)> _testPerf = [];

    public Type[] DataTypesConsumed => [typeof(TestNodeUpdateMessage)];

    public string Uid => nameof(SlowestTestsConsumer);

    public string Version => "1.0.0";

    public string DisplayName => nameof(SlowestTestsConsumer);

    public string Description => nameof(SlowestTestsConsumer);

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        if (value is not TestNodeUpdateMessage testNodeUpdatedMessage
            || testNodeUpdatedMessage.TestNode.Properties.SingleOrDefault<PassedTestNodeStateProperty>() is null)
        {
            return Task.CompletedTask;
        }

        double milliseconds = testNodeUpdatedMessage.TestNode.Properties.Single<TimingProperty>().GlobalTiming.Duration.TotalMilliseconds;
        _testPerf.Add((testNodeUpdatedMessage.TestNode.Uid, milliseconds));

        return Task.CompletedTask;
    }

    public Task OnTestSessionFinishingAsync(SessionUid sessionUid, CancellationToken cancellationToken)
    {
        //Console.WriteLine("Slowest 10 tests");
        //foreach ((string testId, double milliseconds) in _testPerf.OrderByDescending(x => x.Milliseconds).Take(10))
        //{
        //    Console.WriteLine($"{testId} {TimeSpan.FromMilliseconds(milliseconds).TotalSeconds:F5}s");
        //}

        return Task.CompletedTask;
    }

    public Task OnTestSessionStartingAsync(SessionUid sessionUid, CancellationToken cancellationToken) => Task.CompletedTask;
}
