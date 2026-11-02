// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class IPCTests
{
    private readonly TestContext _testContext;

    public IPCTests(TestContext testContext)
        => _testContext = testContext;

    [TestMethod]
    public void ResolvePipeDirectory_WhenOverrideProvided_UsesOverrideAsExplicit()
    {
        (string directory, bool isExplicitOverride) = NamedPipeServer.ResolvePipeDirectory("/custom/pipe/dir", "/tmp");

        Assert.AreEqual("/custom/pipe/dir", directory);
        Assert.IsTrue(isExplicitOverride);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void ResolvePipeDirectory_WhenOverrideMissing_FallsBackToTempPath(string? overrideDirectory)
    {
        (string directory, bool isExplicitOverride) = NamedPipeServer.ResolvePipeDirectory(overrideDirectory, "/var/folders/tmp");

        Assert.AreEqual("/var/folders/tmp", directory);
        Assert.IsFalse(isExplicitOverride);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void ResolvePipeDirectory_WhenNeitherOverrideNorTempPathAvailable_FallsBackToTmp(string? tempPath)
    {
        (string directory, bool isExplicitOverride) = NamedPipeServer.ResolvePipeDirectory(overrideDirectory: null, tempPath);

        Assert.AreEqual("/tmp", directory);
        Assert.IsFalse(isExplicitOverride);
    }

    [TestMethod]
    public void EnsureDirectoryIsWritable_WhenDirectoryMissingButCreatable_CreatesItAndDoesNotThrow()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"mtp.pipedir.{Guid.NewGuid():N}");
        try
        {
            NamedPipeServer.EnsureDirectoryIsWritable(directory);

            Assert.IsTrue(Directory.Exists(directory));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void EnsureDirectoryIsWritable_WhenDirectoryCannotBeCreated_ThrowsInvalidOperationException()
    {
        // Create a regular file, then target a directory *beneath* that file. Directory.CreateDirectory cannot
        // create a directory under an existing file, so this deterministically fails on Windows and Unix.
        string filePath = Path.Combine(Path.GetTempPath(), $"mtp.pipefile.{Guid.NewGuid():N}");
        File.WriteAllText(filePath, string.Empty);
        try
        {
            string invalidDirectory = Path.Combine(filePath, "sub");

            Assert.ThrowsExactly<InvalidOperationException>(() => NamedPipeServer.EnsureDirectoryIsWritable(invalidDirectory));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [TestMethod]
    public void EnsurePathLengthWithinLimit_WhenWithinLimit_DoesNotThrow()
    {
        string path = "/tmp/" + new string('a', NamedPipeServer.MaxUnixDomainSocketPathLengthInBytes - "/tmp/".Length);

        // Should not throw.
        NamedPipeServer.EnsurePathLengthWithinLimit(path);
    }

    [TestMethod]
    public void EnsurePathLengthWithinLimit_WhenExceedsLimit_Throws()
    {
        string path = "/tmp/" + new string('a', NamedPipeServer.MaxUnixDomainSocketPathLengthInBytes);

        Assert.ThrowsExactly<InvalidOperationException>(() => NamedPipeServer.EnsurePathLengthWithinLimit(path));
    }

    [TestMethod]
    public void EnsurePathLengthWithinLimit_WhenMultibyteExceedsByteLimitButNotCharLimit_Throws()
    {
        // '好' (U+597D) is 3 bytes in UTF-8. 40 of them plus the "/tmp/" prefix is only 45 characters -
        // comfortably under the 103 limit if it were (incorrectly) measured in characters - but 125 bytes,
        // which exceeds it. This proves the guard measures UTF-8 bytes (matching sun_path) and not characters.
        string path = "/tmp/" + new string('好', 40);

        Assert.IsLessThan(NamedPipeServer.MaxUnixDomainSocketPathLengthInBytes, path.Length, "Test setup: character count must stay under the limit so only a byte-based check can fail.");
        Assert.IsGreaterThan(NamedPipeServer.MaxUnixDomainSocketPathLengthInBytes, System.Text.Encoding.UTF8.GetByteCount(path), "Test setup: UTF-8 byte count must exceed the limit.");

        Assert.ThrowsExactly<InvalidOperationException>(() => NamedPipeServer.EnsurePathLengthWithinLimit(path));
    }

    [TestMethod]
    public async Task WaitForDisconnectAsync_WhenNeverConnected_ReturnsTrueBeforeAndAfterDisposal()
    {
        using NamedPipeServer server = new(
            NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N")),
            _ => throw new UnreachableException(),
            new SystemEnvironment(),
            new Mock<ILogger>().Object,
            new SystemTask(),
            _testContext.CancellationToken);

        Assert.IsFalse(server.WasConnected);
        Assert.IsTrue(await server.WaitForDisconnectAsync(TimeSpan.Zero));

        server.Dispose();

        Assert.IsFalse(server.WasConnected);
        Assert.IsTrue(await server.WaitForDisconnectAsync(TimeSpan.Zero));
    }

    [TestMethod]
    public async Task SingleConnectionNamedPipeServer_MultipleConnection_Fails()
    {
        PipeNameDescription pipeNameDescription = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));

        List<NamedPipeServer> openedPipe = [];
        List<Exception> exceptions = [];

        ManualResetEventSlim waitException = new(false);
        var waitTask = Task.Run(
            async () =>
            {
                try
                {
                    while (true)
                    {
                        NamedPipeServer singleConnectionNamedPipeServer = new(
                            pipeNameDescription,
                            async _ => await Task.FromResult(VoidResponse.CachedInstance),
                            new SystemEnvironment(),
                            new Mock<ILogger>().Object,
                            new SystemTask(),
                            _testContext.CancellationToken);

                        await singleConnectionNamedPipeServer.WaitConnectionAsync(_testContext.CancellationToken);
                        openedPipe.Add(singleConnectionNamedPipeServer);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    waitException.Set();
                }
            }, _testContext.CancellationToken);

        NamedPipeClient namedPipeClient1 = new(pipeNameDescription.Name);
        await namedPipeClient1.ConnectAsync(_testContext.CancellationToken);
        waitException.Wait(_testContext.CancellationToken);

        Assert.HasCount(1, openedPipe);
        Assert.HasCount(1, exceptions);
        Assert.AreEqual(typeof(IOException), exceptions[0].GetType());
        Assert.Contains("All pipe instances are busy.", exceptions[0].Message);

        await waitTask;
#if NETCOREAPP
        namedPipeClient1.Dispose();
        await openedPipe[0].DisposeAsync();
#else
        namedPipeClient1.Dispose();
        openedPipe[0].Dispose();
#endif

        // Verify double dispose
#if NETCOREAPP
        namedPipeClient1.Dispose();
        await openedPipe[0].DisposeAsync();
#else
        namedPipeClient1.Dispose();
        openedPipe[0].Dispose();
#endif
    }

    [TestMethod]
    public async Task SingleConnectionNamedPipeServer_RequestReplySerialization_Succeeded()
    {
        Queue<object> receivedMessages = new();
        PipeNameDescription pipeNameDescription = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));
        NamedPipeClient namedPipeClient = new(pipeNameDescription.Name);
        namedPipeClient.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
        namedPipeClient.RegisterSerializer(new TextMessageSerializer(), typeof(TextMessage));
        namedPipeClient.RegisterSerializer(new IntMessageSerializer(), typeof(IntMessage));
        namedPipeClient.RegisterSerializer(new LongMessageSerializer(), typeof(LongMessage));
        namedPipeClient.RegisterSerializer(new TestHostCompletedRequestSerializer(), typeof(TestHostCompletedRequest));

        ManualResetEventSlim manualResetEventSlim = new(false);
        var clientConnected = Task.Run(
            async () =>
            {
                while (true)
                {
                    try
                    {
                        await namedPipeClient.ConnectAsync(CancellationToken.None);
                        manualResetEventSlim.Set();
                        break;
                    }
                    catch (OperationCanceledException ct) when (ct.CancellationToken == _testContext.CancellationToken)
                    {
                        throw new OperationCanceledException("SingleConnectionNamedPipeServer_RequestReplySerialization_Succeeded cancellation during connect, testContext.CancellationTokenSource.Token");
                    }
                    catch (OperationCanceledException)
                    {
                        throw new OperationCanceledException("SingleConnectionNamedPipeServer_RequestReplySerialization_Succeeded cancellation during connect");
                    }
                    catch (Exception)
                    {
                    }
                }
            }, _testContext.CancellationToken);
        NamedPipeServer singleConnectionNamedPipeServer = new(
            pipeNameDescription,
            request =>
            {
                receivedMessages.Enqueue(request);
                return Task.FromResult<IResponse>(VoidResponse.CachedInstance);
            },
            new SystemEnvironment(),
            new Mock<ILogger>().Object,
            new SystemTask(),
            CancellationToken.None);
        singleConnectionNamedPipeServer.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
        singleConnectionNamedPipeServer.RegisterSerializer(new TextMessageSerializer(), typeof(TextMessage));
        singleConnectionNamedPipeServer.RegisterSerializer(new IntMessageSerializer(), typeof(IntMessage));
        singleConnectionNamedPipeServer.RegisterSerializer(new LongMessageSerializer(), typeof(LongMessage));
        singleConnectionNamedPipeServer.RegisterSerializer(new LegacyTestHostCompletedRequestSerializer(), typeof(TestHostCompletedRequest));
        await singleConnectionNamedPipeServer.WaitConnectionAsync(CancellationToken.None);
        manualResetEventSlim.Wait(_testContext.CancellationToken);

        await clientConnected.WithCancellationAsync(CancellationToken.None);

        await namedPipeClient.RequestReplyAsync<IntMessage, VoidResponse>(new IntMessage(10), CancellationToken.None);
        Assert.AreEqual(receivedMessages.Dequeue(), new IntMessage(10));

        await namedPipeClient.RequestReplyAsync<LongMessage, VoidResponse>(new LongMessage(11), CancellationToken.None);
        Assert.AreEqual(receivedMessages.Dequeue(), new LongMessage(11));

        await namedPipeClient.RequestReplyAsync<TestHostCompletedRequest, VoidResponse>(
            new TestHostCompletedRequest((int)ExitCode.AtLeastOneTestFailed, (int)ExitCode.CoverageThresholdFailed),
            CancellationToken.None);
        var legacyCompletion = (TestHostCompletedRequest)receivedMessages.Dequeue();
        Assert.AreEqual((int)ExitCode.AtLeastOneTestFailed, legacyCompletion.ExitCode);
        Assert.AreEqual(legacyCompletion.ExitCode, legacyCompletion.UnfilteredExitCode);

        Random random = new();
        int currentRound = 100;

        while (currentRound > 0)
        {
            string currentString = RandomString(random.Next(1024, 1024 * 1024 * 2), random);
            await namedPipeClient.RequestReplyAsync<TextMessage, VoidResponse>(new TextMessage(currentString), CancellationToken.None);
            Assert.HasCount(1, receivedMessages);
            Assert.AreEqual(receivedMessages.Dequeue(), new TextMessage(currentString));
            currentRound--;
        }

#if NETCOREAPP
        namedPipeClient.Dispose();
        await singleConnectionNamedPipeServer.DisposeAsync();
#else
        namedPipeClient.Dispose();
        singleConnectionNamedPipeServer.Dispose();
#endif
    }

    [TestMethod]
    public async Task SingleConnectionNamedPipeServer_ClientDisconnectsBeforeReply_LogsConciseClosure()
    {
        PipeNameDescription pipeNameDescription = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));
        TaskCompletionSource<bool> requestReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> releaseResponse = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<string> loggedMessage = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<ILogger> logger = new();
        logger
            .Setup(x => x.LogAsync(LogLevel.Debug, It.IsAny<string>(), null, LoggingExtensions.Formatter))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>(
                (_, message, _, _) =>
                {
                    if (message.Contains("while writing reply", StringComparison.Ordinal))
                    {
                        loggedMessage.TrySetResult(message);
                    }
                })
            .Returns(Task.CompletedTask);

        NamedPipeServer server = new(
            pipeNameDescription,
            async _ =>
            {
                requestReceived.TrySetResult(true);
                await releaseResponse.Task.ConfigureAwait(false);
                return VoidResponse.CachedInstance;
            },
            new SystemEnvironment(),
            logger.Object,
            new SystemTask(),
            _testContext.CancellationToken);
        server.RegisterSerializer(new IntMessageSerializer(), typeof(IntMessage));
        server.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
        NamedPipeClient client = new(
            pipeNameDescription.Name,
            new SystemEnvironment(),
            exitProcessOnConnectionLoss: false);
        client.RegisterSerializer(new IntMessageSerializer(), typeof(IntMessage));
        client.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));

        try
        {
            Task waitConnectionTask = server.WaitConnectionAsync(_testContext.CancellationToken);
            await client.ConnectAsync(_testContext.CancellationToken);
            await waitConnectionTask;

            Task<VoidResponse> requestTask = client.RequestReplyAsync<IntMessage, VoidResponse>(
                new IntMessage(42),
                _testContext.CancellationToken);
            await requestReceived.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
            client.Dispose();
            releaseResponse.TrySetResult(true);

            await loggedMessage.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
            string message = await loggedMessage.Task;
            Assert.AreEqual(
                $"Pipe '{pipeNameDescription.Name}' closed while writing reply; exiting server loop.",
                message);
            Task completedRequestTask = await Task.WhenAny(
                requestTask,
                Task.Delay(TimeoutHelper.DefaultHangTimeSpanTimeout, _testContext.CancellationToken));
            Assert.AreSame(requestTask, completedRequestTask, "The client request did not observe the closed pipe.");
            await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => requestTask);
        }
        finally
        {
            releaseResponse.TrySetResult(true);
            client.Dispose();
#if NETCOREAPP
            await server.DisposeAsync();
#else
            server.Dispose();
#endif
        }
    }

    [TestMethod]
    public async Task ConnectionNamedPipeServer_MultipleConnection_Succeeds()
    {
        PipeNameDescription pipeNameDescription = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));

        List<NamedPipeServer> pipes = [];
        for (int i = 0; i < 3; i++)
        {
            pipes.Add(new(
                pipeNameDescription,
                async _ => await Task.FromResult(VoidResponse.CachedInstance),
                new SystemEnvironment(),
                new Mock<ILogger>().Object,
                new SystemTask(),
                maxNumberOfServerInstances: 3,
                _testContext.CancellationToken));
        }

        IOException exception = Assert.ThrowsExactly<IOException>(() =>
             new NamedPipeServer(
                pipeNameDescription,
                async _ => await Task.FromResult(VoidResponse.CachedInstance),
                new SystemEnvironment(),
                new Mock<ILogger>().Object,
                new SystemTask(),
                maxNumberOfServerInstances: 3,
                _testContext.CancellationToken));
        Assert.Contains("All pipe instances are busy.", exception.Message);

        List<Task> waitConnectionTask = [];
        int connectionCompleted = 0;
        foreach (NamedPipeServer namedPipeServer in pipes)
        {
            waitConnectionTask.Add(Task.Run(
                async () =>
                {
                    await namedPipeServer.WaitConnectionAsync(_testContext.CancellationToken);
                    Interlocked.Increment(ref connectionCompleted);
                }, _testContext.CancellationToken));
        }

        List<NamedPipeClient> connectedClients = [];
        for (int i = 0; i < waitConnectionTask.Count; i++)
        {
            NamedPipeClient namedPipeClient = new(pipeNameDescription.Name);
            connectedClients.Add(namedPipeClient);
            await namedPipeClient.ConnectAsync(_testContext.CancellationToken);
        }

        await Task.WhenAll([.. waitConnectionTask]);

        Assert.AreEqual(3, connectionCompleted);

#pragma warning disable VSTHRD103 // Call async methods when in an async method
        foreach (NamedPipeClient namedPipeClient in connectedClients)
        {
            namedPipeClient.Dispose();
        }

        foreach (NamedPipeServer namedPipeServer in pipes)
        {
            namedPipeServer.Dispose();
        }
#pragma warning restore VSTHRD103 // Call async methods when in an async method
    }

    private static string RandomString(int length, Random random)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string([.. Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)])]);
    }

    private abstract record BaseMessage : IRequest;

    private sealed record TextMessage(string Text) : BaseMessage;

    private sealed class TextMessageSerializer : NamedPipeSerializer<TextMessage>, INamedPipeSerializer
    {
        public override int Id => 2;

        protected override TextMessage DeserializeCore(Stream stream) => new(ReadString(stream));

        protected override void SerializeCore(TextMessage objectToSerialize, Stream stream) => WriteString(stream, objectToSerialize.Text);
    }

    private sealed record IntMessage(int Integer) : BaseMessage;

    private sealed class IntMessageSerializer : NamedPipeSerializer<IntMessage>, INamedPipeSerializer
    {
        public override int Id => 3;

        protected override IntMessage DeserializeCore(Stream stream) => new(ReadInt(stream));

        protected override void SerializeCore(IntMessage objectToSerialize, Stream stream) => WriteInt(stream, objectToSerialize.Integer);
    }

    private sealed record LongMessage(long Long) : BaseMessage;

    private sealed class LongMessageSerializer : NamedPipeSerializer<LongMessage>, INamedPipeSerializer
    {
        public override int Id => 4;

        protected override LongMessage DeserializeCore(Stream stream) => new(ReadLong(stream));

        protected override void SerializeCore(LongMessage objectToSerialize, Stream stream) => WriteLong(stream, objectToSerialize.Long);
    }

    private sealed class LegacyTestHostCompletedRequestSerializer
        : NamedPipeSerializer<TestHostCompletedRequest>, INamedPipeSerializer
    {
        public override int Id => TestHostCompletedRequestFieldsId.MessagesSerializerId;

        protected override TestHostCompletedRequest DeserializeCore(Stream stream) => new(ReadInt(stream));

        protected override void SerializeCore(TestHostCompletedRequest objectToSerialize, Stream stream)
            => WriteInt(stream, objectToSerialize.ExitCode);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task Client_SerializerWriteFailuresPreserveIdentityAndPrimaryExitPolicy(bool primary, bool disposed)
    {
        Exception expected = disposed
            ? new ObjectDisposedException("owned serializer")
            : new IOException("owned serialization failure");
        Mock<IEnvironment> environment = new();
        using NamedPipeClient client = new(Guid.NewGuid().ToString("N"), environment.Object, primary);
        client.RegisterSerializer(new WriteFailureSerializer(expected), typeof(IntMessage));

        Exception actual = disposed
            ? await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
                () => client.RequestReplyAsync<IntMessage, VoidResponse>(new(42), _testContext.CancellationToken))
            : await Assert.ThrowsExactlyAsync<IOException>(
                () => client.RequestReplyAsync<IntMessage, VoidResponse>(new(42), _testContext.CancellationToken));

        Assert.AreSame(expected, actual);
        environment.Verify(e => e.Exit(1), primary ? Times.Once() : Times.Never());
    }

    [TestMethod]
    [DataRow("primary")]
    [DataRow("auxiliary")]
    [DataRow("broken-stderr")]
    public async Task Client_PeerEofPreservesPrimaryAndAuxiliaryDiagnostics(string kind)
    {
        SystemWrappersTests.ChildResult child = await SystemWrappersTests.RunChildAsync("pipe-eof-" + kind);

        Assert.AreEqual(0, child.ExitCode, child.Error);
        Assert.AreSequenceEqual(
            new[] { "request-frame:8,3,42", "eof-contract:" + kind },
            ReadPipeChildRecords(child.Output));
    }

    [TestMethod]
    [DataRow("sync")]
    [DataRow("missing-loop")]
#if NETCOREAPP
    [DataRow("async")]
#endif
    public async Task Server_ZeroOwnedHangTimeoutReportsBlockedLoopAndNullLoopCompletes(string kind)
    {
        SystemWrappersTests.ChildResult child = await SystemWrappersTests.RunChildAsync("pipe-lifecycle-" + kind);

        Assert.AreEqual(0, child.ExitCode, child.Error);
        string[] expected = kind == "missing-loop"
            ? ["disconnect:False", "dispose:completed", "errors:0"]
            : ["disconnect:False", "dispose:Method 'InternalLoopAsync' did not exit successfully", "errors:1"];
        Assert.AreSequenceEqual(expected, ReadPipeChildRecords(child.Output));
    }

    internal static async Task RunPeerEofChildAsync(string mode, Action<string> record)
    {
        bool primary = mode != "pipe-eof-auxiliary";
        bool brokenDiagnostic = mode == "pipe-eof-broken-stderr";
        string name = NamedPipeServer.GetPipeName("eof-" + Guid.NewGuid().ToString("N")).Name;
        using System.IO.Pipes.NamedPipeServerStream peer = new(
            name, System.IO.Pipes.PipeDirection.InOut, 1, System.IO.Pipes.PipeTransmissionMode.Byte, System.IO.Pipes.PipeOptions.Asynchronous);
        Mock<IEnvironment> environment = new();
        using NamedPipeClient client = new(name, environment.Object, primary);
        client.RegisterSerializer(new IntMessageSerializer(), typeof(IntMessage));
        Task accepted = peer.WaitForConnectionAsync(CancellationToken.None);
        await client.ConnectAsync(CancellationToken.None);
        await accepted;
        TextWriter originalError = Console.Error;
        using StringWriter diagnostic = new(CultureInfo.InvariantCulture);
        using ThrowingPipeDiagnosticWriter unavailableDiagnostic = new();
        Console.SetError(brokenDiagnostic ? unavailableDiagnostic : diagnostic);
        try
        {
            // Windows pipe draining may complete synchronously; let the owned peer drain in parallel.
            Task<VoidResponse> request = Task.Run(
                () => client.RequestReplyAsync<IntMessage, VoidResponse>(new(42), CancellationToken.None),
                CancellationToken.None);
            byte[] frame = new byte[12];
            int offset = 0;
            while (offset < frame.Length)
            {
                int read = await peer.ReadAsync(frame, offset, frame.Length - offset, CancellationToken.None);
                Assert.IsGreaterThan(0, read);
                offset += read;
            }

            byte[] expectedFrame = [.. BitConverter.GetBytes(8), .. BitConverter.GetBytes(3), .. BitConverter.GetBytes(42)];
            Assert.AreSequenceEqual(expectedFrame, frame);
            record("request-frame:8,3,42");
            peer.Dispose();
            Exception failure = primary
                ? await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => request)
                : await Assert.ThrowsExactlyAsync<IOException>(() => request);
            Assert.AreEqual($"Pipe '{name}' was closed by the server before a response was received.", failure.Message);
            environment.Verify(e => e.Exit(1), primary ? Times.Once() : Times.Never());
            string expectedDiagnostic = primary && !brokenDiagnostic
                ? $"[NamedPipeClient] Pipe '{name}' was closed by the server before a response was received. The peer process likely exited or was killed. Terminating with exit code 1." + Environment.NewLine
                : string.Empty;
            Assert.AreEqual(expectedDiagnostic, diagnostic.ToString());
            record("eof-contract:" + mode["pipe-eof-".Length..]);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    internal static async Task RunLifecycleChildAsync(string mode, Action<string> record)
    {
        Environment.SetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DEFAULT_HANG_TIMEOUT, "0s");
        Assert.AreEqual(TimeSpan.Zero, TimeoutHelper.DefaultHangTimeSpanTimeout);
        bool missingLoop = mode == "pipe-lifecycle-missing-loop";
        TaskCompletionSource<bool> loop = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<ITask> task = new();
        task.Setup(t => t.Run(It.IsAny<Func<Task>>(), CancellationToken.None))
            .Returns(missingLoop ? null! : loop.Task);
        List<string> errors = [];
        PipeErrorLogger logger = new(errors);
        NamedPipeServer server = new(
            "lifecycle-" + Guid.NewGuid().ToString("N"),
            _ => throw new UnreachableException(),
            Mock.Of<IEnvironment>(), logger, task.Object, CancellationToken.None);
        try
        {
            using System.IO.Pipes.NamedPipeClientStream client = new(
                ".", server.PipeName.Name, System.IO.Pipes.PipeDirection.InOut, System.IO.Pipes.PipeOptions.Asynchronous);
            Task accepted = server.WaitConnectionAsync(CancellationToken.None);
            await client.ConnectAsync(CancellationToken.None);
            await accepted;
            Assert.IsTrue(server.WasConnected);
            bool disconnected = await server.WaitForDisconnectAsync(TimeSpan.Zero);
            Assert.IsFalse(disconnected);
            record("disconnect:" + disconnected);
            if (missingLoop)
            {
                server.Dispose();
                Assert.IsEmpty(errors);
                record("dispose:completed");
            }
            else
            {
                InvalidOperationException failure;
#if NETCOREAPP
                if (mode == "pipe-lifecycle-async")
                {
                    failure = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await server.DisposeAsync());
                }
                else
#endif
                {
                    Action dispose = server.Dispose;
                    failure = Assert.ThrowsExactly<InvalidOperationException>(dispose);
                }

                Assert.AreEqual("Method 'InternalLoopAsync' did not exit successfully", failure.Message);
                string method = mode == "pipe-lifecycle-async" ? "DisposeAsync" : "Dispose";
                Assert.AreEqual(
                    $"NamedPipeServer.{method}: 'InternalLoopAsync' for pipe '{server.PipeName.Name}' did not complete within 00:00:00. WasConnected=True, LoopTaskStatus=WaitingForActivation.",
                    Assert.ContainsSingle(errors));
                record("dispose:" + failure.Message);
            }

            record("errors:" + errors.Count.ToString(CultureInfo.InvariantCulture));
            task.Verify(t => t.Run(It.IsAny<Func<Task>>(), CancellationToken.None), Times.Once);
        }
        finally
        {
            loop.TrySetResult(true);
            server.GetServerStream().Dispose();
            server.Dispose();
        }
    }

    private static string[] ReadPipeChildRecords(string output)
        => output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimStart('\uFEFF'))
            .Where(line => line.StartsWith("PROBE|", StringComparison.Ordinal))
            .Select(line => Encoding.UTF8.GetString(Convert.FromBase64String(line["PROBE|".Length..])))
            .ToArray();

    private sealed class WriteFailureSerializer(Exception failure) : NamedPipeSerializer<IntMessage>
    {
        public override int Id => 99;

        protected override IntMessage DeserializeCore(Stream stream) => throw new UnreachableException();

        protected override void SerializeCore(IntMessage objectToSerialize, Stream stream) => throw failure;
    }

    private sealed class ThrowingPipeDiagnosticWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override Task WriteLineAsync(string? value) => Task.FromException(new IOException("owned unavailable stderr"));

#if NETCOREAPP
        public override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
            => Task.FromException(new IOException("owned unavailable stderr"));
#endif
    }

    private sealed class PipeErrorLogger(List<string> errors) : ILogger
    {
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
            {
                errors.Add(formatter(state, exception));
            }
        }

        public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Log(logLevel, state, exception, formatter);
            return Task.CompletedTask;
        }
    }
}
