// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Helpers;

public sealed class FixtureMethodRunnerTests : TestContainer
{
    private const string Canceled = "cancelled {0}.{1}";
    private const string TimedOut = "timed out {0}.{1} after {2}";
    private static readonly MethodInfo FixtureMethod = typeof(FixtureMethodRunnerTests).GetMethod(nameof(FixtureMarker), BindingFlags.NonPublic | BindingFlags.Static)!;

    public async Task AlreadyCanceledTokenPreventsFixtureInvocation()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        bool invoked = false;
        TestFailedException? failure = await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
            () =>
            {
                invoked = true;
                return new(Task.CompletedTask);
            }, cancellation, null, FixtureMethod, null, Canceled, TimedOut);

        invoked.Should().BeFalse();
        failure!.Outcome.Should().Be(UnitTestOutcome.Timeout);
        failure.Message.Should().Be($"cancelled {typeof(FixtureMethodRunnerTests).FullName}.{nameof(FixtureMarker)}");
    }

    public async Task WithoutTimeoutRunsOnCapturedExecutionContextAndRestoresCallerValue()
    {
        AsyncLocal<string> local = new() { Value = "captured" };
        ExecutionContext context = ExecutionContext.Capture()!;
        local.Value = "caller";
        using CancellationTokenSource cancellation = new();
        string? observed = null;
        TestFailedException? failure = await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
            () =>
            {
                observed = local.Value;
                local.Value = "fixture";
                return new(Task.CompletedTask);
            }, cancellation, null, FixtureMethod, context, Canceled, TimedOut);

        failure.Should().BeNull();
        observed.Should().Be("captured");
        local.Value.Should().Be("caller");
    }

    public async Task WithoutTimeoutTranslatesWrappedCancellationFromFixtureToken()
    {
        using CancellationTokenSource cancellation = new();
        TestFailedException? failure = await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
            () =>
            {
                cancellation.Cancel();
                return new(Task.FromException(new TargetInvocationException(new OperationCanceledException(cancellation.Token))));
            }, cancellation, null, FixtureMethod, null, Canceled, TimedOut);

        failure!.Outcome.Should().Be(UnitTestOutcome.Timeout);
        failure.Message.Should().Be($"cancelled {typeof(FixtureMethodRunnerTests).FullName}.{nameof(FixtureMarker)}");
    }

    public async Task WithoutTimeoutUsesWholeTestTimeoutDiagnosticWhenParentTokenIsCanceled()
    {
        using CancellationTokenSource cancellation = new();
        using CancellationTokenSource parent = new();
        TestFailedException? failure = await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
            () =>
            {
                parent.Cancel();
                return new(Task.FromException(new OperationCanceledException(cancellation.Token)));
            }, cancellation, null, FixtureMethod, null, Canceled, TimedOut, (parent, 123));

        failure!.Outcome.Should().Be(UnitTestOutcome.Timeout);
        failure.Message.Should().Be($"timed out {typeof(FixtureMethodRunnerTests).FullName}.{nameof(FixtureMarker)} after 123");
    }

    public async Task WithoutTimeoutPropagatesUnrelatedCancellationInsteadOfReportingFixtureTimeout()
    {
        using CancellationTokenSource cancellation = new();
        using CancellationTokenSource unrelated = new();
        OperationCanceledException expected = new(unrelated.Token);
        Func<Task> action = async () => await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
            () => new(Task.FromException(expected)), cancellation, null, FixtureMethod, null, Canceled, TimedOut);

        (await action.Should().ThrowAsync<OperationCanceledException>()).Which.Should().BeSameAs(expected);
    }

    public async Task CooperativeFixturePreservesSuccessAndTranslatesOnlyMatchingCancellation()
    {
        var timeout = TimeoutInfo.FromTimeoutAttribute(new TimeoutAttribute(int.MaxValue) { CooperativeCancellation = true });
        using CancellationTokenSource cancellation = new();
        int invoked = 0;
        TestFailedException? success = await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
            () =>
            {
                invoked++;
                return new(Task.CompletedTask);
            }, cancellation, timeout, FixtureMethod, null, Canceled, TimedOut);
        success.Should().BeNull();
        invoked.Should().Be(1);

        TestFailedException? canceled = await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
            () =>
            {
                cancellation.Cancel();
                return new(Task.FromException(new OperationCanceledException(cancellation.Token)));
            }, cancellation, timeout, FixtureMethod, null, Canceled, TimedOut);
        canceled!.Outcome.Should().Be(UnitTestOutcome.Timeout);
        canceled.Message.Should().Be($"cancelled {typeof(FixtureMethodRunnerTests).FullName}.{nameof(FixtureMarker)}");
    }

    public async Task CooperativeFixturePropagatesOriginalNonCancellationFailure()
    {
        var timeout = TimeoutInfo.FromTimeoutAttribute(new TimeoutAttribute(int.MaxValue) { CooperativeCancellation = true });
        using CancellationTokenSource cancellation = new();
        InvalidOperationException expected = new("cooperative fixture failed");
        Func<Task> action = async () => await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
            () => new(Task.FromException(expected)), cancellation, timeout, FixtureMethod, null, Canceled, TimedOut);

        (await action.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(expected);
    }

    public async Task NonCooperativeThreadPoolCompletionPreservesSuccessAndOriginalFailure()
    {
        using CancellationTokenSource cancellation = new();
        int invoked = 0;
        TestFailedException? failure = await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
            () =>
            {
                Interlocked.Increment(ref invoked);
                return new(Task.CompletedTask);
            }, cancellation, TimeoutInfo.FromTimeout(Timeout.Infinite), FixtureMethod, null, Canceled, TimedOut);
        failure.Should().BeNull();
        invoked.Should().Be(1);

        InvalidOperationException expected = new("fixture failed");
        Func<Task> action = async () => await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
            () => new(Task.FromException(expected)), cancellation, TimeoutInfo.FromTimeout(Timeout.Infinite), FixtureMethod, null, Canceled, TimedOut);
        (await action.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(expected);
    }

    private static void FixtureMarker()
    {
    }

    public async Task NonCooperativePendingFixtureHasDeterministicZeroDeadline()
    {
        foreach (bool sta in RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? new[] { false, true } : new[] { false })
        {
            using CancellationTokenSource cancellation = new();
            var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var finished = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            int calls = 0;
            TestFailedException? failure;
            try
            {
                failure = await OnApartmentAsync(sta, async () => await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
                    PendingAction, cancellation, TimeoutInfo.FromTimeout(0), FixtureMethod, null, Canceled, TimedOut));
            }
            finally
            {
                release.TrySetResult(null);
                await finished.Task;
            }

            failure!.Outcome.Should().Be(UnitTestOutcome.Timeout);
            failure.Message.Should().Be($"timed out {typeof(FixtureMethodRunnerTests).FullName}.{nameof(FixtureMarker)} after 0");
            calls.Should().Be(1);

            async SynchronizationContextPreservingTask PendingAction()
            {
                Interlocked.Increment(ref calls);
                try
                {
                    await release.Task.ConfigureAwait(false);
                }
                finally
                {
                    finished.TrySetResult(null);
                }
            }
        }
    }

    public async Task NonCooperativeCancellationUsesTheFixtureTokenOnBothSupportedApartmentPaths()
    {
        foreach (bool sta in RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? new[] { false, true } : new[] { false })
        {
            using CancellationTokenSource cancellation = new();
            var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var finished = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            TestFailedException? failure;
            try
            {
                failure = await OnApartmentAsync(sta, async () => await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
                    PendingAction, cancellation, TimeoutInfo.FromTimeout(Timeout.Infinite), FixtureMethod, null, Canceled, TimedOut));
            }
            finally
            {
                release.TrySetResult(null);
                await finished.Task;
            }

            failure!.Outcome.Should().Be(UnitTestOutcome.Timeout);
            failure.Message.Should().Be($"cancelled {typeof(FixtureMethodRunnerTests).FullName}.{nameof(FixtureMarker)}");

            async SynchronizationContextPreservingTask PendingAction()
            {
                cancellation.Cancel();
                try
                {
                    await release.Task.ConfigureAwait(false);
                }
                finally
                {
                    finished.TrySetResult(null);
                }
            }
        }
    }

    public async Task StaFixtureSuccessAndFailurePreserveExecutionContextAndOriginalException()
    {
        foreach (bool sta in RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? new[] { false, true } : new[] { false })
        {
            using CancellationTokenSource cancellation = new();
            AsyncLocal<string> local = new() { Value = "fixture snapshot" };
            ExecutionContext captured = ExecutionContext.Capture()!;
            local.Value = "caller";
            string? observed = null;
            TestFailedException? result = await OnApartmentAsync(sta, async () => await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
                () =>
                {
                    observed = local.Value;
                    return new(Task.CompletedTask);
                }, cancellation, TimeoutInfo.FromTimeout(Timeout.Infinite), FixtureMethod, captured, Canceled, TimedOut));
            result.Should().BeNull();
            observed.Should().Be("fixture snapshot");
            local.Value.Should().Be("caller");
            var expected = new InvalidOperationException("dedicated fixture failure");
            Func<Task> run = async () => await OnApartmentAsync(sta, async () => await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
                () => new(Task.FromException(expected)), cancellation, TimeoutInfo.FromTimeout(Timeout.Infinite),
                FixtureMethod, null, Canceled, TimedOut));
            (await run.Should().ThrowExactlyAsync<InvalidOperationException>()).Which.Should().BeSameAs(expected);
        }
    }

    private static async Task<TestFailedException?> OnApartmentAsync(bool sta, Func<Task<TestFailedException?>> action)
    {
        if (!sta)
        {
            return await action();
        }

        TestFailedException? result = null;
        await Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers.StaThreadHelper.RunOnStaThreadAsync(async () => result = await action());
        return result;
    }
}
