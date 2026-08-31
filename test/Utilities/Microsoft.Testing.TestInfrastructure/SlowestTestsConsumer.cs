// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.TestInfrastructure;

public sealed class SlowestTestsConsumer : IDataConsumer, ITestSessionLifetimeHandler
{
    private readonly List<(string TestId, string DisplayName, double Milliseconds)> _testPerf = [];
    private readonly Action<string> _writeLine;

    public SlowestTestsConsumer()
        : this(Console.WriteLine)
    {
    }

    internal SlowestTestsConsumer(Action<string> writeLine)
        => _writeLine = writeLine;

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
        _testPerf.Add((testNodeUpdatedMessage.TestNode.Uid, testNodeUpdatedMessage.TestNode.DisplayName, milliseconds));

        return Task.CompletedTask;
    }

    public Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        _writeLine("Slowest 10 tests:");
        foreach ((_, string displayName, double milliseconds) in _testPerf.OrderByDescending(x => x.Milliseconds).Take(10))
        {
            _writeLine($"  {TimeSpan.FromMilliseconds(milliseconds).TotalSeconds:F5}s {displayName}");
        }

        return Task.CompletedTask;
    }

    public Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext) => Task.CompletedTask;
}
