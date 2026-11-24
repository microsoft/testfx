// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipes;

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;

namespace Microsoft.Testing.Platform.UnitTests;

/// <summary>
/// Owns a real local framed-protocol server without importing the shipping protocol's hidden base interfaces.
/// Only the existing canonical serializer bridge crosses that assembly-identity boundary.
/// </summary>
[UnsupportedOSPlatform("browser")]
#if !NETFRAMEWORK
[UnsupportedOSPlatform("wasi")]
#endif
internal sealed class OwnedProtocolServerPeer : IDisposable
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly NamedPipeServerStream _stream;
    private readonly Task _loop;
    private bool _disposed;

    internal OwnedProtocolServerPeer(Func<int, byte[], (int Id, byte[] Payload)> reply)
    {
        PipeName = NamedPipeServer.GetPipeName("owned-protocol-" + Guid.NewGuid().ToString("N")).Name;
        _stream = new(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        _loop = RunAsync(reply);
    }

    internal string PipeName { get; }

    internal Task WaitForDisconnectAsync() => _loop.TimeoutAfterAsync(Limit);

    internal static byte[] Serialize<T>(object serializer, T message)
    {
        using MemoryStream payload = new();
        ProtocolSerializerTestHelper.Serialize(serializer, message, payload);
        return payload.ToArray();
    }

    internal static T Deserialize<T>(object serializer, byte[] payload)
    {
        using MemoryStream stream = new(payload, writable: false);
        return (T)ProtocolSerializerTestHelper.Deserialize(serializer, stream);
    }

    private async Task RunAsync(Func<int, byte[], (int Id, byte[] Payload)> reply)
    {
        try
        {
            await _stream.WaitForConnectionAsync(_lifetime.Token);
            while (true)
            {
                byte[] header = new byte[4];
                int first = await _stream.ReadAsync(header, 0, header.Length, _lifetime.Token);
                if (first == 0)
                {
                    return;
                }

                await ReadRestAsync(header, first);
                int size = BitConverter.ToInt32(header, 0);
                Assert.IsGreaterThanOrEqualTo(4, size);
                Assert.IsLessThanOrEqualTo(1_000_000, size);
                byte[] framedPayload = new byte[size];
                await ReadRestAsync(framedPayload, 0);
                int id = BitConverter.ToInt32(framedPayload, 0);
                byte[] payload = framedPayload.Skip(4).ToArray();
                (int responseId, byte[] responsePayload) = reply(id, payload);
                byte[] frame = [.. BitConverter.GetBytes(checked(responsePayload.Length + 4)), .. BitConverter.GetBytes(responseId), .. responsePayload];
                await _stream.WriteAsync(frame, 0, frame.Length, _lifetime.Token);
                await _stream.FlushAsync(_lifetime.Token);
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (IOException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (_lifetime.IsCancellationRequested)
        {
        }
    }

    private async Task ReadRestAsync(byte[] buffer, int offset)
    {
        while (offset < buffer.Length)
        {
            int count = await _stream.ReadAsync(buffer, offset, buffer.Length - offset, _lifetime.Token);
            Assert.IsGreaterThan(0, count, "The owned peer received a truncated frame.");
            offset += count;
        }
    }

    public void Dispose() => DisposeAsync().GetAwaiter().GetResult();

    public async Task DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetime.Cancel();
        _stream.Dispose();
        await _loop.TimeoutAfterAsync(Limit);
        _lifetime.Dispose();
    }
}
