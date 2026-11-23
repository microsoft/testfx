// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public sealed class ThreadOperationsMutationTests : TestContainer
{
    private const string CustomThreadName = "MSTest Test Execution Thread";
    private const int CompletionTimeoutMilliseconds = 30_000;

    // Only an implementation that fails to observe the cancellation waits for this long.
    private const int BlockedActionMilliseconds = 3_000;
    private const int SchedulingProbeMilliseconds = 1_000;
    private const int CancellationAttempts = 5;

    private readonly ThreadOperations _threadOperations = new();

#if !NETFRAMEWORK
    public void ExecuteShouldRunTheActionOnAThreadPoolThreadWhenTheCallerIsNotInASingleThreadedApartment()
    {
        ThreadSnapshot? snapshot = null;

        bool completed = RunOnMultiThreadedApartment(
            () => _threadOperations.Execute(() => snapshot = ThreadSnapshot.Capture(), CompletionTimeoutMilliseconds, CancellationToken.None));

        completed.Should().BeTrue();
        snapshot.Should().NotBeNull();
        snapshot!.IsThreadPoolThread.Should().BeTrue();
        snapshot.Name.Should().NotBe(CustomThreadName);
    }

    public void ExecuteShouldReturnFalseWhenTheTokenIsAlreadyCanceledForAThreadPoolExecution()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        bool completed = RunOnMultiThreadedApartment(
            () => _threadOperations.Execute(() => { }, CompletionTimeoutMilliseconds, cancellation.Token));

        completed.Should().BeFalse();
    }
#endif

    public void ExecuteShouldRunTheActionOnADedicatedStaThreadWhenTheCallerIsInASingleThreadedApartment()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        ThreadSnapshot? snapshot = null;

        bool completed = RunOnSingleThreadedApartment(
            () => _threadOperations.Execute(() => snapshot = ThreadSnapshot.Capture(), CompletionTimeoutMilliseconds, CancellationToken.None));

        completed.Should().BeTrue();
        snapshot.Should().NotBeNull();
        snapshot!.Name.Should().Be(CustomThreadName);
        snapshot.IsBackground.Should().BeTrue();
        snapshot.IsThreadPoolThread.Should().BeFalse();
        snapshot.ApartmentState.Should().Be(ApartmentState.STA);
    }

    public void ExecuteShouldNotStartTheActionWhenTheTokenIsAlreadyCanceledOnTheCustomThread()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        int executions = 0;

        bool completed = RunOnSingleThreadedApartment(
            () => _threadOperations.Execute(() => Interlocked.Increment(ref executions), CompletionTimeoutMilliseconds, cancellation.Token));

        completed.Should().BeFalse();
        Volatile.Read(ref executions).Should().Be(0);
    }

    public void ExecuteShouldReturnFalseWhenTheTokenIsCanceledWhileTheActionIsStillRunningOnTheCustomThread()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // A cancellation that lands before the thread pool starts the join task cancels that task instead, which hides a missing wait. Retrying makes the favorable ordering practically certain.
        for (int attempt = 0; attempt < CancellationAttempts; attempt++)
        {
            using CancellationTokenSource cancellation = new();
            using ManualResetEventSlim actionStarted = new();
            using ManualResetEventSlim releaseAction = new();
            Thread? actionThread = null;
            bool completed;
            try
            {
                completed = RunOnSingleThreadedApartment(
                    () => _threadOperations.Execute(
                        () =>
                        {
                            actionThread = Thread.CurrentThread;
                            actionStarted.Set();
                            releaseAction.Wait(BlockedActionMilliseconds);
                        },
                        CompletionTimeoutMilliseconds,
                        cancellation.Token),
                    caller =>
                    {
                        actionStarted.Wait(CompletionTimeoutMilliseconds);
                        Task.Run(() => { }).Wait(SchedulingProbeMilliseconds);
                        SpinWait.SpinUntil(() => (caller.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0, SchedulingProbeMilliseconds);
                        cancellation.Cancel();
                    });
            }
            finally
            {
                releaseAction.Set();
                actionThread?.Join(CompletionTimeoutMilliseconds).Should().BeTrue("the action must finish before its synchronization gates are disposed");
            }

            completed.Should().BeFalse("the cancellation must be observed while the action is still blocked");
        }
    }

#if !NETFRAMEWORK
    private static bool RunOnMultiThreadedApartment(Func<bool> execute)
    {
        Task<bool> caller = Task.Run(execute);
        caller.Wait(CompletionTimeoutMilliseconds).Should().BeTrue("Execute must return");
        return caller.GetAwaiter().GetResult();
    }
#endif

    [SupportedOSPlatform("windows")]
    private static bool RunOnSingleThreadedApartment(Func<bool> execute, Action<Thread>? whileRunning = null)
    {
        bool result = false;
        Exception? failure = null;
        Thread caller = new(() =>
        {
            try
            {
                result = execute();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        })
        {
            IsBackground = true,
        };

        caller.SetApartmentState(ApartmentState.STA);
        caller.Start();
        try
        {
            whileRunning?.Invoke(caller);
        }
        finally
        {
            caller.Join(CompletionTimeoutMilliseconds).Should().BeTrue("Execute must return");
        }

        failure.Should().BeNull();
        return result;
    }

    private sealed class ThreadSnapshot
    {
        private ThreadSnapshot(Thread thread)
        {
            Name = thread.Name;
            IsBackground = thread.IsBackground;
            IsThreadPoolThread = thread.IsThreadPoolThread;
            ApartmentState = thread.GetApartmentState();
        }

        public string? Name { get; }

        public bool IsBackground { get; }

        public bool IsThreadPoolThread { get; }

        public ApartmentState ApartmentState { get; }

        public static ThreadSnapshot Capture() => new(Thread.CurrentThread);
    }
}
