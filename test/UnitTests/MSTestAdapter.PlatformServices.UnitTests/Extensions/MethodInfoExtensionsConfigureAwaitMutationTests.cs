// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Execution;

using MSTestAdapter.PlatformServices.UnitTests.Helpers;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Extensions;

public sealed class MethodInfoExtensionsConfigureAwaitMutationTests : TestContainer
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);

    public async Task InvokeAsFixtureMethodAsync_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var fixtureGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        PendingFixture.Gate = fixtureGate;
        MethodInfo fixtureMethod = typeof(PendingFixture).GetMethod(nameof(PendingFixture.Run))!;
        using var testContext = new TestContextImplementation(null, null, new Dictionary<string, object?>(), null, null);
        ExecutionContext? capturedExecutionContext = null;
        bool afterExecutionContextCaptured = false;

        // The fixture method returns a task that is still pending, so the fixture is awaited on the calling thread.
        SynchronizationContextPreservingTask call = probe.Run(() => fixtureMethod.InvokeAsFixtureMethodAsync(
            testContext,
            executionContext => capturedExecutionContext = executionContext,
            () => afterExecutionContextCaptured = true));
        Task task = AsTask(call);

        task.IsCompleted.Should().BeFalse();

        fixtureGate.SetResult(true);
        await WaitFor(task);

        afterExecutionContextCaptured.Should().BeTrue();
        capturedExecutionContext.Should().NotBeNull();
        probe.PostCount.Should().Be(0, "the fixture method must not be awaited on the caller's synchronization context");
    }

    private static async Task AsTask(SynchronizationContextPreservingTask task)
        => await task.ConfigureAwait(false);

    private static async Task WaitFor(Task task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(WaitTimeout));
        completed.Should().BeSameAs(task, "the awaited operation timed out");
        await task;
    }

    private static class PendingFixture
    {
        public static TaskCompletionSource<bool>? Gate { get; set; }

        public static Task Run()
            => Gate!.Task;
    }
}
