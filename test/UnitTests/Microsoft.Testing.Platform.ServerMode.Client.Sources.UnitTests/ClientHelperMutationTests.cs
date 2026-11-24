// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias serverclient;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

using ClientTasks = serverclient::Microsoft.Testing.Platform.Helpers.TaskExtensions;

namespace Microsoft.Testing.Platform.ServerMode.Client.Sources.UnitTests;

[TestClass]
public sealed class ClientHelperMutationTests
{
    internal const string LateFaultFixtureEnvironmentVariable = "TESTFX_LATE_FAULT_OBSERVATION";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("generic-cancellation")]
    [DataRow("cancellation")]
    [DataRow("timeout")]
    [DataRow("timeout-with-token")]
    public async Task TaskWaits_LateFaultObservation_ControlsUnobservedTaskException(string operation)
    {
        using Process process = MtpServerProcessTests.CreateFixtureProcess();
        process.StartInfo.EnvironmentVariables[LateFaultFixtureEnvironmentVariable] = operation;
        process.Start();
#if NETCOREAPP
        Task<string> output = process.StandardOutput.ReadToEndAsync(TestContext.CancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(TestContext.CancellationToken);
#else
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
#endif
        try
        {
            // Each of the two collections has a 30-second watchdog; allow profiled child startup as well.
            const int FixtureExitLimitMilliseconds = 120_000;
            Assert.IsTrue(
                await Task.Run(() => process.WaitForExit(FixtureExitLimitMilliseconds), TestContext.CancellationToken),
                "The isolated late-fault fixture must terminate within its collection watchdogs.");
            string actualOutput = await output;
            string actualError = await error;
            Assert.AreEqual(0, process.ExitCode, $"{actualOutput}{actualError}");
            Assert.AreEqual(
                $"Operation={operation} Cases=2 Collected=2 FalseEvents=1 TrueEvents=0 Unrelated=0{Environment.NewLine}",
                actualOutput);
            Assert.AreEqual(string.Empty, actualError);
        }
        finally
        {
            MtpServerProcessTests.KillProcessIfRunning(process);
            await Task.WhenAll(output, error);
        }
    }

    [TestMethod]
    [DataRow("watchdog-helper", "Blocked helper wait.")]
    [DataRow("watchdog-finalizer", "Blocked finalizer.")]
    public async Task LateFaultFixture_WatchdogTerminatesBlockedChild(string operation, string expectedOutput)
    {
        using Process process = MtpServerProcessTests.CreateFixtureProcess();
        process.StartInfo.EnvironmentVariables[LateFaultFixtureEnvironmentVariable] = operation;
        process.Start();
#if NETCOREAPP
        Task<string> output = process.StandardOutput.ReadToEndAsync(TestContext.CancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(TestContext.CancellationToken);
#else
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
#endif
        try
        {
            // The probe uses a five-second child deadline; leave room for profiled process startup.
            const int FixtureExitLimitMilliseconds = 30_000;
            Assert.IsTrue(
                await Task.Run(() => process.WaitForExit(FixtureExitLimitMilliseconds), TestContext.CancellationToken),
                "The child-owned watchdog must terminate the blocked fixture without parent cleanup.");
            Assert.AreNotEqual(0, process.ExitCode);
            Assert.AreEqual($"{expectedOutput}{Environment.NewLine}", await output);
            Assert.AreEqual($"The late-fault fixture exceeded its independent lifetime.{Environment.NewLine}", await error);
        }
        finally
        {
            MtpServerProcessTests.KillProcessIfRunning(process);
            await Task.WhenAll(output, error);
        }
    }

    [TestMethod]
    public void StateGuard_OnlyFailedConditionsThrowAndPreserveDiagnostics()
    {
        InvokeGuard("Ensure", true, "explicit failure");
        InvokeGuard("Ensure", true, "source.cs", 42);

        InvalidOperationException explicitFailure = Assert.ThrowsExactly<InvalidOperationException>(() => InvokeGuard("Ensure", false, "explicit failure"));
        InvalidOperationException locatedFailure = Assert.ThrowsExactly<InvalidOperationException>(() => InvokeGuard("Ensure", false, "source.cs", 42));

        Assert.AreEqual("explicit failure", explicitFailure.Message);
        Assert.AreEqual("Unexpected state in file 'source.cs' at line '42'", locatedFailure.Message);
        Assert.AreEqual(
            "This program location is thought to be unreachable. File='source.cs' Line=42",
            Assert.IsInstanceOfType<Exception>(InvokeGuard("Unreachable", "source.cs", 42)).Message);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task WithCancellation_CompletedTaskWinsOverCancellationAndPreservesResultOrFault(bool observe)
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var failure = new InvalidOperationException("original failure");

        Assert.AreEqual(42, await ClientTasks.WithCancellationAsync(Task.FromResult(42), canceled.Token, observe));
        await ClientTasks.WithCancellationAsync(Task.CompletedTask, canceled.Token, observe);
        InvalidOperationException generic = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            ClientTasks.WithCancellationAsync(Task.FromException<int>(failure), canceled.Token, observe));
        InvalidOperationException nonGeneric = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            ClientTasks.WithCancellationAsync(Task.FromException(failure), canceled.Token, observe));

        Assert.AreSame(failure, generic);
        Assert.AreSame(failure, nonGeneric);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task WithCancellation_PendingTaskCompletes_PreservesResultAndFault(bool observe)
    {
        var result = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var failureSource = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new InvalidOperationException("original failure");
        Task<int> generic = ClientTasks.WithCancellationAsync(result.Task, TestContext.CancellationToken, observe);
        Task nonGeneric = ClientTasks.WithCancellationAsync((Task)result.Task, TestContext.CancellationToken, observe);
        Task<int> genericFault = ClientTasks.WithCancellationAsync(failureSource.Task, TestContext.CancellationToken, observe);
        Task nonGenericFault = ClientTasks.WithCancellationAsync((Task)failureSource.Task, TestContext.CancellationToken, observe);

        result.SetResult(37);
        failureSource.SetException(failure);

        Assert.AreEqual(37, await generic);
        await nonGeneric;
        Assert.AreSame(failure, await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => genericFault));
        Assert.AreSame(failure, await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => nonGenericFault));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task WithCancellation_CancelingWait_DoesNotCancelOriginalTask(bool observe)
    {
        var source = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        Task<int> generic = ClientTasks.WithCancellationAsync(source.Task, cancellation.Token, observe);
        Task nonGeneric = ClientTasks.WithCancellationAsync((Task)source.Task, cancellation.Token, observe);
        try
        {
            cancellation.Cancel();
            Assert.IsFalse(source.Task.IsCompleted);
            source.SetResult(42);
            OperationCanceledException genericFailure = await Assert.ThrowsAsync<OperationCanceledException>(() => generic);
            OperationCanceledException nonGenericFailure = await Assert.ThrowsAsync<OperationCanceledException>(() => nonGeneric);
            Assert.AreEqual(cancellation.Token, genericFailure.CancellationToken);
            Assert.AreEqual(cancellation.Token, nonGenericFailure.CancellationToken);
        }
        finally
        {
            source.TrySetResult(42);
            await source.Task;
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task TimeoutAfter_CompletedTask_PreservesFaultInsteadOfRewritingIt(bool observe)
    {
        var failure = new TimeoutException("original timeout");
        await ClientTasks.TimeoutAfterAsync(Task.CompletedTask, TimeSpan.Zero, observe);
        await ClientTasks.TimeoutAfterAsync(Task.CompletedTask, TimeSpan.Zero, CancellationToken.None, observe);

        TimeoutException withoutToken = await Assert.ThrowsExactlyAsync<TimeoutException>(() =>
            ClientTasks.TimeoutAfterAsync(Task.FromException(failure), TimeSpan.Zero, observe));
        TimeoutException withToken = await Assert.ThrowsExactlyAsync<TimeoutException>(() =>
            ClientTasks.TimeoutAfterAsync(Task.FromException(failure), TimeSpan.Zero, CancellationToken.None, observe));

        Assert.AreSame(failure, withoutToken);
        Assert.AreSame(failure, withToken);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task TimeoutAfter_ZeroTimeout_DoesNotCompleteTheOriginalTask(bool withToken)
    {
        var source = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            Task wait = withToken
                ? ClientTasks.TimeoutAfterAsync(source.Task, TimeSpan.Zero, CancellationToken.None)
                : ClientTasks.TimeoutAfterAsync(source.Task, TimeSpan.Zero);
            await Assert.ThrowsExactlyAsync<TimeoutException>(() => wait);
            Assert.IsFalse(source.Task.IsCompleted);
        }
        finally
        {
            source.TrySetResult(42);
            await source.Task;
        }
    }

    [TestMethod]
    public async Task TimeoutAfter_CanceledWait_PreservesCancellationToken()
    {
        var source = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        Task wait = ClientTasks.TimeoutAfterAsync(source.Task, Timeout.InfiniteTimeSpan, cancellation.Token);
        try
        {
            cancellation.Cancel();
            Assert.IsFalse(source.Task.IsCompleted);
            source.SetResult(42);
            OperationCanceledException exception = await Assert.ThrowsAsync<OperationCanceledException>(() => wait);
            Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        }
        finally
        {
            source.TrySetResult(42);
            await source.Task;
        }
    }

    [TestMethod]
    [DataRow(false, null, "The operation timed out after reaching the limit of -1ms.", DisplayName = "Timeout without token or source")]
    [DataRow(false, "", "The operation timed out after reaching the limit of -1ms.", DisplayName = "Timeout without token and empty source")]
    [DataRow(false, "source.cs", "The operation at source.cs:42 timed out after reaching the limit of -1ms.", DisplayName = "Timeout without token and with source")]
    [DataRow(true, null, "The operation timed out after reaching the limit of -1ms.", DisplayName = "Timeout with token and no source")]
    [DataRow(true, "source.cs", "The operation at source.cs:42 timed out after reaching the limit of -1ms.", DisplayName = "Timeout with token and source")]
    public async Task TimeoutAfter_HelperTimeout_PreservesCallerLocation(bool withToken, string? path, string expectedMessage)
    {
        var source = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        // A pending operation may itself report a helper-origin timeout; completed task faults are
        // deliberately excluded by the fast path exercised in TimeoutAfter_CompletedTask.
        var failure = new TimeoutException("helper timeout") { Source = typeof(ClientTasks).Namespace };
        Task wait = withToken
            ? ClientTasks.TimeoutAfterAsync(source.Task, Timeout.InfiniteTimeSpan, CancellationToken.None, filePath: path, lineNumber: 42)
            : ClientTasks.TimeoutAfterAsync(source.Task, Timeout.InfiniteTimeSpan, filePath: path, lineNumber: 42);
        source.SetException(failure);

        TimeoutException exception = await Assert.ThrowsExactlyAsync<TimeoutException>(() => wait);

        Assert.AreEqual(expectedMessage, exception.Message);
        Assert.AreNotSame(failure, exception);
    }

    [TestMethod]
    [DataRow("generic-cancellation")]
    [DataRow("cancellation")]
    [DataRow("timeout")]
    [DataRow("timeout-with-token")]
    public async Task TaskWaits_DoNotCaptureCallingSynchronizationContext(string operation)
    {
        var source = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = new RecordingSynchronizationContext();
        SynchronizationContext? previous = SynchronizationContext.Current;
        Task wait;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            wait = operation switch
            {
                "generic-cancellation" => ClientTasks.WithCancellationAsync(source.Task, CancellationToken.None),
                "cancellation" => ClientTasks.WithCancellationAsync((Task)source.Task, CancellationToken.None),
                "timeout" => ClientTasks.TimeoutAfterAsync(source.Task, Timeout.InfiniteTimeSpan),
                _ => ClientTasks.TimeoutAfterAsync(source.Task, Timeout.InfiniteTimeSpan, CancellationToken.None),
            };
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
            source.TrySetResult(42);
        }

        await wait;
        Assert.AreEqual(0, context.PostCount);
    }

#if !NETCOREAPP
    [TestMethod]
    [DataRow(-2L)]
    [DataRow(0xffffffffL)]
    public async Task WaitAsync_InvalidTimeout_RejectsTimeoutWithoutCompletingTarget(long milliseconds)
    {
        var source = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            ArgumentOutOfRangeException exception = await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() =>
                ClientTasks.WaitAsync((Task)source.Task, TimeSpan.FromMilliseconds(milliseconds), CancellationToken.None));
            Assert.AreEqual("timeout", exception.ParamName);
            Assert.IsFalse(source.Task.IsCompleted);
        }
        finally
        {
            source.TrySetResult(42);
            await source.Task;
        }
    }

    [TestMethod]
    public async Task WaitAsync_CompletedAndPendingTargets_KeepResultsAndCancellationSeparate()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.AreEqual(42, await ClientTasks.WaitAsync(Task.FromResult(42), cancellation.Token));
        Assert.AreEqual(43, await ClientTasks.WaitAsync(Task.FromResult(43), TimeSpan.Zero));
        await ClientTasks.WaitAsync(Task.CompletedTask, cancellation.Token);
        // Bind the no-token wrapper explicitly rather than testing a different, cancellable overload.
        Func<Task, TimeSpan, Task> waitWithoutCancellation = ClientTasks.WaitAsync;
        await waitWithoutCancellation(Task.CompletedTask, TimeSpan.Zero);
        var source = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<int> wait = ClientTasks.WaitAsync(source.Task, Timeout.InfiniteTimeSpan, TestContext.CancellationToken);
        source.SetResult(37);
        Assert.AreEqual(37, await wait);
    }
#endif

    internal static int RunLateFaultFixture(string operation)
    {
        using var completed = new ManualResetEvent(false);
        var lifetime = TimeSpan.FromSeconds(operation is "watchdog-helper" or "watchdog-finalizer" ? 5 : 90);
        // A mutated helper or finalizer can block before collection checks, even with a starved pool.
        var watchdog = new Thread(() =>
        {
            if (!completed.WaitOne(lifetime))
            {
                // Environment.Exit can itself wait for a blocked finalizer.
                using var process = Process.GetCurrentProcess();
                Console.Error.WriteLine("The late-fault fixture exceeded its independent lifetime.");
                process.Kill();
            }
        })
        {
            IsBackground = true,
        };
        watchdog.Start();
        try
        {
            return RunLateFaultFixtureCore(operation);
        }
        finally
        {
            completed.Set();
            watchdog.Join();
        }
    }

    private static int RunLateFaultFixtureCore(string operation)
    {
        if (operation == "watchdog-helper")
        {
            var source = new TaskCompletionSource<int>();
            Task wait = ClientTasks.WithCancellationAsync(source.Task, CancellationToken.None);
            Console.WriteLine("Blocked helper wait.");
            wait.GetAwaiter().GetResult();
            throw new InvalidOperationException("The pending helper unexpectedly completed.");
        }

        if (operation == "watchdog-finalizer")
        {
            QueueBlockedFinalizer();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            throw new InvalidOperationException("The blocked finalizer unexpectedly completed.");
        }

        var observed = new ConcurrentDictionary<string, int>();
        int unrelated = 0;
        EventHandler<UnobservedTaskExceptionEventArgs> handler = (_, arguments) =>
        {
            foreach (Exception exception in arguments.Exception.Flatten().InnerExceptions)
            {
                if (exception is LateFaultException fault)
                {
                    observed.AddOrUpdate(fault.Message, 1, (_, count) => count + 1);
                }
                else
                {
                    Interlocked.Increment(ref unrelated);
                }
            }

            arguments.SetObserved();
        };
        int collected = 0;
        int falseEvents = 0;
        int trueEvents = 0;
        TaskScheduler.UnobservedTaskException += handler;
        try
        {
            foreach (bool observe in new[] { false, true })
            {
                string marker = Guid.NewGuid().ToString("N");
                WeakReference reference = CreateLateFault(operation, observe, marker);
                var watchdog = Stopwatch.StartNew();
                do
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    Thread.Yield();
                }
                while (IsAlive(reference) && watchdog.Elapsed < TimeSpan.FromSeconds(30));

                GC.WaitForPendingFinalizers();
                if (!IsAlive(reference))
                {
                    collected++;
                }

                int events = observed.TryGetValue(marker, out int count) ? count : 0;
                if (observe)
                {
                    trueEvents = events;
                }
                else
                {
                    falseEvents = events;
                }
            }
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= handler;
        }

        Console.WriteLine($"Operation={operation} Cases=2 Collected={collected} FalseEvents={falseEvents} TrueEvents={trueEvents} Unrelated={unrelated}");
        return collected == 2 && falseEvents == 1 && trueEvents == 0 && unrelated == 0 ? 0 : 18;
    }

    // Drop every strong reference before collection, including JIT-extended locals in the creator frame.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateLateFault(string operation, bool observe, string marker)
    {
        var source = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Task wait = operation switch
        {
            "generic-cancellation" => ClientTasks.WithCancellationAsync(source.Task, cancellation.Token, observe),
            "cancellation" => ClientTasks.WithCancellationAsync((Task)source.Task, cancellation.Token, observe),
            "timeout" => ClientTasks.TimeoutAfterAsync(source.Task, TimeSpan.Zero, observe, filePath: null),
            "timeout-with-token" => ClientTasks.TimeoutAfterAsync(source.Task, TimeSpan.Zero, CancellationToken.None, observe, filePath: null),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
        try
        {
            wait.GetAwaiter().GetResult();
            throw new InvalidOperationException("The pending wait unexpectedly completed successfully.");
        }
        catch (OperationCanceledException) when (operation is "generic-cancellation" or "cancellation")
        {
        }
        catch (TimeoutException) when (operation is "timeout" or "timeout-with-token")
        {
        }

        source.SetException(new LateFaultException(marker));
        return new WeakReference(source.Task);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsAlive(WeakReference reference) => reference.IsAlive;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void QueueBlockedFinalizer() => GC.KeepAlive(new BlockedFinalizer());

    private static object? InvokeGuard(string name, params object?[] arguments)
    {
        // [Embedded] hides this linked type from C# reference symbols, including friend assemblies.
        Type guard = typeof(ClientTasks).Assembly.GetType("Microsoft.Testing.Platform.Helpers.ApplicationStateGuard", throwOnError: true)!;
        MethodInfo method = guard.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(candidate => candidate.Name == name && candidate.GetParameters().Length == arguments.Length);
        try
        {
            return method.Invoke(null, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    private sealed class RecordingSynchronizationContext : SynchronizationContext
    {
        private int _postCount;

        public int PostCount => Volatile.Read(ref _postCount);

        public override void Post(SendOrPostCallback d, object? state)
        {
            Interlocked.Increment(ref _postCount);
            ThreadPool.QueueUserWorkItem(_ => d(state));
        }
    }

    private sealed class LateFaultException(string marker) : Exception(marker);

    private sealed class BlockedFinalizer
    {
        ~BlockedFinalizer()
        {
            Console.WriteLine("Blocked finalizer.");
            Thread.Sleep(Timeout.Infinite);
        }
    }
}
