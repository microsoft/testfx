// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Extensions.Policy.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.Policy;

[UnsupportedOSPlatform("browser")]
internal sealed class RetryDataConsumer : IDataConsumer, ITestSessionLifetimeHandler, IAsyncInitializableExtension
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICommandLineOptions _commandLineOptions;
    private RetryLifecycleCallbacks? _retryFailedTestsLifecycleCallbacks;
    private int _totalTests;

    public RetryDataConsumer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _commandLineOptions = _serviceProvider.GetCommandLineOptions();
    }

    public Type[] DataTypesConsumed => [typeof(TestNodeUpdateMessage)];

    public string Uid => nameof(RetryDataConsumer);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.RetryFailedTestsExtensionDisplayName;

    public string Description => ExtensionResources.RetryFailedTestsExtensionDescription;

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        var testNodeUpdateMessage = (TestNodeUpdateMessage)value;
        TestNodeStateProperty? nodeState = testNodeUpdateMessage.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>();
        if (nodeState is null)
        {
            return;
        }

        if (Array.IndexOf(TestNodePropertiesCategories.WellKnownTestNodeTestRunOutcomeFailedProperties, nodeState.GetType()) != -1)
        {
            ApplicationStateGuard.Ensure(_retryFailedTestsLifecycleCallbacks is not null);
            ApplicationStateGuard.Ensure(_retryFailedTestsLifecycleCallbacks.Client is not null);
            await _retryFailedTestsLifecycleCallbacks.Client.RequestReplyAsync<FailedTestRequest, VoidResponse>(new FailedTestRequest(testNodeUpdateMessage.TestNode.Uid), cancellationToken).ConfigureAwait(false);
        }

        if (Array.IndexOf(TestNodePropertiesCategories.WellKnownTestNodeTestRunOutcomeProperties, nodeState.GetType()) != -1)
        {
            _totalTests++;
        }
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        ApplicationStateGuard.Ensure(_retryFailedTestsLifecycleCallbacks is not null);
        ApplicationStateGuard.Ensure(_retryFailedTestsLifecycleCallbacks.Client is not null);
        await _retryFailedTestsLifecycleCallbacks.Client.RequestReplyAsync<TotalTestsRunRequest, VoidResponse>(new TotalTestsRunRequest(_totalTests), testSessionContext.CancellationToken).ConfigureAwait(false);
    }

    public Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
        => Task.CompletedTask;

    public Task<bool> IsEnabledAsync()

        => Task.FromResult(_commandLineOptions.IsOptionSet(RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName));

    public async Task InitializeAsync()
    {
        if (await IsEnabledAsync().ConfigureAwait(false))
        {
            _retryFailedTestsLifecycleCallbacks = _serviceProvider.GetRequiredService<RetryLifecycleCallbacks>();
        }
    }
}
