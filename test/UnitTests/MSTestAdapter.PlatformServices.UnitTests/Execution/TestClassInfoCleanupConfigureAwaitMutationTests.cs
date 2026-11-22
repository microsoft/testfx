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

public sealed class TestClassInfoCleanupConfigureAwaitMutationTests : TestContainer
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);

    public async Task RunClassCleanupAsync_WithPendingClassCleanup_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var fixtureGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ClassCleanupFixture.Gate = fixtureGate;
        TestClassInfo classInfo = CreateClassInfo(typeof(ClassCleanupFixture));
        classInfo.ClassCleanupMethod = typeof(ClassCleanupFixture).GetMethod(nameof(ClassCleanupFixture.Run))!;
        classInfo.IsClassInitializeExecuted = true;
        using var testContext = new TestContextImplementation(null, typeof(ClassCleanupFixture).FullName, new Dictionary<string, object?>(), null, null);

        // The semaphore is free, so the whole cleanup runs synchronously until the pending class cleanup method.
        Task<TestResult?> runner = probe.Run(() => classInfo.RunClassCleanupAsync(testContext, []));

        runner.IsCompleted.Should().BeFalse();

        fixtureGate.SetResult(true);
        await WaitFor(runner);

        (await runner).Should().BeNull();
        classInfo.IsClassCleanupExecuted.Should().BeTrue();
        probe.PostCount.Should().Be(0, "the class cleanup must not capture the caller's synchronization context");
    }

    public async Task ExecuteClassCleanupAsync_WithPendingBaseClassCleanup_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var fixtureGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        BaseClassCleanupFixture.Gate = fixtureGate;
        TestClassInfo classInfo = CreateClassInfo(typeof(BaseClassCleanupFixture));
        classInfo.BaseClassCleanupMethods.Add(typeof(BaseClassCleanupFixture).GetMethod(nameof(BaseClassCleanupFixture.Run))!);
        classInfo.IsClassInitializeExecuted = true;
        using var testContext = new TestContextImplementation(null, typeof(BaseClassCleanupFixture).FullName, new Dictionary<string, object?>(), null, null);

        // There is no class cleanup method, so the pending base class cleanup method is the first awaited fixture.
        Task<TestFailedException?> runner = probe.Run(() => classInfo.ExecuteClassCleanupAsync(testContext));

        runner.IsCompleted.Should().BeFalse();

        fixtureGate.SetResult(true);
        await WaitFor(runner);

        (await runner).Should().BeNull();
        classInfo.IsClassCleanupExecuted.Should().BeTrue();
        probe.PostCount.Should().Be(0, "the base class cleanup must not capture the caller's synchronization context");
    }

    public async Task ExecuteClassCleanupAsync_WhenAnotherCleanupIsRunning_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var fixtureGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ConcurrentClassCleanupFixture.Gate = fixtureGate;
        TestClassInfo classInfo = CreateClassInfo(typeof(ConcurrentClassCleanupFixture));
        classInfo.ClassCleanupMethod = typeof(ConcurrentClassCleanupFixture).GetMethod(nameof(ConcurrentClassCleanupFixture.Run))!;
        classInfo.IsClassInitializeExecuted = true;
        using var testContext = new TestContextImplementation(null, typeof(ConcurrentClassCleanupFixture).FullName, new Dictionary<string, object?>(), null, null);

        // The first call acquires the semaphore and then waits on the pending class cleanup method.
        Task<TestFailedException?> runner = probe.Run(() => classInfo.ExecuteClassCleanupAsync(testContext));

        // The second call queues behind the first one, so its semaphore acquisition is pending.
        Task<TestFailedException?> waiter = probe.Run(() => classInfo.ExecuteClassCleanupAsync(testContext));

        runner.IsCompleted.Should().BeFalse();
        waiter.IsCompleted.Should().BeFalse();

        fixtureGate.SetResult(true);
        await WaitFor(runner);
        await WaitFor(waiter);

        (await runner).Should().BeNull();
        (await waiter).Should().BeNull();
        probe.PostCount.Should().Be(0, "the class cleanup must not capture the caller's synchronization context");
    }

    private static TestClassInfo CreateClassInfo(Type classType)
        => new(classType, classType.GetConstructor(Type.EmptyTypes), true, new TestClassAttribute(), new TestAssemblyInfo(classType.Assembly));

    private static async Task WaitFor(Task task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(WaitTimeout));
        completed.Should().BeSameAs(task, "the awaited operation timed out");
        await task;
    }

    private static class ClassCleanupFixture
    {
        public static TaskCompletionSource<bool>? Gate { get; set; }

        public static Task Run()
            => Gate!.Task;
    }

    private static class BaseClassCleanupFixture
    {
        public static TaskCompletionSource<bool>? Gate { get; set; }

        public static Task Run()
            => Gate!.Task;
    }

    private static class ConcurrentClassCleanupFixture
    {
        public static TaskCompletionSource<bool>? Gate { get; set; }

        public static Task Run()
            => Gate!.Task;
    }
}
