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

public sealed class TestClassInfoInitializerConfigureAwaitMutationTests : TestContainer
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

    public async Task GetResultOrRunClassInitializeAsync_WithPendingBaseClassInitialize_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var fixtureGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        BaseClassInitializeFixture.Gate = fixtureGate;
        TestClassInfo classInfo = CreateClassInfo(typeof(BaseClassInitializeFixture));
        classInfo.BaseClassInitMethods.Add(typeof(BaseClassInitializeFixture).GetMethod(nameof(BaseClassInitializeFixture.Run))!);
        using var testContext = new TestContextImplementation(null, typeof(BaseClassInitializeFixture).FullName, new Dictionary<string, object?>(), null, null);

        // The semaphore is free, so the whole initialization runs synchronously until the pending base class initialize method.
        Task<TestResult> runner = probe.Run(() => classInfo.GetResultOrRunClassInitializeAsync(testContext, string.Empty, string.Empty, string.Empty, string.Empty));

        runner.IsCompleted.Should().BeFalse();

        fixtureGate.SetResult(true);
        await WaitFor(runner);

        (await runner).Outcome.Should().Be(UnitTestOutcome.Passed);
        probe.PostCount.Should().Be(0, "the base class initialization must not capture the caller's synchronization context");
    }

    public async Task GetResultOrRunClassInitializeAsync_WithPendingClassInitialize_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var fixtureGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ClassInitializeFixture.Gate = fixtureGate;
        TestClassInfo classInfo = CreateClassInfo(typeof(ClassInitializeFixture));
        classInfo.ClassInitializeMethod = typeof(ClassInitializeFixture).GetMethod(nameof(ClassInitializeFixture.Run))!;
        using var testContext = new TestContextImplementation(null, typeof(ClassInitializeFixture).FullName, new Dictionary<string, object?>(), null, null);

        // The first call acquires the semaphore and then waits on the pending class initialize method.
        Task<TestResult> runner = probe.Run(() => classInfo.GetResultOrRunClassInitializeAsync(testContext, string.Empty, string.Empty, string.Empty, string.Empty));

        // The second call queues behind the first one, so its semaphore acquisition is pending.
        Task<TestResult> waiter = probe.Run(() => classInfo.GetResultOrRunClassInitializeAsync(testContext, string.Empty, string.Empty, string.Empty, string.Empty));

        runner.IsCompleted.Should().BeFalse();
        waiter.IsCompleted.Should().BeFalse();

        fixtureGate.SetResult(true);
        await WaitFor(runner);
        await WaitFor(waiter);

        (await runner).Outcome.Should().Be(UnitTestOutcome.Passed);
        (await waiter).Outcome.Should().Be(UnitTestOutcome.Passed);
        probe.PostCount.Should().Be(0, "the class initialization must not capture the caller's synchronization context");
    }

    private static TestClassInfo CreateClassInfo(Type classType)
        => new(classType, classType.GetConstructor(Type.EmptyTypes), true, new TestClassAttribute(), new TestAssemblyInfo(classType.Assembly));

    private static async Task WaitFor(Task task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(WaitTimeout));
        completed.Should().BeSameAs(task, "the awaited operation timed out");
        await task;
    }

    private static class BaseClassInitializeFixture
    {
        public static TaskCompletionSource<bool>? Gate { get; set; }

        public static Task Run()
            => Gate!.Task;
    }

    private static class ClassInitializeFixture
    {
        public static TaskCompletionSource<bool>? Gate { get; set; }

        public static Task Run()
            => Gate!.Task;
    }
}
