// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Execution;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Helpers;

public class CancellationTimeoutHelperTests : TestContainer
{
    public async Task RunWithCooperativeCancellationAsyncWhenTimeoutFiresBeforeActionReturnsInvokesFailureFactoryWithTimeout()
    {
        using CancellationTokenSource outerCancellationTokenSource = new();
        bool? isTimeoutResult = null;
        int failureFactoryInvocationCount = 0;

        string result = await CancellationTimeoutHelper.RunWithCooperativeCancellationAsync(
            async _ =>
            {
                await Task.Delay(Timeout.Infinite, outerCancellationTokenSource.Token);
                return "unexpected";
            },
            outerCancellationTokenSource,
            timeout: 1,
            isTimeout =>
            {
                failureFactoryInvocationCount++;
                isTimeoutResult = isTimeout;
                return "failure";
            });

        result.Should().Be("failure");
        failureFactoryInvocationCount.Should().Be(1);
        isTimeoutResult.Should().BeTrue();
    }

    public async Task RunWithCooperativeCancellationAsyncWhenOuterTokenCancelsBeforeTimeoutInvokesFailureFactoryWithCancellation()
    {
        using CancellationTokenSource outerCancellationTokenSource = new();
        bool? isTimeoutResult = null;
        int failureFactoryInvocationCount = 0;

        string result = await CancellationTimeoutHelper.RunWithCooperativeCancellationAsync(
            async _ =>
            {
                outerCancellationTokenSource.Cancel();
                await Task.Delay(Timeout.Infinite, outerCancellationTokenSource.Token);
                return "unexpected";
            },
            outerCancellationTokenSource,
            timeout: 30_000,
            isTimeout =>
            {
                failureFactoryInvocationCount++;
                isTimeoutResult = isTimeout;
                return "failure";
            });

        result.Should().Be("failure");
        failureFactoryInvocationCount.Should().Be(1);
        isTimeoutResult.Should().BeFalse();
    }

    public async Task RunWithCooperativeCancellationAsyncWhenTimeoutIsZeroReturnsFailureWithoutCallingAction()
    {
        using CancellationTokenSource outerCancellationTokenSource = new();
        bool actionCalled = false;
        bool? isTimeoutResult = null;

        string result = await CancellationTimeoutHelper.RunWithCooperativeCancellationAsync(
            _ =>
            {
                actionCalled = true;
                return UnexpectedActionAsync();
            },
            outerCancellationTokenSource,
            timeout: 0,
            isTimeout =>
            {
                isTimeoutResult = isTimeout;
                return "failure";
            });

        result.Should().Be("failure");
        actionCalled.Should().BeFalse();
        isTimeoutResult.Should().BeTrue();
    }

    public async Task RunWithCooperativeCancellationAsyncWhenActionReturnsBeforeTimeoutReturnsActionResult()
    {
        using CancellationTokenSource outerCancellationTokenSource = new();
        bool failureFactoryCalled = false;

        string result = await CancellationTimeoutHelper.RunWithCooperativeCancellationAsync(
            timeoutTokenSource => SuccessfulActionAsync(timeoutTokenSource),
            outerCancellationTokenSource,
            timeout: 30_000,
            isTimeout =>
            {
                failureFactoryCalled = true;
                return isTimeout ? "timeout" : "cancelled";
            });

        result.Should().Be("success");
        outerCancellationTokenSource.IsCancellationRequested.Should().BeFalse();
        failureFactoryCalled.Should().BeFalse();
    }

    public async Task RunWithCooperativeCancellationAsyncWhenActionThrowsUnrelatedOperationCanceledExceptionPropagatesException()
    {
        using CancellationTokenSource outerCancellationTokenSource = new();
        using CancellationTokenSource unrelatedCancellationTokenSource = new();
        unrelatedCancellationTokenSource.Cancel();

        Func<Task> action = async () => await CancellationTimeoutHelper.RunWithCooperativeCancellationAsync(
            _ => ThrowUnrelatedOperationCanceledExceptionAsync(unrelatedCancellationTokenSource.Token),
            outerCancellationTokenSource,
            timeout: 30_000,
            _ => "failure");

        OperationCanceledException exception = (await action.Should().ThrowAsync<OperationCanceledException>()).Which;
        exception.CancellationToken.Should().Be(unrelatedCancellationTokenSource.Token);
    }

    private static async SynchronizationContextPreservingTask<string> SuccessfulActionAsync(CancellationTokenSource timeoutTokenSource)
    {
        await Task.Yield();
        timeoutTokenSource.IsCancellationRequested.Should().BeFalse();
        return "success";
    }

    private static async SynchronizationContextPreservingTask<string> UnexpectedActionAsync()
    {
        await Task.Yield();
        return "unexpected";
    }

    private static async SynchronizationContextPreservingTask<string> ThrowUnrelatedOperationCanceledExceptionAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        throw new OperationCanceledException(cancellationToken);
    }
}
