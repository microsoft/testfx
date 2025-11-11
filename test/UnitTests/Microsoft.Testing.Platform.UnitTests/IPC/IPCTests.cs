// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using EasyNamedPipes;

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class IPCTests
{
    private readonly TestContext _testContext;

    public IPCTests(TestContext testContext)
        => _testContext = testContext;

    [TestMethod]
    public async Task SingleConnectionNamedPipeServer_MultipleConnection_Fails()
    {
        PipeNameDescription pipeNameDescription = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"), new SystemEnvironment());

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
        Queue<BaseMessage> receivedMessages = new();
        PipeNameDescription pipeNameDescription = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"), new SystemEnvironment());
        NamedPipeClient namedPipeClient = new(pipeNameDescription.Name);
        namedPipeClient.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
        namedPipeClient.RegisterSerializer(new TextMessageSerializer(), typeof(TextMessage));
        namedPipeClient.RegisterSerializer(new IntMessageSerializer(), typeof(IntMessage));
        namedPipeClient.RegisterSerializer(new LongMessageSerializer(), typeof(LongMessage));

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
                receivedMessages.Enqueue((BaseMessage)request);
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
        await singleConnectionNamedPipeServer.WaitConnectionAsync(CancellationToken.None);
        manualResetEventSlim.Wait(_testContext.CancellationToken);

        await clientConnected.WithCancellationAsync(CancellationToken.None);

        await namedPipeClient.RequestReplyAsync<IntMessage, VoidResponse>(new IntMessage(10), CancellationToken.None);
        Assert.AreEqual(receivedMessages.Dequeue(), new IntMessage(10));

        await namedPipeClient.RequestReplyAsync<LongMessage, VoidResponse>(new LongMessage(11), CancellationToken.None);
        Assert.AreEqual(receivedMessages.Dequeue(), new LongMessage(11));

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
    public async Task ConnectionNamedPipeServer_MultipleConnection_Succeeds()
    {
        PipeNameDescription pipeNameDescription = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"), new SystemEnvironment());

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

    private sealed class TextMessageSerializer : BaseSerializer, INamedPipeSerializer
    {
        public int Id => 2;

        public object Deserialize(Stream stream) => new TextMessage(ReadString(stream));

        public void Serialize(object objectToSerialize, Stream stream) => WriteString(stream, ((TextMessage)objectToSerialize).Text);
    }

    private sealed record IntMessage(int Integer) : BaseMessage;

    private sealed class IntMessageSerializer : BaseSerializer, INamedPipeSerializer
    {
        public int Id => 3;

        public object Deserialize(Stream stream) => new IntMessage(ReadInt(stream));

        public void Serialize(object objectToSerialize, Stream stream) => WriteInt(stream, ((IntMessage)objectToSerialize).Integer);
    }

    private sealed record LongMessage(long Long) : BaseMessage;

    private sealed class LongMessageSerializer : BaseSerializer, INamedPipeSerializer
    {
        public int Id => 4;

        public object Deserialize(Stream stream) => new LongMessage(ReadInt(stream));

        public void Serialize(object objectToSerialize, Stream stream) => WriteLong(stream, ((LongMessage)objectToSerialize).Long);
    }
}
