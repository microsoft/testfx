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

[SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "The STA execution paths are exercised only on Windows.")]
public sealed class FixtureMethodRunnerMutationTests : TestContainer
{
    private const string Canceled = "cancelled {0}.{1}";
    private const string TimedOut = "timed out {0}.{1} after {2}";
    private const string FixtureThreadName = "MSTest Fixture Execution Thread";
    private static readonly MethodInfo FixtureMethod = typeof(FixtureMethodRunnerMutationTests).GetMethod(nameof(FixtureMarker), BindingFlags.NonPublic | BindingFlags.Static)!;

    public async Task CooperativeZeroTimeout_IsReportedAsATimeoutAndDoesNotInvokeTheFixture()
    {
        using CancellationTokenSource cancellation = new();
        int calls = 0;

        TestFailedException? failure = await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
            () =>
            {
                calls++;
                return new(Task.CompletedTask);
            },
            cancellation,
            TimeoutInfo.FromTimeoutAttribute(new TimeoutAttribute(0) { CooperativeCancellation = true }),
            FixtureMethod,
            null,
            Canceled,
            TimedOut);

        failure!.Outcome.Should().Be(UnitTestOutcome.Timeout);
        failure.Message.Should().Be($"timed out {typeof(FixtureMethodRunnerMutationTests).FullName}.{nameof(FixtureMarker)} after 0");
        calls.Should().Be(0);
    }

    public async Task NonCooperativeFixtureStartedFromAnMtaThread_RunsOnAThreadPoolThread()
    {
        using CancellationTokenSource cancellation = new();
        ThreadSnapshot? snapshot = null;

        // Thread pool threads are always MTA, so this does not depend on the apartment state of the test host thread.
        TestFailedException? failure = await Task.Run(async () => await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
            () =>
            {
                snapshot = ThreadSnapshot.Capture();
                return new(Task.CompletedTask);
            },
            cancellation,
            TimeoutInfo.FromTimeout(Timeout.Infinite),
            FixtureMethod,
            null,
            Canceled,
            TimedOut));

        failure.Should().BeNull();
        snapshot.Should().NotBeNull();
        snapshot!.IsThreadPoolThread.Should().BeTrue();
        snapshot.Name.Should().NotBe(FixtureThreadName);
    }

    public async Task NonCooperativeFixtureStartedFromAnStaThread_RunsOnADedicatedBackgroundStaThread()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        using CancellationTokenSource cancellation = new();
        ThreadSnapshot? snapshot = null;
        int callerThreadId = 0;

        TestFailedException? failure = await OnStaThreadAsync(async () =>
        {
            callerThreadId = Environment.CurrentManagedThreadId;
            return await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
                () =>
                {
                    snapshot = ThreadSnapshot.Capture();
                    return new(Task.CompletedTask);
                },
                cancellation,
                TimeoutInfo.FromTimeout(Timeout.Infinite),
                FixtureMethod,
                null,
                Canceled,
                TimedOut);
        });

        failure.Should().BeNull();
        snapshot.Should().NotBeNull();
        snapshot!.ManagedThreadId.Should().NotBe(callerThreadId);
        snapshot.IsThreadPoolThread.Should().BeFalse();
        snapshot.Name.Should().Be(FixtureThreadName);
        snapshot.IsBackground.Should().BeTrue();
        snapshot.Apartment.Should().Be(ApartmentState.STA);
    }

    public async Task NonCooperativeFixtureStartedFromAnStaThread_PropagatesWaitFailuresUnrelatedToTheFixture()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        using CancellationTokenSource cancellation = new();

        // An invalid timeout makes the wait itself fail, while the fixture task is neither faulted nor canceled.
        Func<Task> run = async () => await OnStaThreadAsync(async () => await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
            () => new(Task.CompletedTask),
            cancellation,
            TimeoutInfo.FromTimeout(-2),
            FixtureMethod,
            null,
            Canceled,
            TimedOut));

        await run.Should().ThrowExactlyAsync<ArgumentOutOfRangeException>();
    }

    private static void FixtureMarker()
    {
    }

    private static async Task<TestFailedException?> OnStaThreadAsync(Func<Task<TestFailedException?>> action)
    {
        TestFailedException? result = null;
        await Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers.StaThreadHelper.RunOnStaThreadAsync(async () => result = await action());
        return result;
    }

    private sealed class ThreadSnapshot
    {
        private ThreadSnapshot(bool isThreadPoolThread, bool isBackground, string? name, ApartmentState apartment, int managedThreadId)
        {
            IsThreadPoolThread = isThreadPoolThread;
            IsBackground = isBackground;
            Name = name;
            Apartment = apartment;
            ManagedThreadId = managedThreadId;
        }

        public bool IsThreadPoolThread { get; }

        public bool IsBackground { get; }

        public string? Name { get; }

        public ApartmentState Apartment { get; }

        public int ManagedThreadId { get; }

        public static ThreadSnapshot Capture()
        {
            Thread thread = Thread.CurrentThread;
            return new(thread.IsThreadPoolThread, thread.IsBackground, thread.Name, thread.GetApartmentState(), thread.ManagedThreadId);
        }
    }
}
