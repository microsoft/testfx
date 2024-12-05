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
    private readonly IStopTestExecutionCapability? _capability;
    private readonly PoliciesService _policiesService;
    private readonly CancellationToken _cancellationToken;
    private int _failCount;

    public AbortForMaxFailedTestsExtension(ICommandLineOptions commandLineOptions, IStopTestExecutionCapability? capability, PoliciesService policiesService, CancellationToken cancellationToken)
    {
        if (commandLineOptions.TryGetOptionArgumentList(PlatformCommandLineProvider.MaxFailedTestsOptionKey, out string[]? args) &&
            int.TryParse(args[0], out int maxFailedTests) &&
            maxFailedTests > 0)
        {
            _maxFailedTests = maxFailedTests;
        }

        _capability = capability;
        _policiesService = policiesService;
        _cancellationToken = cancellationToken;
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

        // If we are called, the extension is enabled, which means both _maxFailedTests and _cancellationTokenSource are not null.
        RoslynDebug.Assert(_maxFailedTests is not null);
        RoslynDebug.Assert(_capability is not null);

        int maxFailed = _maxFailedTests.Value;
        TestNodeStateProperty testNodeStateProperty = node.TestNode.Properties.Single<TestNodeStateProperty>();
        if (TestNodePropertiesCategories.WellKnownTestNodeTestRunOutcomeFailedProperties.Any(t => t == testNodeStateProperty.GetType()) &&
            Interlocked.Increment(ref _failCount) > maxFailed)
        {
            await _capability.StopTestExecutionAsync(_cancellationToken);
            await _policiesService.ExecuteOnStopTestExecutionCallbacks(_cancellationToken);
        }
    }
}
