// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Framework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public sealed class IPCTests : TestBase
{
    private readonly ITestExecutionContext _testExecutionContext;

    public IPCTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
        _testExecutionContext = testExecutionContext;
    }

    public async Task SingleConnectionNamedPipeServer_MultipleConnection_Fails()
    {
        PipeNameDescription pipeNameDescription = SingleConnectionNamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));

        List<SingleConnectionNamedPipeServer> openedPipe = [];
        List<Exception> exceptions = [];

        ManualResetEventSlim waitException = new(false);
        var waitTask = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    SingleConnectionNamedPipeServer singleConnectionNamedPipeServer = new(
                        pipeNameDescription,
                        async _ => await Task.FromResult(VoidResponse.CachedInstance),
                        new SystemEnvironment(),
                        new Mock<ILogger>().Object,
                        new SystemTask(),
                        _testExecutionContext.CancellationToken);

                    await singleConnectionNamedPipeServer.WaitConnectionAsync(_testExecutionContext.CancellationToken);
                    openedPipe.Add(singleConnectionNamedPipeServer);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
                waitException.Set();
            }
        });

        NamedPipeClient namedPipeClient1 = new(pipeNameDescription.Name);
        await namedPipeClient1.ConnectAsync(_testExecutionContext.CancellationToken);
        waitException.Wait();

        Assert.AreEqual(1, openedPipe.Count);
        Assert.AreEqual(1, exceptions.Count);
        Assert.AreEqual(exceptions[0].GetType(), typeof(IOException));
        Assert.IsTrue(exceptions[0].Message.Contains("All pipe instances are busy."));

        await waitTask;
#if NETCOREAPP
        await namedPipeClient1.DisposeAsync();
        await openedPipe[0].DisposeAsync();
#else
        namedPipeClient1.Dispose();
        openedPipe[0].Dispose();
#endif
        pipeNameDescription.Dispose();

        // Verify double dispose
#if NETCOREAPP
        await namedPipeClient1.DisposeAsync();
        await openedPipe[0].DisposeAsync();
#else
        namedPipeClient1.Dispose();
        openedPipe[0].Dispose();
#endif
        pipeNameDescription.Dispose();
    }

    public async Task SingleConnectionNamedPipeServer_RequestReplySerialization_Succeeded()
    {
        Queue<BaseMessage> receivedMessages = new();
        PipeNameDescription pipeNameDescription = SingleConnectionNamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));
        NamedPipeClient namedPipeClient = new(pipeNameDescription.Name);
        namedPipeClient.RegisterSerializer<VoidResponse>(new VoidResponseSerializer());
        namedPipeClient.RegisterSerializer<TextMessage>(new TextMessageSerializer());
        namedPipeClient.RegisterSerializer<IntMessage>(new IntMessageSerializer());
        namedPipeClient.RegisterSerializer<LongMessage>(new LongMessageSerializer());

        ManualResetEventSlim manualResetEventSlim = new(false);
        var clientConnected = Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    await namedPipeClient.ConnectAsync(CancellationToken.None);
                    manualResetEventSlim.Set();
                    break;
                }
                catch (OperationCanceledException ct) when (ct.CancellationToken == _testExecutionContext.CancellationToken)
                {
                    throw new OperationCanceledException("SingleConnectionNamedPipeServer_RequestReplySerialization_Succeeded cancellation during connect, _testExecutionContext.CancellationToken");
                }
                catch (OperationCanceledException)
                {
                    throw new OperationCanceledException("SingleConnectionNamedPipeServer_RequestReplySerialization_Succeeded cancellation during connect");
                }
                catch (Exception)
                {
                    continue;
                }
            }
        });
        SingleConnectionNamedPipeServer singleConnectionNamedPipeServer = new(
            pipeNameDescription,
            (IRequest request) =>
            {
                receivedMessages.Enqueue((BaseMessage)request);
                return Task.FromResult((IResponse)VoidResponse.CachedInstance);
            },
            new SystemEnvironment(),
            new Mock<ILogger>().Object,
            new SystemTask(),
            CancellationToken.None);
        singleConnectionNamedPipeServer.RegisterSerializer<VoidResponse>(new VoidResponseSerializer());
        singleConnectionNamedPipeServer.RegisterSerializer<TextMessage>(new TextMessageSerializer());
        singleConnectionNamedPipeServer.RegisterSerializer<IntMessage>(new IntMessageSerializer());
        singleConnectionNamedPipeServer.RegisterSerializer<LongMessage>(new LongMessageSerializer());
        await singleConnectionNamedPipeServer.WaitConnectionAsync(CancellationToken.None);
        manualResetEventSlim.Wait();

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
            Assert.AreEqual(1, receivedMessages.Count);
            Assert.AreEqual(receivedMessages.Dequeue(), new TextMessage(currentString));
            currentRound--;
        }

#if NETCOREAPP
        await namedPipeClient.DisposeAsync();
        await singleConnectionNamedPipeServer.DisposeAsync();
#else
        namedPipeClient.Dispose();
        singleConnectionNamedPipeServer.Dispose();
#endif

        pipeNameDescription.Dispose();
    }

    private static string RandomString(int length, Random random)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    private abstract record class BaseMessage : IRequest
    {
    }

    private sealed record class TextMessage(string Text) : BaseMessage;

    private sealed class TextMessageSerializer : BaseSerializer, INamedPipeSerializer
    {
        public int Id => 2;

        public object Deserialize(Stream stream) => new TextMessage(ReadString(stream));

        public void Serialize(object objectToSerialize, Stream stream) => WriteString(stream, ((TextMessage)objectToSerialize).Text);
    }

    private sealed record class IntMessage(int Integer) : BaseMessage;

    private sealed class IntMessageSerializer : BaseSerializer, INamedPipeSerializer
    {
        public int Id => 3;

        public object Deserialize(Stream stream) => new IntMessage(ReadInt(stream));

        public void Serialize(object objectToSerialize, Stream stream) => WriteInt(stream, ((IntMessage)objectToSerialize).Integer);
    }

    private sealed record class LongMessage(long Long) : BaseMessage;

    private sealed class LongMessageSerializer : BaseSerializer, INamedPipeSerializer
    {
        public int Id => 4;

        public object Deserialize(Stream stream) => new LongMessage(ReadInt(stream));

        public void Serialize(object objectToSerialize, Stream stream) => WriteLong(stream, ((LongMessage)objectToSerialize).Long);
    }
}
