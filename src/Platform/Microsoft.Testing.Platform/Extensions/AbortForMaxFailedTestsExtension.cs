// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
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
    public string Uid => nameof(AbortForMaxFailedTestsExtension);

    /// <inheritdoc />
    public string Version => PlatformVersion.Version;

    /// <inheritdoc />
    public string DisplayName => nameof(AbortForMaxFailedTestsExtension);

    /// <inheritdoc />
    public string Description { get; } = PlatformResources.AbortForMaxFailedTestsDescription;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(_maxFailedTests.HasValue && _capability is not null);

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        var node = (TestNodeUpdateMessage)value;

        // If we are called, the extension is enabled, which means both _maxFailedTests and capability are not null.
        // Guard defensively so we are a no-op (rather than throwing) if invoked while effectively disabled.
        if (_maxFailedTests is not { } maxFailedTests || _capability is not { } capability)
        {
            return;
        }

        TestNodeStateProperty? testNodeStateProperty = node.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>();
        if (testNodeStateProperty is null)
        {
            return;
        }

        if (testNodeStateProperty is FailedTestNodeStateProperty or ErrorTestNodeStateProperty
                or TimeoutTestNodeStateProperty
#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
                or CancelledTestNodeStateProperty
#pragma warning restore CS0618, MTP0001 // Type or member is obsolete
            && ++_failCount >= maxFailedTests &&
            // If already triggered, don't do it again.
            !_policiesService.IsMaxFailedTestsTriggered)
        {
            await capability.StopTestExecutionAsync(_testApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);
            await _policiesService.ExecuteMaxFailedTestsCallbacksAsync(maxFailedTests, _testApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);
        }
    }
}
