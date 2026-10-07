// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.ServerMode.Client;

namespace Microsoft.Testing.Platform.ServerMode.Client.Sources.UnitTests;

/// <summary>
/// Tests for <see cref="MtpServerClient.LaunchInProcessAsync"/>: the launch path for embedded hosts that
/// cannot spawn a child process. The callback plays the part of the hosted MTP application — it parses the
/// server-mode arguments the client generated, dials back to the client's loopback listener, and serves the
/// real wire protocol through <see cref="FakeMtpServer"/>.
/// </summary>
/// <remarks>
/// Everything below the callback is production code: the client owns the listener, the argument array, the
/// connect race, the serializer/formatter/transport setup and the shutdown sequence. That is exactly what an
/// embedded host must not have to reimplement.
/// </remarks>
[TestClass]
public sealed class MtpServerClientInProcessTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The id of the process the tests run in. An in-process host runs the application here, so this is also
    /// the id the client and the server-mode handshake must report.
    /// </summary>
    private static readonly int CurrentProcessId = GetCurrentProcessId();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task LaunchInProcessAsync_PassesCompleteServerModeArguments()
    {
        using var server = new InProcessServerFixture();

        using MtpServerClient client = await LaunchAsync(server);

        string[] arguments = server.Arguments;
        Assert.AreSequenceEqual(
            ["--server", "jsonrpc", "--client-host", "127.0.0.1", "--client-port"],
            arguments.Take(5),
            $"The client must hand the callback a complete, ordered server-mode argument array. Actual: {string.Join(" ", arguments)}");
        Assert.IsTrue(
            int.TryParse(arguments[5], out int port) && port > 0,
            $"'--client-port' must carry the client's listener port. Actual: {string.Join(" ", arguments)}");
        Assert.AreEqual("--no-banner", arguments[6]);
        Assert.HasCount(7, arguments);
        Assert.Contains(
            $"Hosting an MTP server in process with '{string.Join(" ", arguments)}'.",
            server.Log,
            "The launch diagnostic must include the complete argument array handed to the callback.");
    }

    [TestMethod]
    public async Task LaunchInProcessAsync_DrivesInitializeDiscoverRunAndExit()
    {
        using var server = new InProcessServerFixture();
        var discoverRunId = Guid.NewGuid();
        var runRunId = Guid.NewGuid();

        using MtpServerClient client = await LaunchAsync(server);
        List<MtpTestNodeUpdate> updates = [];
        object gate = new();
        client.TestNodesUpdated += (_, e) =>
        {
            lock (gate)
            {
                updates.AddRange(e.Changes);
            }
        };

        MtpServerCapabilities capabilities = await WithTimeoutAsync(client.InitializeAsync(TestContext.CancellationToken));
        Assert.AreEqual("FakeMtpServer", capabilities.ServerName);
        Assert.AreEqual(CurrentProcessId, client.ProcessId, "An in-process host runs the application in the caller's process.");

        await server.Value.SendDiscoveredTestNodeAsync(discoverRunId, "uid-1", "Test1");
        await WithTimeoutAsync(client.DiscoverTestsAsync(TestContext.CancellationToken));

        await server.Value.SendPassedTestNodeAsync(runRunId, "uid-1", "Test1");
        MtpRunResult result = await WithTimeoutAsync(client.RunTestsAsync(TestContext.CancellationToken));
        Assert.IsEmpty(result.Artifacts);

        await WithTimeoutAsync(client.ExitAsync(TestContext.CancellationToken));
        await WithTimeoutAsync(server.Value.WaitForNotificationAsync(JsonRpcMethods.Exit, DefaultTimeout));

        List<MtpTestNodeUpdate> snapshot;
        lock (gate)
        {
            snapshot = [.. updates];
        }

        Assert.ContainsSingle(update => update.Uid == "uid-1" && update.ExecutionState == "discovered", snapshot);
        Assert.ContainsSingle(update => update.Uid == "uid-1" && update.ExecutionState == "passed", snapshot);
    }

    [TestMethod]
    public async Task LaunchInProcessAsync_CallbackFaultsBeforeConnecting_PreservesCallbackException()
    {
        var callbackFailure = new InvalidOperationException("The embedded host could not start the application.");

        MtpServerConnectionClosedException exception = await AssertThrowsAsync<MtpServerConnectionClosedException>(
            () => MtpServerClient.LaunchInProcessAsync(
                (_, _) => throw callbackFailure,
                CreateOptions(),
                TestContext.CancellationToken));

        Assert.AreSame(
            callbackFailure,
            exception.InnerException,
            "The callback's own exception must survive as the inner exception instead of being replaced by a timeout.");
    }

    [TestMethod]
    public async Task LaunchInProcessAsync_CallbackFaultsAsynchronouslyBeforeConnecting_PreservesCallbackException()
    {
        var callbackFailure = new InvalidOperationException("The application crashed during startup.");

        MtpServerConnectionClosedException exception = await AssertThrowsAsync<MtpServerConnectionClosedException>(
            () => MtpServerClient.LaunchInProcessAsync(
                async (_, _) =>
                {
                    await Task.Yield();
                    throw callbackFailure;
                },
                CreateOptions(),
                TestContext.CancellationToken));

        Assert.AreSame(callbackFailure, exception.InnerException);
        Assert.Contains("failed before connecting back", exception.Message);
    }

    [TestMethod]
    public async Task LaunchInProcessAsync_CallbackReportsMultipleFailuresBeforeConnecting_PreservesAggregateException()
    {
        var firstFailure = new InvalidOperationException("The first startup operation failed.");
        var secondFailure = new IOException("The second startup operation failed.");
        var callbackCompletion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        callbackCompletion.SetException([firstFailure, secondFailure]);

        MtpServerConnectionClosedException exception = await AssertThrowsAsync<MtpServerConnectionClosedException>(
            () => MtpServerClient.LaunchInProcessAsync(
                (_, _) => callbackCompletion.Task,
                CreateOptions(),
                TestContext.CancellationToken));

        AggregateException aggregateException = Assert.IsInstanceOfType<AggregateException>(exception.InnerException);
        Assert.HasCount(2, aggregateException.InnerExceptions);
        Assert.AreSame(firstFailure, aggregateException.InnerExceptions[0]);
        Assert.AreSame(secondFailure, aggregateException.InnerExceptions[1]);
    }

    [TestMethod]
    public async Task LaunchInProcessAsync_CallbackBlocksBeforeReturningTask_ReturnsWithoutBlockingCaller()
    {
        using var releaseCallback = new ManualResetEventSlim(initialState: false);
        var callbackEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = new InProcessServerFixture();

        // StartNew intentionally does not unwrap the launch task: this test must observe whether invoking the
        // async factory itself returns while the callback is blocked in its synchronous prefix.
        Task<Task<MtpServerClient>> invocation = Task.Factory.StartNew(
            () => MtpServerClient.LaunchInProcessAsync(
                (arguments, serverToken) =>
                {
                    callbackEntered.TrySetResult(true);
                    releaseCallback.Wait(serverToken);
                    return server.RunAsync(arguments, serverToken);
                },
                CreateOptions(),
                TestContext.CancellationToken),
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default);

        await WithTimeoutAsync(callbackEntered.Task);
        try
        {
            Task completed = await Task.WhenAny(invocation, Task.Delay(TimeSpan.FromSeconds(2), TestContext.CancellationToken));
            Assert.AreSame(invocation, completed, "Calling LaunchInProcessAsync must return its task without waiting for the callback's synchronous prefix.");

            Task<MtpServerClient> launch = await invocation;
            Assert.IsFalse(launch.IsCompleted, "The launch must still be waiting for the gated callback to connect.");
            releaseCallback.Set();

            using MtpServerClient client = await WithTimeoutAsync(launch);
            _ = await WithTimeoutAsync(server.Connected);
            _ = await WithTimeoutAsync(client.InitializeAsync(TestContext.CancellationToken));
        }
        finally
        {
            releaseCallback.Set();
        }
    }

    [TestMethod]
    public async Task LaunchInProcessAsync_AbandonedCallback_KeepsCancellationSourceAlive()
    {
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackStarted = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        MtpServerClientOptions options = CreateOptions();
        options.ConnectionTimeout = TimeSpan.FromMilliseconds(50);

        try
        {
            Task<MtpServerClient> launch = MtpServerClient.LaunchInProcessAsync(
                async (_, serverToken) =>
                {
                    callbackStarted.TrySetResult(serverToken);
                    await release.Task;
                    GC.KeepAlive(serverToken.WaitHandle);
                    return 0;
                },
                options,
                TestContext.CancellationToken);
            CancellationToken serverToken = await WithTimeoutAsync(callbackStarted.Task);

            _ = await AssertThrowsAsync<MtpServerConnectionClosedException>(() => launch);

            _ = serverToken.WaitHandle;
        }
        finally
        {
            release.TrySetResult(true);
        }
    }

    [TestMethod]
    public async Task LaunchInProcessAsync_CallbackIsCanceledBeforeConnecting_PreservesCancellationException()
    {
        MtpServerConnectionClosedException exception = await AssertThrowsAsync<MtpServerConnectionClosedException>(
            () => MtpServerClient.LaunchInProcessAsync(
                (_, _) => Task.FromCanceled<int>(new CancellationToken(canceled: true)),
                CreateOptions(),
                TestContext.CancellationToken));

        TaskCanceledException cancellationException = Assert.IsInstanceOfType<TaskCanceledException>(exception.InnerException);
        Assert.IsNotNull(cancellationException.Task);
        Assert.Contains("was canceled before connecting back", exception.Message);
    }

    [TestMethod]
    public async Task LaunchInProcessAsync_CallbackExitsWithoutConnecting_FailsFastInsteadOfWaitingOutTheTimeout()
    {
        // The connect race probes "has the server stopped?" between accept polls. A server that stopped can
        // no longer serve the connection, so the launch must report that precise failure (with its exit code)
        // immediately rather than waiting for the connection timeout or handing back a dead client.
        MtpServerClientOptions options = CreateOptions();
        options.ConnectionTimeout = TimeSpan.FromMinutes(5);

        var stopwatch = Stopwatch.StartNew();
        MtpServerConnectionClosedException exception = await AssertThrowsAsync<MtpServerConnectionClosedException>(
            () => MtpServerClient.LaunchInProcessAsync(
                (_, _) => Task.FromResult(9),
                options,
                TestContext.CancellationToken));
        stopwatch.Stop();

        Assert.Contains("exited with code 9", exception.Message);
        Assert.IsLessThan(
            TimeSpan.FromSeconds(20),
            stopwatch.Elapsed,
            "A server that stopped without connecting must fail fast, not wait out the connection timeout.");
    }

    [TestMethod]
    public async Task LaunchInProcessAsync_CallbackExitsBeforeConnecting_ReportsExitCode()
    {
        MtpServerConnectionClosedException exception = await AssertThrowsAsync<MtpServerConnectionClosedException>(
            () => MtpServerClient.LaunchInProcessAsync(
                (_, _) => Task.FromResult(3),
                CreateOptions(),
                TestContext.CancellationToken));

        Assert.Contains(
            "exited with code 3",
            exception.Message,
            "A callback that returns without connecting back must be reported with its exit code, not as a timeout.");
        Assert.Contains(
            "Make sure the callback forwards the supplied server-mode arguments",
            exception.Message,
            "The failure must tell embedded hosts how to correct a callback that exits before connecting.");
    }

    [TestMethod]
    public async Task LaunchInProcessAsync_CallbackExitsBeforeConnecting_StopsListenerAndDisposesCancellationSource()
    {
        string[]? arguments = null;
        CancellationToken serverToken = default;

        _ = await AssertThrowsAsync<MtpServerConnectionClosedException>(
            () => MtpServerClient.LaunchInProcessAsync(
                (serverArguments, cancellationToken) =>
                {
                    arguments = serverArguments;
                    serverToken = cancellationToken;
                    return Task.FromResult(7);
                },
                CreateOptions(),
                TestContext.CancellationToken));

        Assert.IsNotNull(arguments);
        int port = int.Parse(ReadArgument(arguments, "--client-port"), CultureInfo.InvariantCulture);
        using var listener = new DisposableTcpListener(IPAddress.Loopback, port);
        listener.Start();
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = serverToken.WaitHandle);
    }

    [TestMethod]
    public async Task CleanupFailedStartAsync_DisposesCreatedResourcesWithExactDiagnostics()
    {
        var log = new List<(MtpClientLogLevel Level, string Message)>();
        var logger = new DelegateMtpClientLogger((level, message) => log.Add((level, message)));
        using var listener = new DisposableTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var client = new ThrowingTcpClient();
        var cancellation = new ThrowingCancellationTokenSource();

        await MtpServerInProcessHost.CleanupFailedStartAsync(
            listener,
            client,
            cancellation,
            serverTask: null,
            logger);

        Assert.AreEqual(1, client.DisposeCount);
        Assert.AreEqual(1, cancellation.DisposeCount);
        Assert.ContainsSingle(
            entry => entry.Level == MtpClientLogLevel.Debug
                && entry.Message.Contains("Disposing the accepted client socket threw:", StringComparison.Ordinal)
                && entry.Message.Contains("client dispose failure", StringComparison.Ordinal),
            log);
        Assert.ContainsSingle(
            entry => entry.Level == MtpClientLogLevel.Debug
                && entry.Message.Contains("Disposing the server cancellation source threw:", StringComparison.Ordinal)
                && entry.Message.Contains("cancellation dispose failure", StringComparison.Ordinal),
            log);

        using var replacement = new DisposableTcpListener(IPAddress.Loopback, port);
        replacement.Start();
    }

    [TestMethod]
    public async Task CleanupFailedStartAsync_DoesNotCaptureTheCallingSynchronizationContext()
    {
        using var listener = new DisposableTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new TcpClient();
        using var cancellation = new CancellationTokenSource();
        Task<int> serverTask = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(Timeout.Infinite, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                await Task.Delay(100, TestContext.CancellationToken);
            }

            return 0;
        });
        Task cleanup = InvokeWithSynchronizationContext(
            () => MtpServerInProcessHost.CleanupFailedStartAsync(
                listener,
                client,
                cancellation,
                serverTask,
                NullMtpClientLogger.Instance),
            out QueueingSynchronizationContext context);

        bool completedWithoutPumping = await CompletesQuicklyAsync(cleanup);
        DrainContextUntilCompleted(context, cleanup);
        await cleanup;

        Assert.IsTrue(completedWithoutPumping, "Failed-launch cleanup must not capture the calling synchronization context.");
    }

    [TestMethod]
    public async Task LaunchInProcessAsync_CallbackReturnsNullTask_Fails()
    {
        MtpServerConnectionClosedException exception = await AssertThrowsAsync<MtpServerConnectionClosedException>(
            () => MtpServerClient.LaunchInProcessAsync(
#pragma warning disable VSTHRD114 // Avoid returning null from a Task-returning method - this test deliberately supplies a misbehaving callback.
                (_, _) => null!,
#pragma warning restore VSTHRD114
                CreateOptions(),
                TestContext.CancellationToken));

        Assert.IsInstanceOfType<MtpServerClientException>(exception.InnerException);
        Assert.Contains("returned a null task", exception.InnerException.Message);
    }

    [TestMethod]
    public async Task LaunchInProcessAsync_NullCallback_Throws()
    {
        ArgumentNullException exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => MtpServerClient.LaunchInProcessAsync(null!, CreateOptions(), TestContext.CancellationToken));

        Assert.DoesNotContain(nameof(MtpServerInProcessHost), exception.StackTrace ?? string.Empty);
    }

    [TestMethod]
    public async Task LaunchInProcessAsync_DoesNotCaptureTheCallingSynchronizationContext()
    {
        using var server = new InProcessServerFixture();

        using MtpServerClient client = await AssertCompletesWithoutPumpingAsync(
            () => MtpServerClient.LaunchInProcessAsync(
                server.RunAsync,
                CreateOptions(),
                TestContext.CancellationToken));

        _ = await WithTimeoutAsync(server.Connected);
    }

    [TestMethod]
    public async Task LaunchInProcessAsync_ZeroTimeoutFailedStartCleanup_DoesNotCaptureTheCallingSynchronizationContext()
    {
        MtpServerClientOptions options = CreateOptions();
        options.ConnectionTimeout = TimeSpan.Zero;

        Task<MtpServerClient> launch = InvokeWithSynchronizationContext(
            () => MtpServerClient.LaunchInProcessAsync(
                async (_, serverToken) =>
                {
                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, serverToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (serverToken.IsCancellationRequested)
                    {
                        await Task.Delay(100, TestContext.CancellationToken).ConfigureAwait(false);
                    }

                    return 0;
                },
                options,
                TestContext.CancellationToken),
            out QueueingSynchronizationContext context);

        bool completedWithoutPumping = await CompletesQuicklyAsync(launch);
        DrainContextUntilCompleted(context, launch);
        MtpServerConnectionClosedException exception = await Assert.ThrowsExactlyAsync<MtpServerConnectionClosedException>(
            () => launch);

        Assert.IsTrue(
            completedWithoutPumping,
            "Failed-launch cleanup must not capture the calling synchronization context.");
        Assert.Contains("did not connect back within", exception.Message);
    }

    [TestMethod]
    public async Task LaunchInProcessAsync_AlreadyCanceled_DoesNotInvokeCallback()
    {
        using var alreadyCanceled = new CancellationTokenSource();
        alreadyCanceled.Cancel();
        int invocations = 0;

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => MtpServerClient.LaunchInProcessAsync(
                (_, _) =>
                {
                    _ = Interlocked.Increment(ref invocations);
                    return Task.FromResult(0);
                },
                CreateOptions(),
                alreadyCanceled.Token));

        Assert.AreEqual(0, Volatile.Read(ref invocations), "A pre-canceled launch must not start the application.");
    }

    [TestMethod]
    public async Task LaunchInProcessAsync_CanceledWhileConnecting_CancelsTheCallbackToken()
    {
        using var cancellation = new CancellationTokenSource();
        var callbackObservedCancellation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // The callback never dials back, so the launch stays in the connect race until the caller cancels.
        Task<MtpServerClient> launch = MtpServerClient.LaunchInProcessAsync(
            async (serverArguments, serverToken) =>
            {
                callbackStarted.TrySetResult(true);
                using CancellationTokenRegistration registration = serverToken.Register(() => callbackObservedCancellation.TrySetResult(true));
                await callbackObservedCancellation.Task;
                return 0;
            },
            CreateOptions(),
            cancellation.Token);

        await WithTimeoutAsync(callbackStarted.Task);
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => WithTimeoutAsync(launch));
        Assert.IsTrue(
            await WithTimeoutAsync(callbackObservedCancellation.Task),
            "A canceled launch must cancel the token handed to the callback so the abandoned application can stop.");
    }

    [TestMethod]
    public async Task LaunchInProcessAsync_ConnectionTimeoutElapses_FailsWithTimeoutMessage()
    {
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        MtpServerClientOptions options = CreateOptions();
        options.ConnectionTimeout = TimeSpan.FromMilliseconds(250);

        // Deliberately large: an abandoned launch must NOT spend the graceful shutdown timeout waiting for a
        // callback that was never connected, so this value must not show up in the elapsed time below.
        options.ServerShutdownTimeout = TimeSpan.FromMinutes(5);

        try
        {
            var stopwatch = Stopwatch.StartNew();
            MtpServerConnectionClosedException exception = await AssertThrowsAsync<MtpServerConnectionClosedException>(
                () => MtpServerClient.LaunchInProcessAsync(
                    async (_, serverToken) =>
                    {
                        // Runs happily but never dials back: only the connection timeout can end the wait.
                        // It does observe the token, so the unwind does not pay the full cancellation grace.
                        using CancellationTokenRegistration registration = serverToken.Register(() => release.TrySetResult(true));
                        await release.Task;
                        return 0;
                    },
                    options,
                    TestContext.CancellationToken));
            stopwatch.Stop();

            Assert.Contains("did not connect back within", exception.Message);
            Assert.Contains(
                "Make sure the callback forwards the supplied server-mode arguments",
                exception.Message,
                "A connection timeout must explain the most likely embedded-host integration error.");
            Assert.IsLessThan(
                TimeSpan.FromSeconds(20),
                stopwatch.Elapsed,
                $"The failed launch took {stopwatch.Elapsed.TotalSeconds:N1}s; it must be bounded by the connection timeout plus the fixed cancellation grace, not by ServerShutdownTimeout.");
        }
        finally
        {
            _ = release.TrySetResult(true);
        }
    }

    [TestMethod]
    public async Task Dispose_ClosesTransportAndAwaitsTheCallback()
    {
        using var server = new InProcessServerFixture();

        MtpServerClient client = await LaunchAsync(server);
        _ = await WithTimeoutAsync(client.InitializeAsync(TestContext.CancellationToken));
        Assert.IsFalse(server.Completion.IsCompleted, "The application must stay alive while the client is in use.");
        Assert.IsNull(client.ServerExitCode, "The exit code is only known once the application has stopped.");

        client.Dispose();

        Assert.IsTrue(
            server.Completion.IsCompleted,
            "Dispose must close the transport and wait for the hosted application before it returns.");
        Assert.AreEqual(0, await server.Completion);
        Assert.AreEqual(0, client.ServerExitCode, "The callback's exit code must be reachable after shutdown.");
    }

    [TestMethod]
    public async Task Dispose_InProcessHost_DoesNotStartTheClientFallbackShutdown()
    {
        using var server = new InProcessServerFixture();
        MtpServerClient client = await LaunchAsync(server);

        client.Dispose();

        object shutdown = typeof(MtpServerClient).GetField(
            "_shutdown",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!;
        object? fallbackTask = shutdown.GetType().GetField(
            "_task",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(shutdown);
        Assert.IsNull(fallbackTask);
    }

    [TestMethod]
    public async Task ShutdownAsync_ClosesTransportAndAwaitsTheCallback_WithoutBlocking()
    {
        using var server = new InProcessServerFixture();

        using MtpServerClient client = await LaunchAsync(server);
        _ = await WithTimeoutAsync(client.InitializeAsync(TestContext.CancellationToken));
        Assert.IsFalse(server.Completion.IsCompleted);

        await WithTimeoutAsync(client.ShutdownAsync());

        Assert.IsTrue(server.Completion.IsCompleted, "ShutdownAsync must await the hosted application.");
        Assert.AreEqual(0, client.ServerExitCode);

        // The documented pattern is ShutdownAsync inside a using block, so the trailing Dispose must be a
        // cheap no-op rather than a second teardown.
        var stopwatch = Stopwatch.StartNew();
        client.Dispose();
        stopwatch.Stop();
        Assert.IsLessThan(TimeSpan.FromSeconds(2), stopwatch.Elapsed, "Dispose after ShutdownAsync must return immediately.");
        Assert.AreEqual(1, server.CompletionCount);
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = server.ServerToken.WaitHandle);
    }

    [TestMethod]
    public async Task ShutdownCoreAsync_DisposesResourcesAndLogsTheirExactDescriptions()
    {
        var log = new List<(MtpClientLogLevel Level, string Message)>();
        var logger = new DelegateMtpClientLogger((level, message) => log.Add((level, message)));
        using var listener = new DisposableTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var client = new ThrowingTcpClient();
        var cancellation = new ThrowingCancellationTokenSource();
        using var handler = new TrackingMessageHandler();
        using var connection = new MtpJsonRpcConnection(handler, logger);
        MtpServerInProcessHost host = CreateHost(
            listener,
            client,
            connection,
            Task.FromResult(0),
            cancellation,
            TimeSpan.Zero,
            logger);

        await InvokeShutdownCoreAsync(host);

        Assert.AreEqual(1, handler.DisposeCount);
        Assert.AreEqual(1, client.DisposeCount);
        Assert.AreEqual(1, cancellation.DisposeCount);
        Assert.ContainsSingle(
            entry => entry.Level == MtpClientLogLevel.Debug
                && entry.Message.Contains("Disposing the accepted client socket threw:", StringComparison.Ordinal)
                && entry.Message.Contains("client dispose failure", StringComparison.Ordinal),
            log);
        Assert.ContainsSingle(
            entry => entry.Level == MtpClientLogLevel.Debug
                && entry.Message.Contains("Disposing the server cancellation source threw:", StringComparison.Ordinal)
                && entry.Message.Contains("cancellation dispose failure", StringComparison.Ordinal),
            log);

        using var replacement = new DisposableTcpListener(IPAddress.Loopback, port);
        replacement.Start();
    }

    [TestMethod]
    public async Task StartShutdownAsync_SkipReadLoopWait_IsHonoredWithoutReadLoopContext()
    {
        var readCompletion = new TaskCompletionSource<RpcMessage?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new TrackingMessageHandler
        {
            ReadAsyncCallback = _ => readCompletion.Task,
        };
        using var connection = new MtpJsonRpcConnection(handler);
        connection.Start();
        await WithTimeoutAsync(handler.ReadStarted);
        using var listener = new DisposableTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new TcpClient();
        using var cancellation = new CancellationTokenSource();
        MtpServerInProcessHost host = CreateHost(
            listener,
            client,
            connection,
            Task.FromResult(0),
            cancellation,
            TimeSpan.Zero,
            NullMtpClientLogger.Instance);
        MethodInfo method = typeof(MtpServerInProcessHost).GetMethod(
            "StartShutdownAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        var shutdown = (Task)method.Invoke(host, [true])!;
        bool completedWithoutReadLoop = await CompletesQuicklyAsync(shutdown);
        readCompletion.TrySetResult(null);
        await WithTimeoutAsync(shutdown);

        Assert.IsTrue(completedWithoutReadLoop, "The explicit skip flag must avoid waiting for a read loop whose context is not flowing.");
    }

    [TestMethod]
    public async Task ShutdownCoreAsync_ServerWait_DoesNotCaptureTheCallingSynchronizationContext()
    {
        var serverCompletion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new TrackingMessageHandler(() =>
            _ = Task.Run(
                async () =>
                {
                    await Task.Delay(100, TestContext.CancellationToken);
                    serverCompletion.TrySetResult(0);
                },
                TestContext.CancellationToken));
        using var connection = new MtpJsonRpcConnection(handler);
        using var listener = new DisposableTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new TcpClient();
        using var cancellation = new CancellationTokenSource();
        MtpServerInProcessHost host = CreateHost(
            listener,
            client,
            connection,
            serverCompletion.Task,
            cancellation,
            TimeSpan.FromSeconds(5),
            NullMtpClientLogger.Instance);

        Task shutdown = InvokeWithSynchronizationContext(
            () => InvokeShutdownCoreAsync(host),
            out QueueingSynchronizationContext context);
        bool completedWithoutPumping = await CompletesQuicklyAsync(shutdown);
        DrainContextUntilCompleted(context, shutdown);
        await WithTimeoutAsync(shutdown);

        Assert.IsTrue(completedWithoutPumping, "Awaiting the server shutdown task must not capture the calling synchronization context.");
    }

    [TestMethod]
    public async Task ShutdownServerAsync_CompletedServer_ReturnsTrue()
    {
        using var cancellation = new CancellationTokenSource();
        var log = new List<(MtpClientLogLevel Level, string Message)>();

        bool stopped = await InvokeShutdownServerAsync(
            Task.FromResult(0),
            cancellation,
            TimeSpan.Zero,
            new DelegateMtpClientLogger((level, message) => log.Add((level, message))));

        Assert.IsTrue(stopped);
        Assert.IsEmpty(log);
    }

    [TestMethod]
    public async Task ShutdownServerAsync_ZeroGracefulTimeout_DoesNotLogGracefulTimeout()
    {
        using var cancellation = new CancellationTokenSource();
        var log = new List<(MtpClientLogLevel Level, string Message)>();
        Task<int> serverTask = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(Timeout.Infinite, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                await Task.Delay(100, TestContext.CancellationToken);
            }

            return 0;
        });

        Task<bool> shutdown = InvokeWithSynchronizationContext(
            () => InvokeShutdownServerAsync(
                serverTask,
                cancellation,
                TimeSpan.Zero,
                new DelegateMtpClientLogger((level, message) => log.Add((level, message)))),
            out QueueingSynchronizationContext context);
        bool completedWithoutPumping = await CompletesQuicklyAsync(shutdown);
        DrainContextUntilCompleted(context, shutdown);
        bool stopped = await shutdown;

        Assert.IsTrue(stopped);
        Assert.IsTrue(completedWithoutPumping, "The cancellation-grace wait must not capture the calling synchronization context.");
        Assert.DoesNotContain(
            entry => entry.Message.Contains("requesting cancellation", StringComparison.Ordinal),
            log);
    }

    [TestMethod]
    public async Task ShutdownServerAsync_GracefulWait_DoesNotCaptureTheCallingSynchronizationContext()
    {
        using var cancellation = new CancellationTokenSource();
        var serverCompletion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = cancellation.Token.Register(() => serverCompletion.TrySetResult(0));

        Task<bool> shutdown = InvokeWithSynchronizationContext(
            () => InvokeShutdownServerAsync(
                serverCompletion.Task,
                cancellation,
                TimeSpan.FromMilliseconds(100),
                NullMtpClientLogger.Instance),
            out QueueingSynchronizationContext context);
        bool completedWithoutPumping = await CompletesQuicklyAsync(shutdown);
        DrainContextUntilCompleted(context, shutdown);

        Assert.IsTrue(await shutdown);
        Assert.IsTrue(completedWithoutPumping, "The graceful wait must not capture the calling synchronization context.");
    }

    [TestMethod]
    public async Task ShutdownServerAsync_BlockingCancellationRegistration_ReturnsFalse()
    {
        using var cancellation = new CancellationTokenSource();
        using var releaseRegistration = new ManualResetEventSlim();
        var cancellationStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = cancellation.Token.Register(() =>
        {
            cancellationStarted.TrySetResult(true);
            releaseRegistration.Wait(TestContext.CancellationToken);
        });
        Task<int> serverTask = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(Timeout.Infinite, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
            }

            return 0;
        });

        try
        {
            bool stopped = await InvokeShutdownServerAsync(
                serverTask,
                cancellation,
                TimeSpan.Zero,
                NullMtpClientLogger.Instance);

            Assert.IsFalse(stopped);
            Assert.IsTrue(await WithTimeoutAsync(cancellationStarted.Task));
        }
        finally
        {
            releaseRegistration.Set();
        }
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task ShutdownServerAsync_ObservesTheInFlightCancellationTaskWithExactDiagnostic()
    {
        FieldInfo asyncDebugging = typeof(Task).GetField(
            "s_asyncDebuggingEnabled",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        bool previousAsyncDebugging = (bool)asyncDebugging.GetValue(null)!;
        asyncDebugging.SetValue(null, true);

        using var cancellation = new CancellationTokenSource();
        using var releaseRegistration = new ManualResetEventSlim();
        var serverCompletion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationTaskId = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = cancellation.Token.Register(() =>
        {
            cancellationTaskId.TrySetResult(Task.CurrentId!.Value);
            releaseRegistration.Wait(TestContext.CancellationToken);
        });

        try
        {
            Task<bool> shutdown = InvokeShutdownServerAsync(
                serverCompletion.Task,
                cancellation,
                TimeSpan.Zero,
                NullMtpClientLogger.Instance);
            int taskId = await WithTimeoutAsync(cancellationTaskId.Task);
            Task cancelling = GetActiveTask(taskId);
            object continuation = await WaitForContinuationAsync(cancelling);

            Assert.IsTrue(
                ObjectGraphContainsString(
                    continuation,
                    "Canceling the in-process MTP application failed",
                    [with(ReferenceComparer.Instance)],
                    remainingDepth: 8),
                "The in-flight cancellation task must be observed with the exact diagnostic used for late failures.");

            serverCompletion.TrySetResult(0);
            Assert.IsFalse(await WithTimeoutAsync(shutdown));
        }
        finally
        {
            releaseRegistration.Set();
            serverCompletion.TrySetResult(0);
            asyncDebugging.SetValue(null, previousAsyncDebugging);
        }
    }

    [TestMethod]
    public async Task ShutdownServerAsync_AbandonedFault_IsObservedWithExactDiagnostic()
    {
        using var cancellation = new CancellationTokenSource();
        var serverCompletion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var failureLogged = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var logger = new DelegateMtpClientLogger((level, message) =>
        {
            if (level == MtpClientLogLevel.Error && message.Contains("abandoned failure", StringComparison.Ordinal))
            {
                failureLogged.TrySetResult(message);
            }
        });

        bool stopped = await InvokeShutdownServerAsync(
            serverCompletion.Task,
            cancellation,
            TimeSpan.Zero,
            logger);
        serverCompletion.TrySetException(new InvalidOperationException("abandoned failure"));

        string message = await WithTimeoutAsync(failureLogged.Task);
        Assert.IsFalse(stopped);
        Assert.Contains("The in-process MTP application failed:", message);
    }

    [TestMethod]
    public void SafeDispose_DisposesAndLogsSupportedFailures()
    {
        var disposable = new ThrowingDisposable();
        var log = new List<(MtpClientLogLevel Level, string Message)>();
        MethodInfo method = typeof(MtpServerInProcessHost).GetMethod(
            "SafeDispose",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        _ = method.Invoke(
            null,
            [disposable, new DelegateMtpClientLogger((level, message) => log.Add((level, message))), "Synthetic resource"]);

        Assert.AreEqual(1, disposable.DisposeCount);
        Assert.ContainsSingle(
            entry => entry.Level == MtpClientLogLevel.Debug
                && entry.Message.Contains("Synthetic resource threw:", StringComparison.Ordinal)
                && entry.Message.Contains("synthetic dispose failure", StringComparison.Ordinal),
            log);
    }

    [TestMethod]
    public async Task ShutdownAsync_PreservesNonzeroCallbackExitCode()
    {
        using var server = new InProcessServerFixture(exitCode: 42);
        using MtpServerClient client = await LaunchAsync(server);
        _ = await WithTimeoutAsync(client.InitializeAsync(TestContext.CancellationToken));

        await WithTimeoutAsync(client.ShutdownAsync());

        Assert.AreEqual(42, client.ServerExitCode, "Shutdown must preserve the exact exit code returned by the connected callback.");
    }

    [TestMethod]
    public async Task Dispose_FromANotificationHandler_DoesNotSelfWaitOnTheReadLoop()
    {
        // Teardown runs on the thread pool, so the read-loop AsyncLocal marker the connection uses to detect
        // re-entrant disposal has to survive that hop. If it did not, disposing from a handler would stall
        // for the connection's read-loop shutdown timeout while the read loop sits in this very handler.
        using var server = new InProcessServerFixture();

        MtpServerClient client = await LaunchAsync(server);
        _ = await WithTimeoutAsync(client.InitializeAsync(TestContext.CancellationToken));

        var disposeElapsed = new TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.TestNodesUpdated += (_, _) =>
        {
            var stopwatch = Stopwatch.StartNew();
            client.Dispose();
            stopwatch.Stop();
            disposeElapsed.TrySetResult(stopwatch.Elapsed);
        };

        await server.Value.SendDiscoveredTestNodeAsync(Guid.NewGuid(), "uid-1", "Test1");

        TimeSpan elapsed = await WithTimeoutAsync(disposeElapsed.Task);
        Assert.IsLessThan(
            TimeSpan.FromSeconds(4),
            elapsed,
            $"Dispose from a notification handler took {elapsed.TotalMilliseconds:F0} ms; it must not self-wait on the read loop.");
        Assert.IsTrue(server.Completion.IsCompleted, "Dispose from the handler must still complete teardown.");
        Assert.AreEqual(0, await server.Completion);
        Assert.AreEqual(0, client.ServerExitCode, "The callback exit code must still be captured after handler-triggered disposal.");
    }

    [TestMethod]
    public async Task Dispose_WhileShutdownAsyncIsInFlight_WaitsForTheSameTeardown()
    {
        using var release = new ManualResetEventSlim();
        var callbackBlocked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var disposeEntered = new ManualResetEventSlim();
        using var disposeReturned = new ManualResetEventSlim();
        using var server = new InProcessServerFixture(
            onDisconnected: () =>
            {
                callbackBlocked.TrySetResult(true);
                release.Wait(TestContext.CancellationToken);
            },
            waitForDisconnectSynchronously: true);

        MtpServerClient client = await LaunchAsync(server);
        _ = await WithTimeoutAsync(client.InitializeAsync(TestContext.CancellationToken));

        Task shutdown = client.ShutdownAsync();
        Task? dispose = null;
        try
        {
            // ShutdownAsync schedules teardown on the thread pool. Awaiting this rendezvous leaves the test's
            // worker available to run it, which is important for net462 runners with a constrained worker pool.
            await WithTimeoutAsync(callbackBlocked.Task);
            Assert.IsFalse(shutdown.IsCompleted, "ShutdownAsync must remain incomplete while the callback is blocked.");

            // A Dispose that races an in-flight ShutdownAsync must join it, not report success while the
            // application is still stopping. Keep the probe off the thread pool: under net462 contention an
            // awaited probe can resume only after the bounded teardown abandons the callback.
            dispose = Task.Factory.StartNew(
                () =>
                {
                    disposeEntered.Set();
                    client.Dispose();
                    disposeReturned.Set();
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            Assert.IsTrue(
                disposeEntered.Wait(TimeSpan.FromSeconds(5), TestContext.CancellationToken),
                "The dedicated Dispose thread must start.");
            Assert.IsFalse(
                disposeReturned.Wait(TimeSpan.FromMilliseconds(100), TestContext.CancellationToken),
                "Dispose must not return while the shared teardown is still running.");
        }
        finally
        {
            release.Set();
            await WithTimeoutAsync(shutdown);
            if (dispose is not null)
            {
                await WithTimeoutAsync(dispose);
            }
        }

        Assert.AreEqual(1, server.CompletionCount, "Both entry points must share one teardown.");
    }

    [TestMethod]
    public async Task Dispose_IsIdempotent()
    {
        using var server = new InProcessServerFixture();

        MtpServerClient client = await LaunchAsync(server);
        _ = await WithTimeoutAsync(client.InitializeAsync(TestContext.CancellationToken));

        client.Dispose();
        int afterFirst = server.TransportClosedCount;
        var stopwatch = Stopwatch.StartNew();
        client.Dispose();
        client.Dispose();
        stopwatch.Stop();

        Assert.AreEqual(1, afterFirst, "The first Dispose must close the transport exactly once.");
        Assert.AreEqual(1, server.TransportClosedCount, "Repeated Dispose must not tear the application down again.");
        Assert.AreEqual(1, server.CompletionCount, "The hosted application must be torn down exactly once.");
        Assert.IsLessThan(
            TimeSpan.FromSeconds(2),
            stopwatch.Elapsed,
            "A repeated Dispose joins the already-completed teardown, so it must return immediately.");
    }

    [TestMethod]
    public async Task Dispose_CallbackFaultsDuringShutdown_DoesNotThrow()
    {
        using var server = new InProcessServerFixture(
            onDisconnected: () => throw new InvalidOperationException("The application failed while shutting down."));

        MtpServerClient client = await LaunchAsync(server);
        _ = await WithTimeoutAsync(client.InitializeAsync(TestContext.CancellationToken));

        // A shutdown failure is observed and logged, never rethrown: Dispose runs on the unwind path of the
        // caller's own failure and must not replace it.
        client.Dispose();

        Assert.IsTrue(server.Completion.IsFaulted);
        InvalidOperationException faultException = Assert.IsInstanceOfType<InvalidOperationException>(
            server.Completion.Exception?.GetBaseException());
        Assert.AreEqual("The application failed while shutting down.", faultException.Message);
        Assert.Contains(
            "The in-process MTP application failed during disposal",
            server.Log,
            $"The shutdown failure must be reported through the client logger. Log:{Environment.NewLine}{server.Log}");
        Assert.Contains(
            "The application failed while shutting down.",
            server.Log,
            "The disposal diagnostic must retain the callback failure details.");
    }

    [TestMethod]
    public async Task ShutdownAsync_CallbackFaultsDuringShutdown_PropagatesCallbackException()
    {
        var callbackFailure = new InvalidOperationException("The application failed while shutting down.");
        using var server = new InProcessServerFixture(onDisconnected: () => throw callbackFailure);
        using MtpServerClient client = await LaunchAsync(server);
        _ = await WithTimeoutAsync(client.InitializeAsync(TestContext.CancellationToken));

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => WithTimeoutAsync(client.ShutdownAsync()));

        Assert.AreSame(callbackFailure, exception, "ShutdownAsync must preserve the callback's original post-connect failure.");
    }

    [TestMethod]
    public async Task ShutdownAsync_CallbackCancelsItselfAfterConnecting_PropagatesCancellation()
    {
        using var callbackCancellation = new CancellationTokenSource();
        var connected = new TaskCompletionSource<FakeMtpServer>(TaskCreationOptions.RunContinuationsAsynchronously);
        using MtpServerClient client = await MtpServerClient.LaunchInProcessAsync(
            async (arguments, _) =>
            {
                using FakeMtpServer server = ConnectBack(arguments);
                connected.TrySetResult(server);
                await Task.Delay(Timeout.Infinite, callbackCancellation.Token);
                return 0;
            },
            CreateOptions(),
            TestContext.CancellationToken);

        _ = await WithTimeoutAsync(connected.Task);
        _ = await WithTimeoutAsync(client.InitializeAsync(TestContext.CancellationToken));
        callbackCancellation.Cancel();

        TaskCanceledException exception = await Assert.ThrowsExactlyAsync<TaskCanceledException>(
            () => WithTimeoutAsync(client.ShutdownAsync()));

        Assert.AreEqual(callbackCancellation.Token, exception.CancellationToken);
    }

    [TestMethod]
    public async Task ShutdownAsync_CallbackHonorsTeardownCancellationThroughLinkedToken_DoesNotThrow()
    {
        var connected = new TaskCompletionSource<FakeMtpServer>(TaskCreationOptions.RunContinuationsAsynchronously);
        MtpServerClientOptions options = CreateOptions();
        options.ServerShutdownTimeout = TimeSpan.FromMilliseconds(100);
        using MtpServerClient client = await MtpServerClient.LaunchInProcessAsync(
            async (arguments, serverToken) =>
            {
                using FakeMtpServer server = ConnectBack(arguments);
                using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(serverToken);
                connected.TrySetResult(server);
                await Task.Delay(Timeout.Infinite, linkedCancellation.Token);
                return 0;
            },
            options,
            TestContext.CancellationToken);

        _ = await WithTimeoutAsync(connected.Task);
        _ = await WithTimeoutAsync(client.InitializeAsync(TestContext.CancellationToken));

        await WithTimeoutAsync(client.ShutdownAsync());

        Assert.IsNull(client.ServerExitCode, "A callback canceled by teardown did not return an application exit code.");
    }

    [TestMethod]
    [DoNotParallelize] // Measures overlapping timeout paths; thread-pool contention would measure unrelated tests instead.
    public async Task Dispose_BlockedHandlersAndCallback_ReturnsWithinTheDocumentedBound()
    {
        var neverCompletes = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlerEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHandler = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationHandlerEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCancellationHandler = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var connected = new TaskCompletionSource<FakeMtpServer>(TaskCreationOptions.RunContinuationsAsynchronously);
        MtpServerClientOptions options = CreateOptions();
        options.ServerShutdownTimeout = TimeSpan.FromSeconds(5);
        var log = new StringBuilder();
        options.Logger = new DelegateMtpClientLogger((_, message) =>
        {
            lock (log)
            {
                log.AppendLine(message);
            }
        });

        try
        {
            using MtpServerClient client = await MtpServerClient.LaunchInProcessAsync(
                async (arguments, serverToken) =>
                {
                    using FakeMtpServer server = ConnectBack(arguments);
                    connected.TrySetResult(server);
                    using CancellationTokenRegistration registration = serverToken.Register(() =>
                    {
                        cancellationHandlerEntered.TrySetResult(true);
                        releaseCancellationHandler.Task.GetAwaiter().GetResult();
                    });

                    // Deliberately ignores both the closed transport and the cancellation token.
                    await neverCompletes.Task;
                    return 0;
                },
                options,
                TestContext.CancellationToken);

            _ = await WithTimeoutAsync(client.InitializeAsync(TestContext.CancellationToken));

            client.TestNodesUpdated += (_, _) =>
            {
                handlerEntered.TrySetResult(true);
                releaseHandler.Task.GetAwaiter().GetResult();
            };

            FakeMtpServer connectedServer = await WithTimeoutAsync(connected.Task);
            await connectedServer.SendDiscoveredTestNodeAsync(Guid.NewGuid(), "uid-1", "Test1");
            await WithTimeoutAsync(handlerEntered.Task);

            var stopwatch = Stopwatch.StartNew();
            client.Dispose();
            stopwatch.Stop();

            // Matching ServerShutdownTimeout to the connection's fixed 5s read-loop wait avoids depending on a
            // shorter timer continuation getting a worker before a blocked handler releases one on net462. The
            // fixed 5s cancellation grace makes the concurrent path take about 10s; serial waits take at least 15s.
            Assert.IsLessThan(
                TimeSpan.FromSeconds(13),
                stopwatch.Elapsed,
                $"Dispose took {stopwatch.Elapsed.TotalSeconds:N1}s; it must abandon an unresponsive application within the documented bound rather than block indefinitely.");

            Assert.IsTrue(
                await WithTimeoutAsync(cancellationHandlerEntered.Task),
                "Shutdown must request cancellation without waiting inline for a blocking callback registration.");

            string text;
            lock (log)
            {
                text = log.ToString();
            }

            Assert.Contains("abandoning it", text, $"Abandoning the application must be reported. Log:{Environment.NewLine}{text}");
            Assert.Contains(
                "requesting cancellation",
                text,
                $"Escalating from graceful shutdown to cancellation must be reported. Log:{Environment.NewLine}{text}");
        }
        finally
        {
            _ = releaseHandler.TrySetResult(true);
            _ = releaseCancellationHandler.TrySetResult(true);
            _ = neverCompletes.TrySetResult(true);
        }
    }

    [TestMethod]
    public async Task RunTestsAsync_Canceled_SendsCancelRequestToTheHostedApplication()
    {
        using var server = new InProcessServerFixture();

        using MtpServerClient client = await LaunchAsync(server);
        server.Value.WithholdRunResponse = true;
        _ = await WithTimeoutAsync(client.InitializeAsync(TestContext.CancellationToken));

        using var cancellation = new CancellationTokenSource();
        Task<MtpRunResult> run = client.RunTestsAsync(cancellation.Token);
        _ = await WithTimeoutAsync(server.Value.WaitForRequestAsync(JsonRpcMethods.TestingRunTests, DefaultTimeout));

        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => WithTimeoutAsync(run));
        await WithTimeoutAsync(server.Value.WaitForNotificationAsync(JsonRpcMethods.CancelRequest, DefaultTimeout));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task LaunchInProcessAsync_HonorsTheStatefulOption(bool isStateful)
    {
        using var server = new InProcessServerFixture();
        MtpServerClientOptions options = CreateOptions();
        options.IsStateful = isStateful;
        options.ClientName = "EmbeddedHost";

        using MtpServerClient client = await LaunchAsync(server, options);
        MtpServerCapabilities capabilities = await WithTimeoutAsync(client.InitializeAsync(TestContext.CancellationToken));

        RequestMessage initialize = server.Value.ReceivedRequests.Single(request => request.Method == JsonRpcMethods.Initialize);
        InitializeRequestArgs args = initialize.Params is InitializeRequestArgs typed
            ? typed
            : SerializerUtilities.Deserialize<InitializeRequestArgs>((IDictionary<string, object?>)initialize.Params!);

        Assert.AreEqual(isStateful, args.Capabilities.IsStateful, "The in-process path must forward the client's stateful capability unchanged.");
        Assert.AreSame(capabilities, client.Capabilities, "The client must retain the capabilities negotiated with the in-process server.");
        Assert.AreEqual("EmbeddedHost", args.ClientInfo.Name);
        Assert.AreEqual(CurrentProcessId, args.ProcessId);
    }

    [TestMethod]
    public async Task LaunchInProcessAsync_MultipleRequestsOnOneSession_ReuseTheSameConnection()
    {
        using var server = new InProcessServerFixture();
        MtpServerClientOptions options = CreateOptions();
        options.IsStateful = true;

        using MtpServerClient client = await LaunchAsync(server, options);
        MtpServerCapabilities capabilities = await WithTimeoutAsync(client.InitializeAsync(TestContext.CancellationToken));
        Assert.IsTrue(capabilities.MultiRequestSupport, "The fake server negotiates keep-alive back.");

        await WithTimeoutAsync(client.DiscoverTestsAsync(TestContext.CancellationToken));
        _ = await WithTimeoutAsync(client.RunTestsAsync(TestContext.CancellationToken));
        _ = await WithTimeoutAsync(client.RunTestsWithFilterAsync("/*/*/*/*", TestContext.CancellationToken));

        Assert.AreEqual(
            1,
            server.ConnectionCount,
            "A stateful session must serve every request over the single connection the launch established.");
        Assert.AreSequenceEqual(
            [
                JsonRpcMethods.Initialize,
                JsonRpcMethods.TestingDiscoverTests,
                JsonRpcMethods.TestingRunTests,
                JsonRpcMethods.TestingRunTests,
            ],
            server.Value.ReceivedRequestMethods,
            "The stateful session must reuse one connection for the expected initialize, discover, and run requests.");
    }

    [TestMethod]
    public async Task LaunchInProcessAsync_IgnoresEnvironmentVariablesAndWarns()
    {
        using var server = new InProcessServerFixture();
        MtpServerClientOptions options = CreateOptions();
        options.EnvironmentVariables["EXAMPLE"] = "1";

        using MtpServerClient client = await LaunchAsync(server, options);

        Assert.Contains(
            nameof(MtpServerClientOptions.EnvironmentVariables),
            server.Log,
            "An in-process application shares the caller's environment, so silently dropping the variables would be a trap.");
        Assert.Contains(
            "application is hosted in process: it shares the caller's environment",
            server.Log,
            "The warning must explain why the configured variables are ignored.");
    }

    [TestMethod]
    public async Task LaunchInProcessAsync_WithNoEnvironmentVariables_DoesNotWarnAboutIgnoredVariables()
    {
        using var server = new InProcessServerFixture();

        using MtpServerClient client = await LaunchAsync(server);

        Assert.DoesNotContain(
            nameof(MtpServerClientOptions.EnvironmentVariables),
            server.Log,
            "The client must not emit an ignored-environment warning when no variables were configured.");
    }

    private async Task<MtpServerClient> LaunchAsync(InProcessServerFixture server, MtpServerClientOptions? options = null)
    {
        options ??= CreateOptions();

        // Route the client's own diagnostics into the fixture so a test can assert on them.
        options.Logger ??= new DelegateMtpClientLogger((_, message) => server.Append(message));

        MtpServerClient client = await WithTimeoutAsync(MtpServerClient.LaunchInProcessAsync(
            server.RunAsync,
            options,
            TestContext.CancellationToken));

        try
        {
            // The client's accept can complete while the callback is still inside its own connect call, so
            // wait for the callback to publish its server before any test touches it.
            _ = await WithTimeoutAsync(server.Connected);
        }
        catch
        {
            client.Dispose();
            throw;
        }

        return client;
    }

    private static MtpServerClientOptions CreateOptions()
        => new()
        {
            // Keep every negative test bounded well below the suite timeout.
            ConnectionTimeout = TimeSpan.FromSeconds(20),
            ServerShutdownTimeout = TimeSpan.FromSeconds(10),
        };

    private static FakeMtpServer ConnectBack(string[] serverArguments)
    {
        string host = ReadArgument(serverArguments, "--client-host");
        int port = int.Parse(ReadArgument(serverArguments, "--client-port"), CultureInfo.InvariantCulture);
        return FakeMtpServer.ConnectBackTo(host, port);
    }

    private static string ReadArgument(string[] serverArguments, string name)
    {
        int index = Array.IndexOf(serverArguments, name);
        return index >= 0 && index + 1 < serverArguments.Length
            ? serverArguments[index + 1]
            : throw new InvalidOperationException($"The client did not pass '{name}'. Arguments: {string.Join(" ", serverArguments)}");
    }

    private static int GetCurrentProcessId()
    {
        using var current = Process.GetCurrentProcess();
        return current.Id;
    }

    private static async Task<T> WithTimeoutAsync<T>(Task<T> task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(DefaultTimeout)).ConfigureAwait(false);
        return completed != task
            ? throw new TimeoutException("Timed out waiting for the operation to complete.")
            : await task.ConfigureAwait(false);
    }

    private static async Task WithTimeoutAsync(Task task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(DefaultTimeout)).ConfigureAwait(false);
        if (completed != task)
        {
            throw new TimeoutException("Timed out waiting for the operation to complete.");
        }

        await task.ConfigureAwait(false);
    }

    private static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
        => await Assert.ThrowsExactlyAsync<TException>(() => WithTimeoutAsync(action()));

    private static async Task<T> AssertCompletesWithoutPumpingAsync<T>(Func<Task<T>> action)
    {
        Task<T> task = InvokeWithSynchronizationContext(action, out QueueingSynchronizationContext context);
        bool completedWithoutPumping = await CompletesQuicklyAsync(task);
        DrainContextUntilCompleted(context, task);

        T result = await task.ConfigureAwait(false);
        Assert.IsTrue(
            completedWithoutPumping,
            $"The operation captured the calling synchronization context and posted {context.PostCount} continuation(s).");
        return result;
    }

    private static Task<T> InvokeWithSynchronizationContext<T>(
        Func<Task<T>> action,
        out QueueingSynchronizationContext context)
    {
        context = new QueueingSynchronizationContext();
        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            return action();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private static Task InvokeWithSynchronizationContext(
        Func<Task> action,
        out QueueingSynchronizationContext context)
    {
        context = new QueueingSynchronizationContext();
        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            return action();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private static async Task<bool> CompletesQuicklyAsync(Task task)
        => await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(1))).ConfigureAwait(false) == task;

    private static void DrainContextUntilCompleted(QueueingSynchronizationContext context, Task task)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!task.IsCompleted && stopwatch.Elapsed < DefaultTimeout)
        {
            if (!context.TryRunOne())
            {
                Thread.Sleep(10);
            }
        }
    }

    private static MtpServerInProcessHost CreateHost(
        TcpListener listener,
        TcpClient client,
        MtpJsonRpcConnection connection,
        Task<int> serverTask,
        CancellationTokenSource cancellation,
        TimeSpan shutdownTimeout,
        IMtpClientLogger logger)
        => (MtpServerInProcessHost)typeof(MtpServerInProcessHost).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(TcpListener), typeof(TcpClient), typeof(MtpJsonRpcConnection), typeof(Task<int>),
             typeof(CancellationTokenSource), typeof(TimeSpan), typeof(IMtpClientLogger)],
            modifiers: null)!.Invoke(
            [listener, client, connection, serverTask, cancellation, shutdownTimeout, logger]);

    private static Task InvokeShutdownCoreAsync(MtpServerInProcessHost host)
        => (Task)typeof(MtpServerInProcessHost).GetMethod(
            "ShutdownCoreAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(host, null)!;

    private static Task<bool> InvokeShutdownServerAsync(
        Task<int> serverTask,
        CancellationTokenSource cancellation,
        TimeSpan gracefulTimeout,
        IMtpClientLogger logger)
        => (Task<bool>)typeof(MtpServerInProcessHost).GetMethod(
            "ShutdownServerAsync",
            BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(
            null,
            [serverTask, cancellation, gracefulTimeout, logger])!;

    private static Task GetActiveTask(int taskId)
        => (Task)typeof(Task).GetMethod(
            "GetActiveTaskFromId",
            BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [taskId])!;

    private static async Task<object> WaitForContinuationAsync(Task task)
    {
        FieldInfo continuationField = typeof(Task).GetField(
            "m_continuationObject",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(5))
        {
            if (continuationField.GetValue(task) is { } continuation)
            {
                return continuation;
            }

            await Task.Delay(10).ConfigureAwait(false);
        }

        throw new TimeoutException("The cancellation task was not observed.");
    }

    private static bool ObjectGraphContainsString(
        object? value,
        string expected,
        HashSet<object> visited,
        int remainingDepth)
    {
        if (value is null || remainingDepth < 0)
        {
            return false;
        }

        if (value is string text)
        {
            return text.Contains(expected, StringComparison.Ordinal);
        }

        Type type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || !visited.Add(value))
        {
            return false;
        }

        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            object? fieldValue;
            try
            {
                fieldValue = field.GetValue(value);
            }
            catch (Exception)
            {
                continue;
            }

            if (ObjectGraphContainsString(fieldValue, expected, visited, remainingDepth - 1))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class DisposableTcpListener(IPAddress localaddr, int port) : TcpListener(localaddr, port), IDisposable
    {
        void IDisposable.Dispose()
            => Stop();
    }

    /// <summary>
    /// Plays the part of the hosted MTP application: it reads the client-generated arguments, dials back to
    /// the client's listener, serves the protocol until the client closes the connection, and then completes
    /// like a real <c>TestApplication.RunAsync</c> would.
    /// </summary>
    private sealed class InProcessServerFixture : IDisposable
    {
        private readonly Action? _onDisconnected;
        private readonly int _exitCode;
        private readonly bool _waitForDisconnectSynchronously;
        private readonly TaskCompletionSource<int> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<FakeMtpServer> _connected = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly StringBuilder _log = new();

        private FakeMtpServer? _server;
        private int _connectionCount;
        private int _completionCount;
        private int _transportClosedCount;

        public InProcessServerFixture(
            Action? onDisconnected = null,
            int exitCode = 0,
            bool waitForDisconnectSynchronously = false)
        {
            _onDisconnected = onDisconnected;
            _exitCode = exitCode;
            _waitForDisconnectSynchronously = waitForDisconnectSynchronously;
        }

        /// <summary>Gets the argument array the client generated for the callback (empty before it ran).</summary>
        public string[] Arguments { get; private set; } = [];

        /// <summary>Gets the served fake server. Valid once the launch has completed.</summary>
        public FakeMtpServer Value => _server ?? throw new InvalidOperationException("The callback has not connected yet.");

        /// <summary>
        /// Completes once the callback has published its server. The client's accept can complete while the
        /// callback is still inside its own connect call, so a test must await this before touching
        /// <see cref="Value"/> rather than assuming the launch returning means the callback finished setting
        /// itself up.
        /// </summary>
        public Task<FakeMtpServer> Connected => _connected.Task;

        /// <summary>Gets the task that mirrors the hosted application's lifetime.</summary>
        public Task<int> Completion => _completion.Task;

        public CancellationToken ServerToken { get; private set; }

        /// <summary>Gets how many times the callback dialed back to the client.</summary>
        public int ConnectionCount => Volatile.Read(ref _connectionCount);

        /// <summary>Gets how many times the hosted application ran to completion.</summary>
        public int CompletionCount => Volatile.Read(ref _completionCount);

        /// <summary>
        /// Gets how many times the client closed the transport. Teardown is observable from the server side,
        /// so a repeated Dispose that skipped its guard would show up here as a second close.
        /// </summary>
        public int TransportClosedCount => Volatile.Read(ref _transportClosedCount);

        /// <summary>Gets the diagnostics the client emitted through its logger.</summary>
        public string Log
        {
            get
            {
                lock (_log)
                {
                    return _log.ToString();
                }
            }
        }

        public void Append(string message)
        {
            lock (_log)
            {
                _log.AppendLine(message);
            }
        }

        public async Task<int> RunAsync(string[] serverArguments, CancellationToken cancellationToken)
        {
            Arguments = serverArguments;
            ServerToken = cancellationToken;
            FakeMtpServer server = ConnectBack(serverArguments);
            _server = server;
            _connected.TrySetResult(server);
            _ = Interlocked.Increment(ref _connectionCount);

            try
            {
                // A real server-mode application runs until its session ends, which is what closing the
                // client's end of the transport produces.
                if (_waitForDisconnectSynchronously)
                {
                    // This test-only path keeps the post-disconnect callback on the host's dedicated invocation
                    // instead of depending on a thread-pool continuation while testing thread-pool contention.
                    server.Disconnected.GetAwaiter().GetResult();
                }
                else
                {
                    await server.Disconnected.ConfigureAwait(false);
                }

                _ = Interlocked.Increment(ref _transportClosedCount);
                _onDisconnected?.Invoke();
                _ = Interlocked.Increment(ref _completionCount);
                _ = _completion.TrySetResult(_exitCode);
                return _exitCode;
            }
            catch (Exception exception)
            {
                _ = Interlocked.Increment(ref _completionCount);
                _ = _completion.TrySetException(exception);
                throw;
            }
        }

        public void Dispose()
        {
            _server?.Dispose();
            _ = _completion.TrySetResult(0);
            _ = _connected.TrySetCanceled();
        }
    }

    private sealed class QueueingSynchronizationContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _callbacks = new();
        private readonly object _lock = new();
        private int _postCount;

        public int PostCount => Volatile.Read(ref _postCount);

        public override void Post(SendOrPostCallback d, object? state)
        {
            _ = Interlocked.Increment(ref _postCount);
            lock (_lock)
            {
                _callbacks.Enqueue((d, state));
            }
        }

        public bool TryRunOne()
        {
            (SendOrPostCallback Callback, object? State) work;
            lock (_lock)
            {
                if (_callbacks.Count == 0)
                {
                    return false;
                }

                work = _callbacks.Dequeue();
            }

            SynchronizationContext? previous = Current;
            SetSynchronizationContext(this);
            try
            {
                work.Callback(work.State);
            }
            finally
            {
                SetSynchronizationContext(previous);
            }

            return true;
        }
    }

    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        public static ReferenceComparer Instance { get; } = new();

        public new bool Equals(object? x, object? y)
            => ReferenceEquals(x, y);

        public int GetHashCode(object obj)
            => RuntimeHelpers.GetHashCode(obj);
    }

    private sealed class TrackingMessageHandler : IMessageHandler, IDisposable
    {
        private readonly Action? _onDispose;
        private readonly TaskCompletionSource<bool> _readStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _disposeCount;

        public TrackingMessageHandler(Action? onDispose = null)
            => _onDispose = onDispose;

        public Func<CancellationToken, Task<RpcMessage?>>? ReadAsyncCallback { get; init; }

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public Task ReadStarted => _readStarted.Task;

        public Task<RpcMessage?> ReadAsync(CancellationToken cancellationToken)
        {
            _readStarted.TrySetResult(true);
            return ReadAsyncCallback?.Invoke(cancellationToken)
                ?? Task.Delay(Timeout.Infinite, cancellationToken).ContinueWith<RpcMessage?>(
                _ => null,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        public Task WriteRequestAsync(RpcMessage message, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public void Dispose()
        {
            _ = Interlocked.Increment(ref _disposeCount);
            _onDispose?.Invoke();
        }
    }

    private sealed class ThrowingTcpClient : TcpClient
    {
        private int _disposeCount;

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _ = Interlocked.Increment(ref _disposeCount);
            throw new IOException("client dispose failure");
        }
    }

    private sealed class ThrowingCancellationTokenSource : CancellationTokenSource
    {
        private int _disposeCount;

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _ = Interlocked.Increment(ref _disposeCount);
            throw new ObjectDisposedException(nameof(ThrowingCancellationTokenSource), "cancellation dispose failure");
        }
    }

    private sealed class ThrowingDisposable : IDisposable
    {
        private int _disposeCount;

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public void Dispose()
        {
            _ = Interlocked.Increment(ref _disposeCount);
            throw new IOException("synthetic dispose failure");
        }
    }
}
