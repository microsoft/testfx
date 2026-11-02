// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.TestInfrastructure;

public sealed class SlowestTestsConsumer : IDataConsumer, ITestSessionLifetimeHandler
{
    private readonly List<(string TestId, string DisplayName, string Outcome, TimeSpan Duration)> _testPerf = [];
    private readonly Action<string> _writeLine;
    private readonly bool _isEnabled;
    private readonly string _moduleName;
    private readonly string _targetFramework;
#if NET9_0_OR_GREATER
    private readonly System.Threading.Lock _stateLock = new();
#else
    private readonly object _stateLock = new();
#endif

    public SlowestTestsConsumer()
        : this(Console.WriteLine)
    {
    }

    public SlowestTestsConsumer(ICommandLineOptions commandLineOptions)
        : this(commandLineOptions, Environment.GetEnvironmentVariable("TF_BUILD"))
    {
    }

    internal SlowestTestsConsumer(ICommandLineOptions commandLineOptions, string? tfBuild)
        : this(
            Console.WriteLine,
            isEnabled: !commandLineOptions.IsOptionSet("report-azdo-summary")
                || !string.Equals(tfBuild, "true", StringComparison.OrdinalIgnoreCase))
    {
    }

    internal SlowestTestsConsumer(
        Action<string> writeLine,
        bool isEnabled = true,
        string? moduleName = null,
        string? targetFramework = null)
    {
        _writeLine = writeLine;
        _isEnabled = isEnabled;
        _moduleName = moduleName ?? Assembly.GetEntryAssembly()?.GetName().Name ?? "unknown";
#if NETCOREAPP
        _targetFramework = targetFramework ?? AppContext.TargetFrameworkName ?? "unknown";
#else
        _targetFramework = targetFramework ?? AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName ?? "unknown";
#endif
    }

    public Type[] DataTypesConsumed => [typeof(TestNodeUpdateMessage)];

    public string Uid => nameof(SlowestTestsConsumer);

    public string Version => "1.0.0";

    public string DisplayName => nameof(SlowestTestsConsumer);

    public string Description => nameof(SlowestTestsConsumer);

    public Task<bool> IsEnabledAsync() => Task.FromResult(_isEnabled);

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        if (!_isEnabled
            || value is not TestNodeUpdateMessage testNodeUpdatedMessage
            || testNodeUpdatedMessage.TestNode.Properties.SingleOrDefault<RetryAttemptProperty>() is { IsSuperseded: true })
        {
            return Task.CompletedTask;
        }

        string? outcome = testNodeUpdatedMessage.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>() switch
        {
            PassedTestNodeStateProperty => "passed",
            FailedTestNodeStateProperty or ErrorTestNodeStateProperty or TimeoutTestNodeStateProperty => "failed",
#pragma warning disable CS0618, MTP0001
            CancelledTestNodeStateProperty => "failed",
#pragma warning restore CS0618, MTP0001
            SkippedTestNodeStateProperty => "skipped",
            _ => null,
        };
        TimingProperty? timing = testNodeUpdatedMessage.TestNode.Properties.SingleOrDefault<TimingProperty>();
        if (outcome is not null && timing is not null && timing.GlobalTiming.Duration > TimeSpan.Zero)
        {
            lock (_stateLock)
            {
                _testPerf.Add((testNodeUpdatedMessage.TestNode.Uid, testNodeUpdatedMessage.TestNode.DisplayName, outcome, timing.GlobalTiming.Duration));
            }
        }

        return Task.CompletedTask;
    }

    public Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        (string TestId, string DisplayName, string Outcome, TimeSpan Duration)[] snapshot;
        lock (_stateLock)
        {
            snapshot = [.. _testPerf];
        }

        if (_isEnabled && snapshot.Length > 0)
        {
            _writeLine($"Slowest 10 tests — {_moduleName} ({_targetFramework}):");
            foreach ((_, string displayName, string outcome, TimeSpan duration) in snapshot
                .OrderByDescending(static test => test.Duration)
                .ThenBy(static test => test.DisplayName, StringComparer.Ordinal)
                .ThenBy(static test => test.TestId, StringComparer.Ordinal)
                .Take(10))
            {
                _writeLine($"  {duration.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture)}s [{outcome}] {displayName}");
            }
        }

        return Task.CompletedTask;
    }

    public Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        lock (_stateLock)
        {
            _testPerf.Clear();
        }

        return Task.CompletedTask;
    }
}
