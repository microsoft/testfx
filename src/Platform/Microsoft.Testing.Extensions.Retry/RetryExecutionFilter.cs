// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.Policy;

internal sealed class RetryExecutionFilter : ITestExecutionFilter
{
    private readonly IServiceProvider _serviceProvider;
    private readonly bool _isEnabled;

    public RetryExecutionFilter(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _isEnabled = serviceProvider.GetCommandLineOptions().IsOptionSet(RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName);
    }

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(_isEnabled);

    /// <inheritdoc />
    public Task<bool> MatchesFilterAsync(TestNode testNode) => TestNodeUidListFilter.MatchesFilterAsync(testNode);

    [field: AllowNull]
    [field: MaybeNull]
    private TestNodeUid[] RetryableTests => field ??= GetRetryableTests(_serviceProvider);

    [field: AllowNull]
    [field: MaybeNull]
    private TestNodeUidListFilter TestNodeUidListFilter => field ??= new TestNodeUidListFilter(RetryableTests);

    private static TestNodeUid[] GetRetryableTests(IServiceProvider serviceProvider)
    {
        RetryLifecycleCallbacks retryLifecycleCallbacks = serviceProvider.GetRequiredService<RetryLifecycleCallbacks>();

        return retryLifecycleCallbacks.FailedTestsIDToRetry?
            .Select(x => new TestNodeUid(x)).ToArray() ?? [];
    }
}
