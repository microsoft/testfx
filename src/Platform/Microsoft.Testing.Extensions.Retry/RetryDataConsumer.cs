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
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.Policy;

[UnsupportedOSPlatform("browser")]
internal sealed class RetryDataConsumer : IDataConsumer, ITestSessionLifetimeHandler, IAsyncInitializableExtension
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICommandLineOptions _commandLineOptions;
    private RetryLifecycleCallbacks? _retryFailedTestsLifecycleCallbacks;
    private int _passedTests;
    private int _failedTests;
    private int _skippedTests;

    public RetryDataConsumer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _commandLineOptions = _serviceProvider.GetCommandLineOptions();
    }

    public Type[] DataTypesConsumed => [typeof(TestNodeUpdateMessage)];

    public string Uid => nameof(RetryDataConsumer);

    public string Version => ExtensionVersion.DefaultSemVer;

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

        if (nodeState is FailedTestNodeStateProperty or ErrorTestNodeStateProperty
            or TimeoutTestNodeStateProperty
#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
            or CancelledTestNodeStateProperty)
#pragma warning restore CS0618, MTP0001 // Type or member is obsolete
        {
            ApplicationStateGuard.Ensure(_retryFailedTestsLifecycleCallbacks is not null);
            ApplicationStateGuard.Ensure(_retryFailedTestsLifecycleCallbacks.Client is not null);
            await _retryFailedTestsLifecycleCallbacks.Client.RequestReplyAsync<FailedTestRequest, VoidResponse>(new FailedTestRequest(testNodeUpdateMessage.TestNode.Uid, testNodeUpdateMessage.TestNode.DisplayName), cancellationToken).ConfigureAwait(false);
            _failedTests++;
        }
        else if (nodeState is PassedTestNodeStateProperty)
        {
            _passedTests++;
        }
        else if (nodeState is SkippedTestNodeStateProperty)
        {
            // Skipped tests are counted so the orchestrator's "total" matches the platform run summary's "total",
            // which includes them. They are never retried (a skipped test cannot be in the failed set), so the
            // first attempt's skipped count is the whole suite's.
            _skippedTests++;
        }
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        ApplicationStateGuard.Ensure(_retryFailedTestsLifecycleCallbacks is not null);
        ApplicationStateGuard.Ensure(_retryFailedTestsLifecycleCallbacks.Client is not null);
        await _retryFailedTestsLifecycleCallbacks.Client.RequestReplyAsync<TestRunCountsRequest, VoidResponse>(new TestRunCountsRequest(_passedTests, _failedTests, _skippedTests), testSessionContext.CancellationToken).ConfigureAwait(false);
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
