// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MSTestAdapter.PlatformServices.UnitTests.Helpers;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Execution;

public sealed class TestAssemblyInfoConfigureAwaitMutationTests : TestContainer
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);

    public async Task RunAssemblyInitializeAsync_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var fixtureGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        InitializeFixture.Gate = fixtureGate;
        var assemblyInfo = new TestAssemblyInfo(typeof(InitializeFixture).Assembly)
        {
            AssemblyInitializeMethod = typeof(InitializeFixture).GetMethod(nameof(InitializeFixture.Run))!,
        };
        using var testContext = new TestContextImplementation(null, null, new Dictionary<string, object?>(), null, null);

        // The first call acquires the semaphore and then waits on the pending fixture, so the fixture await is pending.
        Task<TestResult> runner = probe.Run(() => assemblyInfo.RunAssemblyInitializeAsync(testContext));

        // The second call queues behind the first one, so its semaphore acquisition is pending.
        Task<TestResult> waiter = probe.Run(() => assemblyInfo.RunAssemblyInitializeAsync(testContext));

        runner.IsCompleted.Should().BeFalse();
        waiter.IsCompleted.Should().BeFalse();

        fixtureGate.SetResult(true);
        await WaitFor(runner);
        await WaitFor(waiter);

        (await runner).Outcome.Should().Be(UnitTestOutcome.Passed);
        (await waiter).Outcome.Should().Be(UnitTestOutcome.Passed);
        probe.PostCount.Should().Be(0, "the assembly initialization must not capture the caller's synchronization context");
    }

    public async Task ExecuteAssemblyCleanupAsync_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var fixtureGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        CleanupFixture.Gate = fixtureGate;
        var assemblyInfo = new TestAssemblyInfo(typeof(CleanupFixture).Assembly)
        {
            AssemblyCleanupMethod = typeof(CleanupFixture).GetMethod(nameof(CleanupFixture.Run))!,
        };
        using var testContext = new TestContextImplementation(null, null, new Dictionary<string, object?>(), null, null);

        // The first call acquires the semaphore and then waits on the pending fixture, so the fixture await is pending.
        Task<TestFailedException?> runner = probe.Run(() => assemblyInfo.ExecuteAssemblyCleanupAsync(testContext));

        // The second call queues behind the first one, so its semaphore acquisition is pending.
        Task<TestFailedException?> waiter = probe.Run(() => assemblyInfo.ExecuteAssemblyCleanupAsync(testContext));

        runner.IsCompleted.Should().BeFalse();
        waiter.IsCompleted.Should().BeFalse();

        fixtureGate.SetResult(true);
        await WaitFor(runner);
        await WaitFor(waiter);

        (await runner).Should().BeNull();
        (await waiter).Should().BeNull();
        probe.PostCount.Should().Be(0, "the assembly cleanup must not capture the caller's synchronization context");
    }

    private static async Task WaitFor(Task task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(WaitTimeout));
        completed.Should().BeSameAs(task, "the awaited operation timed out");
        await task;
    }

    private static class InitializeFixture
    {
        public static TaskCompletionSource<bool>? Gate { get; set; }

        public static Task Run()
            => Gate!.Task;
    }

    private static class CleanupFixture
    {
        public static TaskCompletionSource<bool>? Gate { get; set; }

        public static Task Run()
            => Gate!.Task;
    }
}
