// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Extensions.Policy.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.Policy;

[UnsupportedOSPlatform("browser")]
internal sealed class RetryDataConsumer : IDataConsumer, ITestSessionLifetimeHandler, IAsyncInitializableExtension
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICommandLineOptions _commandLineOptions;

    // Uids of the tests this attempt was asked to retry which produced a passing result and no failing one, i.e.
    // the ones that genuinely recovered. Reported explicitly so the orchestrator does not have to infer recovery
    // from "absent from the failed set", which would also match a test that never ran at all.
    private readonly HashSet<string> _recoveredTests = [];

    // Uids from the retry set that produced at least one non-passing result in this attempt. A folded data-driven
    // test reports several results under one uid, so a uid must not count as recovered when any of its rows failed.
    private readonly HashSet<string> _notRecoveredTests = [];

    private RetryLifecycleCallbacks? _retryFailedTestsLifecycleCallbacks;
    private HashSet<string>? _testsBeingRetried;
    private int _passedTests;
    private int _failedTests;
    private int _skippedTests;

    public RetryDataConsumer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _commandLineOptions = _serviceProvider.GetCommandLineOptions();
    }

    public Type[] DataTypesConsumed => [typeof(TestNodeUpdateMessage), typeof(SessionFileArtifact)];

    public string Uid => nameof(RetryDataConsumer);

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.RetryFailedTestsExtensionDisplayName;

    public string Description => ExtensionResources.RetryFailedTestsExtensionDescription;

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        if (value is SessionFileArtifact artifact)
        {
            NamedPipeClient client = GetClient();
            await client.RequestReplyAsync<ArtifactRequest, VoidResponse>(
                new ArtifactRequest(artifact.FileInfo.FullName, artifact.Kind),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        var testNodeUpdateMessage = (TestNodeUpdateMessage)value;
        TestNodeStateProperty? nodeState = testNodeUpdateMessage.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>();
        if (nodeState is null)
        {
            return;
        }

        // A test framework that retries in-process (MSTest's [Retry]) reports every attempt under the same test
        // node uid. A superseded attempt is not the test's outcome: counting it here would both ask the
        // orchestrator to relaunch the host for a test whose final in-process attempt passed, and inflate the run
        // totals. Only the final attempt participates in the out-of-process retry decision, so the two retry
        // mechanisms compose rather than multiply.
        if (testNodeUpdateMessage.TestNode.IsSupersededRetryAttempt())
        {
            return;
        }

        string uid = testNodeUpdateMessage.TestNode.Uid;
        if (nodeState is FailedTestNodeStateProperty or ErrorTestNodeStateProperty
            or TimeoutTestNodeStateProperty
#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
            or CancelledTestNodeStateProperty)
#pragma warning restore CS0618, MTP0001 // Type or member is obsolete
        {
            NamedPipeClient client = GetClient();
            await client.RequestReplyAsync<FailedTestRequest, VoidResponse>(new FailedTestRequest(uid, testNodeUpdateMessage.TestNode.DisplayName), cancellationToken).ConfigureAwait(false);
            _failedTests++;
            MarkNotRecovered(uid);
        }
        else if (nodeState is PassedTestNodeStateProperty)
        {
            _passedTests++;
            MarkRecoveredIfRetried(uid);
        }
        else if (nodeState is SkippedTestNodeStateProperty)
        {
            // Skipped tests are counted so the orchestrator's "total" matches the platform run summary's "total",
            // which includes them. A test that was retried and came back skipped did not recover: it produced no
            // passing result, so it must not be reported as flaky.
            _skippedTests++;
            MarkNotRecovered(uid);
        }
    }

    /// <summary>
    /// Records that <paramref name="uid"/> passed, provided it is one of the tests this attempt was asked to retry.
    /// A uid that also produced a non-passing result in the same attempt (possible for a folded data-driven test,
    /// whose rows share one uid) is never treated as recovered, regardless of the order the results arrive in.
    /// </summary>
    private void MarkRecoveredIfRetried(string uid)
    {
        if (_testsBeingRetried is not null && _testsBeingRetried.Contains(uid) && !_notRecoveredTests.Contains(uid))
        {
            _recoveredTests.Add(uid);
        }
    }

    private void MarkNotRecovered(string uid)
    {
        if (_testsBeingRetried is not null && _testsBeingRetried.Contains(uid))
        {
            _notRecoveredTests.Add(uid);
            _recoveredTests.Remove(uid);
        }
    }

    private NamedPipeClient GetClient()
        => _retryFailedTestsLifecycleCallbacks?.Client ?? throw ApplicationStateGuard.Unreachable();

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        ApplicationStateGuard.Ensure(_retryFailedTestsLifecycleCallbacks is not null);
        ApplicationStateGuard.Ensure(_retryFailedTestsLifecycleCallbacks.Client is not null);
        await _retryFailedTestsLifecycleCallbacks.Client.RequestReplyAsync<TestRunCountsRequest, VoidResponse>(
            new TestRunCountsRequest(_passedTests, _failedTests, _skippedTests, [.. _recoveredTests]),
            testSessionContext.CancellationToken).ConfigureAwait(false);
    }

    public Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        // Read the retry set here rather than in InitializeAsync: it is fetched from the orchestrator during
        // BeforeRunAsync, which the host runs after extension initialization but before the test session starts.
        // Reading it any earlier would always observe null and silently disable recovery tracking.
        string[]? testsToRetry = _retryFailedTestsLifecycleCallbacks?.FailedTestsIDToRetry;
        if (testsToRetry is { Length: > 0 })
        {
            _testsBeingRetried = new HashSet<string>(testsToRetry, StringComparer.Ordinal);
        }

        return Task.CompletedTask;
    }

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
