// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform.ServerMode.Client.Sources.UnitTests;

/// <summary>
/// Tests for <see cref="MtpServerConnector.WaitBoundedAsync(Task, TimeSpan)"/>, the bounded-wait helper shared
/// by every teardown path that must never block indefinitely on a task it does not own.
/// </summary>
[TestClass]
public sealed class MtpServerConnectorTests
{
    public TestContext TestContext { get; set; } = null!;

    // Only this test mutates the process-global SerializerUtilities.Serializers dictionary and
    // s_clientSerializersRegistered flag (it removes the ClientInfo entry and resets the flag to force
    // CreateFormatter to re-register, then restores both in the finally block). No other test in this class
    // touches that static state, so [DoNotParallelize] is scoped to this method instead of the whole class;
    // every other test here only touches per-test TCP listeners/sockets, TaskCompletionSource instances, or
    // SynchronizationContext (which flows per-logical-call through ExecutionContext and does not leak across
    // concurrently running tests).
    [TestMethod]
    [DoNotParallelize]
    public async Task CreateFormatterRegistersClientSerializersBeforeCreatingFormatter()
    {
        FieldInfo serializersField = typeof(SerializerUtilities).GetField("Serializers", BindingFlags.NonPublic | BindingFlags.Static)!;
        FieldInfo registeredField = typeof(SerializerUtilities).GetField("s_clientSerializersRegistered", BindingFlags.NonPublic | BindingFlags.Static)!;
        var serializers = (IDictionary)serializersField.GetValue(null)!;
        Type clientInfoType = typeof(ClientInfo);
        object clientInfoSerializer = serializers[clientInfoType]!;

        serializers.Remove(clientInfoType);
        registeredField.SetValue(null, false);
        try
        {
            IMessageFormatter formatter = MtpServerConnector.CreateFormatter();

            string json = await formatter.SerializeAsync(new ClientInfo("mutation-test", "1.0")).ConfigureAwait(false);

            Assert.Contains("mutation-test", json);
        }
        finally
        {
            serializers[clientInfoType] = clientInfoSerializer;
            registeredField.SetValue(null, false);
            SerializerUtilities.RegisterClientSerializers();
        }
    }

    [TestMethod]
    public async Task StartLoopbackListenerStartsListeningAndReturnsBoundPort()
    {
        TcpListener listener = MtpServerConnector.StartLoopbackListener(out int port);
        try
        {
            Assert.IsGreaterThan(0, port);

            using var client = new TcpClient();
            Task<TcpClient> acceptTask = AcceptTcpClientAsync(listener);
            await ConnectAsync(client, IPAddress.Loopback, port).ConfigureAwait(false);
            using TcpClient accepted = await acceptTask.ConfigureAwait(false);

            Assert.IsTrue(client.Connected);
            Assert.IsTrue(accepted.Connected);
        }
        finally
        {
            listener.Stop();
        }
    }

    [TestMethod]
    public void BuildInProcessServerArgumentsReturnsCompleteInvariantArgumentList()
    {
        string[] arguments = MtpServerConnector.BuildInProcessServerArguments(12345);

        Assert.AreSequenceEqual(
            new[] { "--server", "jsonrpc", "--client-host", "127.0.0.1", "--client-port", "12345", "--no-banner" },
            arguments);
    }

    [TestMethod]
    public async Task AcceptAsyncReturnsConnectedClientWithNoDelayEnabled()
    {
        TcpListener listener = MtpServerConnector.StartLoopbackListener(out int port);
        using var dialingClient = new TcpClient();
        try
        {
            Task<TcpClient> acceptTask = MtpServerConnector.AcceptAsync(
                listener,
                _ => Task.FromResult<Exception?>(null),
                _ => Task.FromResult<Exception>(new TimeoutException()),
                TimeSpan.FromSeconds(30),
                serverCompletion: null,
                CancellationToken.None);

            await ConnectAsync(dialingClient, IPAddress.Loopback, port).ConfigureAwait(false);
            using TcpClient acceptedClient = await acceptTask.ConfigureAwait(false);

            Assert.IsTrue(acceptedClient.Connected);
            Assert.IsTrue(acceptedClient.NoDelay);
        }
        finally
        {
            listener.Stop();
        }
    }

    [TestMethod]
    public async Task AcceptAsyncWhenServerStopsDisposesClientAcceptedAfterFailure()
    {
        TcpListener listener = MtpServerConnector.StartLoopbackListener(out int port);
        using var dialingClient = new TcpClient();
        var expected = new InvalidOperationException("server stopped");
        try
        {
            InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => MtpServerConnector.AcceptAsync(
                    listener,
                    _ => Task.FromResult<Exception?>(expected),
                    _ => Task.FromResult<Exception>(new TimeoutException()),
                    TimeSpan.FromSeconds(30),
                    serverCompletion: null,
                    CancellationToken.None));

            Assert.AreSame(expected, actual);

            await ConnectAsync(dialingClient, IPAddress.Loopback, port).ConfigureAwait(false);
            NetworkStream stream = dialingClient.GetStream();
            byte[] buffer = new byte[1];
            Task<int> readTask = stream.ReadAsync(buffer, 0, buffer.Length, TestContext.CancellationToken);
            Task completed = await Task.WhenAny(readTask, Task.Delay(TimeSpan.FromSeconds(5), TestContext.CancellationToken)).ConfigureAwait(false);

            Assert.AreSame(readTask, completed, "The late accepted socket should be disposed immediately.");
            Assert.AreEqual(0, await readTask.ConfigureAwait(false));
        }
        finally
        {
            listener.Stop();
        }
    }

    [TestMethod]
    public async Task AcceptAsyncChecksCancellationAfterAcceptBeforeReturningClient()
    {
        TcpListener listener = MtpServerConnector.StartLoopbackListener(out int port);
        using var dialingClient = new TcpClient();
        using var cancellation = new CancellationTokenSource();
        try
        {
            await ConnectAsync(dialingClient, IPAddress.Loopback, port).ConfigureAwait(false);
            Assert.IsTrue(
                SpinWait.SpinUntil(listener.Pending, TimeSpan.FromSeconds(5)),
                "The connection must be queued before AcceptAsync starts so cancellation occurs after accept.");

            await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                () => MtpServerConnector.AcceptAsync(
                    listener,
                    _ =>
                    {
                        cancellation.Cancel();
                        return Task.FromResult<Exception?>(null);
                    },
                    _ => Task.FromResult<Exception>(new TimeoutException()),
                    TimeSpan.FromSeconds(30),
                    serverCompletion: null,
                    cancellation.Token));
        }
        finally
        {
            listener.Stop();
        }
    }

    [TestMethod]
    public async Task AcceptAsyncThrowsStoppedFailureObservedAfterAccept()
    {
        TcpListener listener = MtpServerConnector.StartLoopbackListener(out int port);
        using var dialingClient = new TcpClient();
        var expected = new InvalidOperationException("stopped after accept");
        try
        {
            await ConnectAsync(dialingClient, IPAddress.Loopback, port).ConfigureAwait(false);
            Assert.IsTrue(
                SpinWait.SpinUntil(listener.Pending, TimeSpan.FromSeconds(5)),
                "The connection must be queued so the stopped probe runs after accept.");

            InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => MtpServerConnector.AcceptAsync(
                    listener,
                    _ => Task.FromResult<Exception?>(expected),
                    _ => Task.FromResult<Exception>(new TimeoutException()),
                    TimeSpan.FromSeconds(30),
                    serverCompletion: null,
                    CancellationToken.None));

            Assert.AreSame(expected, actual);
        }
        finally
        {
            listener.Stop();
        }
    }

    [TestMethod]
    public async Task AcceptAsyncServerCompletionRechecksStoppedStateWithoutWaitingForPollDelay()
    {
        TcpListener listener = MtpServerConnector.StartLoopbackListener(out _);
        var expected = new InvalidOperationException("server stopped");
        int probes = 0;
        try
        {
            Task<TcpClient> acceptTask = MtpServerConnector.AcceptAsync(
                listener,
                _ => Task.FromResult<Exception?>(Interlocked.Increment(ref probes) == 1 ? null : expected),
                _ => Task.FromResult<Exception>(new TimeoutException()),
                TimeSpan.FromSeconds(30),
                Task.CompletedTask,
                CancellationToken.None);

            Task completed = await Task.WhenAny(acceptTask, Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.CancellationToken)).ConfigureAwait(false);

            Assert.AreSame(acceptTask, completed, "A completed server signal must bypass the 100ms poll delay.");
            InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => acceptTask);
            Assert.AreSame(expected, actual);
            Assert.AreEqual(2, probes);
        }
        finally
        {
            listener.Stop();
        }
    }

    [TestMethod]
    public async Task AcceptAsyncServerStoppedProbeDoesNotCaptureSynchronizationContext()
    {
        TcpListener listener = MtpServerConnector.StartLoopbackListener(out int port);
        using var dialingClient = new TcpClient();
        var probeResult = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        int probes = 0;
        try
        {
            await AssertDoesNotCaptureSynchronizationContextAsync(
                () => MtpServerConnector.AcceptAsync(
                    listener,
                    _ => Interlocked.Increment(ref probes) == 1
                        ? probeResult.Task
                        : Task.FromResult<Exception?>(null),
                    _ => Task.FromResult<Exception>(new TimeoutException()),
                    TimeSpan.FromSeconds(30),
                    serverCompletion: null,
                    CancellationToken.None),
                () =>
                {
                    probeResult.SetResult(null);
                    dialingClient.Connect(IPAddress.Loopback, port);
                }).ConfigureAwait(false);
        }
        finally
        {
            listener.Stop();
        }
    }

    [TestMethod]
    public async Task AcceptAsyncTimeoutFactoryDoesNotCaptureSynchronizationContext()
    {
        TcpListener listener = MtpServerConnector.StartLoopbackListener(out _);
        var timeoutFailure = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = new TimeoutException("expected");
        Task<TcpClient>? acceptTask = null;
        try
        {
            await AssertDoesNotCaptureSynchronizationContextAsync(
                () =>
                {
                    acceptTask = MtpServerConnector.AcceptAsync(
                        listener,
                        _ => Task.FromResult<Exception?>(null),
                        _ => timeoutFailure.Task,
                        TimeSpan.Zero,
                        serverCompletion: null,
                        CancellationToken.None);
                    return IgnoreFailureAsync(acceptTask);
                },
                () => timeoutFailure.SetResult(expected)).ConfigureAwait(false);

            TimeoutException actual = await Assert.ThrowsExactlyAsync<TimeoutException>(() => acceptTask!);
            Assert.AreSame(expected, actual);
        }
        finally
        {
            listener.Stop();
        }
    }

    [TestMethod]
    public async Task AcceptAsyncPollWaitDoesNotCaptureSynchronizationContext()
    {
        TcpListener listener = MtpServerConnector.StartLoopbackListener(out int port);
        using var dialingClient = new TcpClient();
        try
        {
            await AssertDoesNotCaptureSynchronizationContextAsync(
                () => MtpServerConnector.AcceptAsync(
                    listener,
                    _ => Task.FromResult<Exception?>(null),
                    _ => Task.FromResult<Exception>(new TimeoutException()),
                    TimeSpan.FromSeconds(30),
                    serverCompletion: null,
                    CancellationToken.None),
                () => dialingClient.Connect(IPAddress.Loopback, port)).ConfigureAwait(false);
        }
        finally
        {
            listener.Stop();
        }
    }

    [TestMethod]
    public async Task AcceptAsyncPollWaitWithServerCompletionDoesNotCaptureSynchronizationContext()
    {
        TcpListener listener = MtpServerConnector.StartLoopbackListener(out int port);
        using var dialingClient = new TcpClient();
        var serverCompletion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await AssertDoesNotCaptureSynchronizationContextAsync(
                () => MtpServerConnector.AcceptAsync(
                    listener,
                    _ => Task.FromResult<Exception?>(null),
                    _ => Task.FromResult<Exception>(new TimeoutException()),
                    TimeSpan.FromSeconds(30),
                    serverCompletion.Task,
                    CancellationToken.None),
                () =>
                {
                    serverCompletion.SetResult(null);
                    dialingClient.Connect(IPAddress.Loopback, port);
                }).ConfigureAwait(false);
        }
        finally
        {
            listener.Stop();
        }
    }

    [TestMethod]
    public async Task AcceptAsyncFinalStoppedProbeDoesNotCaptureSynchronizationContext()
    {
        TcpListener listener = MtpServerConnector.StartLoopbackListener(out int port);
        using var dialingClient = new TcpClient();
        var probeResult = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await ConnectAsync(dialingClient, IPAddress.Loopback, port).ConfigureAwait(false);
            Assert.IsTrue(
                SpinWait.SpinUntil(listener.Pending, TimeSpan.FromSeconds(5)),
                "The connection must be queued so AcceptAsync reaches the final stopped probe synchronously.");

            await AssertDoesNotCaptureSynchronizationContextAsync(
                () => MtpServerConnector.AcceptAsync(
                    listener,
                    _ => probeResult.Task,
                    _ => Task.FromResult<Exception>(new TimeoutException()),
                    TimeSpan.FromSeconds(30),
                    serverCompletion: null,
                    CancellationToken.None),
                () => probeResult.SetResult(null)).ConfigureAwait(false);
        }
        finally
        {
            listener.Stop();
        }
    }

    [TestMethod]
    public void SafeStopStopsListener()
    {
        TcpListener listener = MtpServerConnector.StartLoopbackListener(out int port);

        MtpServerConnector.SafeStop(listener, NullMtpClientLogger.Instance);

        var replacement = new TcpListener(IPAddress.Loopback, port);
        try
        {
            replacement.Start();
        }
        finally
        {
            replacement.Stop();
        }
    }

    [TestMethod]
    public void GetCurrentProcessIdReturnsCallerProcessId()
    {
        using var current = Process.GetCurrentProcess();

        Assert.AreEqual(current.Id, MtpServerConnector.GetCurrentProcessId());
    }

    [TestMethod]
    public async Task WaitBoundedAsync_TaskAlreadyCompleted_ReturnsTrueWithoutWaiting()
    {
        Task completedTask = Task.CompletedTask;

        // A non-positive timeout would normally short-circuit to false, so passing one here specifically
        // proves the already-completed check runs (and wins) before the timeout is ever inspected.
        bool result = await MtpServerConnector.WaitBoundedAsync(completedTask, TimeSpan.Zero).ConfigureAwait(false);

        Assert.IsTrue(result, "An already-completed task must report completion regardless of the timeout.");
    }

    [TestMethod]
    public async Task WaitBoundedAsync_NonPositiveTimeoutAndIncompleteTask_ReturnsFalseWithoutWaiting()
    {
        var neverCompletes = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        bool result = await MtpServerConnector.WaitBoundedAsync(neverCompletes.Task, TimeSpan.Zero).ConfigureAwait(false);

        Assert.IsFalse(result, "A non-positive timeout must degrade to 'check, do not wait' rather than waiting forever.");
    }

    [TestMethod]
    public async Task WaitBoundedAsync_TaskCompletesBeforeTimeout_ReturnsTrue()
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> waitTask = MtpServerConnector.WaitBoundedAsync(tcs.Task, TimeSpan.FromSeconds(30));

        tcs.TrySetResult(true);

        bool result = await waitTask.ConfigureAwait(false);

        Assert.IsTrue(result, "The task completing before the timeout elapses must report completion.");
    }

    [TestMethod]
    public async Task WaitBoundedAsync_TimeoutElapsesBeforeTaskCompletes_ReturnsFalse()
    {
        var neverCompletes = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        bool result = await MtpServerConnector.WaitBoundedAsync(neverCompletes.Task, TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);

        Assert.IsFalse(result, "A short timeout against a task that never completes must report a timeout, not completion.");
    }

    [TestMethod]
    public async Task WaitBoundedAsyncDoesNotCaptureSynchronizationContext()
    {
        var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool>? waitTask = null;

        await AssertDoesNotCaptureSynchronizationContextAsync(
            () => waitTask = MtpServerConnector.WaitBoundedAsync(completion.Task, TimeSpan.FromSeconds(30)),
            () => completion.SetResult(null)).ConfigureAwait(false);

        Assert.IsTrue(await waitTask!.ConfigureAwait(false));
    }

    [TestMethod]
    public void ObserveFailureForCompletedFaultedTaskLogsDescriptionAndException()
    {
        var log = new List<(MtpClientLogLevel Level, string Message)>();
        var expected = new InvalidOperationException("completed failure");

        MtpServerConnector.ObserveFailure(Task.FromException(expected), new DelegateMtpClientLogger((level, message) => log.Add((level, message))), "operation failed");

        Assert.HasCount(1, log);
        Assert.AreEqual(MtpClientLogLevel.Error, log[0].Level);
        Assert.Contains("operation failed", log[0].Message);
        Assert.Contains("completed failure", log[0].Message);
    }

    [TestMethod]
    public async Task ObserveFailureForIncompleteTaskLogsFailureWhenItCompletes()
    {
        var logged = new TaskCompletionSource<(MtpClientLogLevel Level, string Message)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        MtpServerConnector.ObserveFailure(
            task.Task,
            new DelegateMtpClientLogger((level, message) => logged.TrySetResult((level, message))),
            "late operation failed");
        task.SetException(new InvalidOperationException("late failure"));

        Task completed = await Task.WhenAny(logged.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.CancellationToken)).ConfigureAwait(false);
        Assert.AreSame(logged.Task, completed);
        Assert.AreEqual(MtpClientLogLevel.Error, logged.Task.Result.Level);
        Assert.Contains("late operation failed", logged.Task.Result.Message);
        Assert.Contains("late failure", logged.Task.Result.Message);
    }

    [TestMethod]
    public async Task ObserveFailureForIncompleteSuccessfulTaskDoesNotLog()
    {
        int logCount = 0;
        var task = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        MtpServerConnector.ObserveFailure(
            task.Task,
            new DelegateMtpClientLogger((_, _) => Interlocked.Increment(ref logCount)),
            "operation");
        task.SetResult(null);
        await task.Task.ConfigureAwait(false);
        await Task.Delay(50, TestContext.CancellationToken).ConfigureAwait(false);

        Assert.AreEqual(0, logCount);
    }

    [TestMethod]
    public void NeutralizePendingAcceptObservesFaultedTaskFailure()
    {
        const string FailureMessage = "neutralized accept failure";
        int unobservedCount = 0;
        EventHandler<UnobservedTaskExceptionEventArgs> handler = (_, e) =>
        {
            if (e.Exception.ToString().Contains(FailureMessage, StringComparison.Ordinal))
            {
                Interlocked.Increment(ref unobservedCount);
                e.SetObserved();
            }
        };

        TaskScheduler.UnobservedTaskException += handler;
        try
        {
            WeakReference taskReference = CreateAndNeutralizeFaultedAccept(FailureMessage);
            for (int i = 0; i < 10 && taskReference.IsAlive; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            Assert.IsFalse(taskReference.IsAlive, "The faulted task should become collectible during the test.");
            Assert.AreEqual(0, unobservedCount, "Neutralizing a faulted accept must observe its exception.");
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= handler;
        }
    }

    private async Task AssertDoesNotCaptureSynchronizationContextAsync(Func<Task> startOperation, Action releaseOperation)
    {
        var context = new QueuedSynchronizationContext();
        SynchronizationContext? previous = SynchronizationContext.Current;
        Task operation;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            operation = startOperation();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        releaseOperation();
        Task completed = await Task.WhenAny(operation, Task.Delay(TimeSpan.FromSeconds(2), TestContext.CancellationToken)).ConfigureAwait(false);
        context.RunAll();

        Assert.AreSame(operation, completed, "The operation captured and posted back to the caller's synchronization context.");
        await operation.ConfigureAwait(false);
        Assert.AreEqual(0, context.PostCount);
    }

    private Task<TcpClient> AcceptTcpClientAsync(TcpListener listener) =>
#if NET
        listener.AcceptTcpClientAsync(TestContext.CancellationToken).AsTask();
#else
        listener.AcceptTcpClientAsync();
#endif

    private Task ConnectAsync(TcpClient client, IPAddress address, int port) =>
#if NET
        client.ConnectAsync(address, port, TestContext.CancellationToken).AsTask();
#else
        client.ConnectAsync(address, port);
#endif

    private static async Task IgnoreFailureAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception)
        {
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateAndNeutralizeFaultedAccept(string message)
    {
        Task<TcpClient> acceptTask = Task.FromException<TcpClient>(new InvalidOperationException(message));
        MethodInfo method = typeof(MtpServerConnector).GetMethod(
            "NeutralizePendingAccept",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        _ = method.Invoke(null, [acceptTask]);
        return new WeakReference(acceptTask);
    }

    private sealed class QueuedSynchronizationContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _callbacks = new();

        public int PostCount { get; private set; }

        public override void Post(SendOrPostCallback d, object? state)
        {
            lock (_callbacks)
            {
                PostCount++;
                _callbacks.Enqueue((d, state));
            }
        }

        public void RunAll()
        {
            while (true)
            {
                (SendOrPostCallback Callback, object? State) work;
                lock (_callbacks)
                {
                    if (_callbacks.Count == 0)
                    {
                        return;
                    }

                    work = _callbacks.Dequeue();
                }

                work.Callback(work.State);
            }
        }
    }
}
