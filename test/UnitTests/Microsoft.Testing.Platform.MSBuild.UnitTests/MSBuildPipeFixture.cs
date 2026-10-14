// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;

namespace Microsoft.Testing.Platform.MSBuild.UnitTests;

/// <summary>
/// An independently decoded, per-test MSBuild peer. It owns no process-global state or port.
/// Requests are recorded before their acknowledgement, so a completed client call can inspect them.
/// </summary>
internal sealed class MSBuildPipeFixture : IAsyncDisposable
{
    internal static readonly TimeSpan OperationLimit = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CleanupLimit = TimeSpan.FromSeconds(30);
    private readonly NamedPipeServerStream _server;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly ConcurrentQueue<object> _requests = new();
    private readonly TaskCompletionSource _disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task _worker;

    public MSBuildPipeFixture()
    {
        PipeName = $"testfx-msbuild-{Guid.NewGuid():N}";
        _server = new(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        _worker = ServeAsync();
    }

    public string PipeName { get; }

    public T[] Received<T>() => _requests.OfType<T>().ToArray();

    public Task WaitForDisconnectAsync() => _disconnected.Task.WaitAsync(OperationLimit);

    public async ValueTask DisposeAsync()
    {
        // Independent cleanup cancellation is not the already-cancelled operation token.
        // Join the original worker before disposing its stream or cancellation source.
        await _lifetime.CancelAsync();
        try
        {
            await _worker.WaitAsync(CleanupLimit);
        }
        finally
        {
            _server.Dispose();
            _lifetime.Dispose();
        }
    }

    private async Task ServeAsync()
    {
        try
        {
            await _server.WaitForConnectionAsync(_lifetime.Token);
            byte[] header = new byte[sizeof(int)];
            while (true)
            {
                int firstRead = await _server.ReadAsync(header, _lifetime.Token);
                if (firstRead == 0)
                {
                    _disconnected.SetResult();
                    return;
                }

                await _server.ReadExactlyAsync(header.AsMemory(firstRead), _lifetime.Token);
                int payloadLength = BitConverter.ToInt32(header);
                if (payloadLength is < sizeof(int) or > 1_000_000)
                {
                    throw new InvalidDataException($"Invalid MSBuild frame length: {payloadLength}.");
                }

                byte[] payload = new byte[payloadLength];
                await _server.ReadExactlyAsync(payload, _lifetime.Token);
                using MemoryStream body = new(payload, writable: false);
                using BinaryReader reader = new(body, Encoding.UTF8);
                object request = reader.ReadInt32() switch
                {
                    1 => new MSBuildModuleInfo(ReadString(reader), ReadString(reader), ReadString(reader)),
                    2 => new MSBuildFailureInfo(
                        ReadString(reader),
                        reader.ReadInt32() == 1,
                        ReadString(reader),
                        ReadString(reader),
                        ReadString(reader),
                        ReadString(reader),
                        ReadString(reader),
                        ReadString(reader),
                        reader.ReadInt32()),
                    3 => new MSBuildSummaryInfo(
                        reader.ReadInt32(),
                        reader.ReadInt32(),
                        reader.ReadInt32(),
                        reader.ReadInt32(),
                        ReadString(reader),
                        reader.ReadInt32() != 0),
                    int id => throw new InvalidDataException($"Unexpected MSBuild request ID: {id}."),
                };
                if (body.Position != body.Length)
                {
                    throw new InvalidDataException("The MSBuild request contained unconsumed bytes.");
                }

                _requests.Enqueue(request);

                // VoidResponse's frame has a four-byte payload containing serializer ID zero.
                byte[] response = [4, 0, 0, 0, 0, 0, 0, 0];
                await _server.WriteAsync(response, _lifetime.Token);
                await _server.FlushAsync(_lifetime.Token);
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // Fixture teardown requested termination. Worker faults other than cancellation propagate.
        }
    }

    private static string ReadString(BinaryReader reader)
    {
        int byteCount = reader.ReadInt32();
        byte[] bytes = reader.ReadBytes(byteCount);
        return bytes.Length == byteCount
            ? Encoding.UTF8.GetString(bytes)
            : throw new EndOfStreamException();
    }
}

internal sealed record MSBuildModuleInfo(string FrameworkDescription, string ProcessArchitecture, string TestResultFolder);

internal sealed record MSBuildFailureInfo(
    string DisplayName,
    bool IsCanceled,
    string Duration,
    string ErrorMessage,
    string ErrorStackTrace,
    string Expected,
    string Actual,
    string CodeFilePath,
    int LineNumber);

internal sealed record MSBuildSummaryInfo(int Total, int Failed, int Passed, int Skipped, string Duration, bool AllowSkipped);
