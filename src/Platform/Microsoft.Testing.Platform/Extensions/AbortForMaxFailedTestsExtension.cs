// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Extensions;

internal sealed class AbortForMaxFailedTestsExtension : IDataConsumer
{
    private readonly int? _maxFailedTests;
    private readonly IGracefulStopTestExecutionCapability? _capability;
    private readonly IStopPoliciesService _policiesService;
    private readonly ITestApplicationCancellationTokenSource _testApplicationCancellationTokenSource;
    private int _failCount;

    public AbortForMaxFailedTestsExtension(
        ICommandLineOptions commandLineOptions,
        IGracefulStopTestExecutionCapability? capability,
        IStopPoliciesService policiesService,
        ITestApplicationCancellationTokenSource testApplicationCancellationTokenSource)
    {
        if (commandLineOptions.TryGetOptionArgumentList(MaxFailedTestsCommandLineOptionsProvider.MaxFailedTestsOptionKey, out string[]? args) &&
            int.TryParse(args[0], out int maxFailedTests) &&
            maxFailedTests > 0)
        {
            _maxFailedTests = maxFailedTests;
        }

        _capability = capability;
        _policiesService = policiesService;
        _testApplicationCancellationTokenSource = testApplicationCancellationTokenSource;
    }

    public Type[] DataTypesConsumed { get; } = [typeof(TestNodeUpdateMessage)];

    /// <inheritdoc />
    public string Uid { get; } = nameof(AbortForMaxFailedTestsExtension);

    /// <inheritdoc />
    public string Version { get; } = AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = nameof(AbortForMaxFailedTestsExtension);

    /// <inheritdoc />
    public string Description { get; } = PlatformResources.AbortForMaxFailedTestsDescription;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(_maxFailedTests.HasValue && _capability is not null);

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        var node = (TestNodeUpdateMessage)value;

        // If we are called, the extension is enabled, which means both _maxFailedTests and capability are not null.
        RoslynDebug.Assert(_maxFailedTests is not null);
        RoslynDebug.Assert(_capability is not null);

        TestNodeStateProperty testNodeStateProperty = node.TestNode.Properties.Single<TestNodeStateProperty>();
        if (TestNodePropertiesCategories.WellKnownTestNodeTestRunOutcomeFailedProperties.Any(t => t == testNodeStateProperty.GetType()) &&
            ++_failCount >= _maxFailedTests.Value &&
            // If already triggered, don't do it again.
            !_policiesService.IsMaxFailedTestsTriggered)
        {
            await _capability.StopTestExecutionAsync(_testApplicationCancellationTokenSource.CancellationToken);
            await _policiesService.ExecuteMaxFailedTestsCallbacksAsync(_maxFailedTests.Value, _testApplicationCancellationTokenSource.CancellationToken);
        }
    }
}
