// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Services;

internal sealed class TestCoverageResult : ITestCoverageResult
{
    private readonly List<TestCoverageMessage> _coverageEntries = [];
    private readonly List<TestCoverageThresholdMessage> _thresholdEntries = [];

    public string Uid => nameof(TestCoverageResult);

    public string Version => PlatformVersion.Version;

    public string DisplayName => "Test coverage result";

    public string Description => "Consumes and tracks test coverage data and threshold results.";

    public Type[] DataTypesConsumed { get; } = [typeof(TestCoverageMessage), typeof(TestCoverageThresholdMessage)];

    public bool HasCoverageThresholdFailure { get; private set; }

    public IReadOnlyList<TestCoverageThresholdMessage> ThresholdEntries => _thresholdEntries;

    public IReadOnlyList<TestCoverageMessage> CoverageEntries => _coverageEntries;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public void Reset()
    {
        _coverageEntries.Clear();
        _thresholdEntries.Clear();
        HasCoverageThresholdFailure = false;
    }

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        switch (value)
        {
            case TestCoverageMessage coverageMessage:
                _coverageEntries.Add(coverageMessage);
                break;

            case TestCoverageThresholdMessage thresholdMessage:
                _thresholdEntries.Add(thresholdMessage);
                if (!thresholdMessage.Passed)
                {
                    HasCoverageThresholdFailure = true;
                }

                break;
        }

        return Task.CompletedTask;
    }
}
